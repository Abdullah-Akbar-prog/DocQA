using DocQA.Auth;
using DocQA.Models;
using DocQA.Rag;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DocQA.Controllers;

[ApiController]
[Route("api/ask")]
public class AskController(AskService ask, ILogger<AskController> logger) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [HttpPost]
    public async Task<ActionResult<AskResult>> Ask(AskRequest request, CancellationToken ct)
    {
        try
        {
            return await ask.AskAsync(request.Question, request.TopK, ct);
        }
        catch (HttpRequestException)
        {
            return Problem(
                title: "The language model is not reachable. Is Ollama running?",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Problem(
                title: "The language model took too long to answer.",
                statusCode: StatusCodes.Status504GatewayTimeout);
        }
    }

    /// <summary>Server-Sent Events. Event order: "sources" (once), "token" (many), "done". Or "error".</summary>
    [HttpPost("stream")]
    public async Task Stream(AskRequest request, CancellationToken ct)
    {
        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no"; // stops nginx buffering the stream

        try
        {
            var userId = User.GetUserId();
            await foreach (var evt in ask.AskStreamAsync(request.Question, request.TopK, userId, ct))
            {
                switch (evt)
                {
                    case SourcesEvent sources:
                        await WriteEventAsync("sources", JsonSerializer.Serialize(sources.Sources, Json), ct);
                        break;
                    case TokenEvent token:
                        await WriteEventAsync("token", JsonSerializer.Serialize(token.Text, Json), ct);
                        break;
                    case DoneEvent done:
                        await WriteEventAsync("done", JsonSerializer.Serialize(new { declined = done.Declined }, Json), ct);
                        break;
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The client disconnected or pressed Stop. Passing ct stopped the model too.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Streaming answer failed");
            var payload = JsonSerializer.Serialize(new { message = FriendlyMessage(ex) }, Json);
            await WriteEventAsync("error", payload, CancellationToken.None);
        }
    }

    private async Task WriteEventAsync(string name, string data, CancellationToken ct)
    {
        await Response.WriteAsync($"event: {name}\ndata: {data}\n\n", ct);
        await Response.Body.FlushAsync(ct); // send it now instead of waiting for a full buffer
    }

    private static string FriendlyMessage(Exception ex) => ex switch
    {
        HttpRequestException => "The language model is not reachable. Is Ollama running?",
        TaskCanceledException => "The language model took too long to answer.",
        _ => "The answer could not be generated."
    };
}