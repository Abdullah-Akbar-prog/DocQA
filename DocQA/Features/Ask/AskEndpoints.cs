using DocQA.Rag;
using System.Text.Json;

namespace DocQA.Features.Ask;

public sealed record AskRequest(string Question, int? TopK);

public static class AskEndpoints
{
    private const int MaxQuestionLength = 2000;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapAskEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/ask", Ask).WithTags("Ask");
        app.MapPost("/api/ask/stream", Stream).WithTags("Ask");
        return app;
    }

    private static async Task<IResult> Ask(AskRequest request, AskService ask, CancellationToken ct)
    {
        if (Validate(request) is { } error) return Invalid(error);

        try
        {
            return Results.Ok(await ask.AskAsync(request.Question, request.TopK, ct));
        }
        catch (HttpRequestException)
        {
            return Results.Problem(
                title: "The language model is not reachable. Is Ollama running?",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return Results.Problem(
                title: "The language model took too long to answer.",
                statusCode: StatusCodes.Status504GatewayTimeout);
        }
    }

    /// <summary>Server-Sent Events. Event order: "sources" (once), "token" (many), "done". Or "error".</summary>
    private static async Task Stream(
        AskRequest request, AskService ask, HttpContext context, ILogger<AskService> logger)
    {
        var ct = context.RequestAborted;

        if (Validate(request) is { } error)
        {
            await Invalid(error).ExecuteAsync(context);
            return;
        }

        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers["X-Accel-Buffering"] = "no"; // stops nginx buffering the stream

        try
        {
            await foreach (var evt in ask.AskStreamAsync(request.Question, request.TopK, ct))
            {
                switch (evt)
                {
                    case SourcesEvent sources:
                        await WriteEventAsync(context, "sources", JsonSerializer.Serialize(sources.Sources, Json), ct);
                        break;
                    case TokenEvent token:
                        await WriteEventAsync(context, "token", JsonSerializer.Serialize(token.Text, Json), ct);
                        break;
                    case DoneEvent done:
                        await WriteEventAsync(context, "done", JsonSerializer.Serialize(new { declined = done.Declined }, Json), ct);
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
            await WriteEventAsync(context, "error", payload, CancellationToken.None);
        }
    }

    private static async Task WriteEventAsync(HttpContext context, string name, string data, CancellationToken ct)
    {
        await context.Response.WriteAsync($"event: {name}\ndata: {data}\n\n", ct);
        await context.Response.Body.FlushAsync(ct); // send it now instead of waiting for a full buffer
    }

    private static string FriendlyMessage(Exception ex) => ex switch
    {
        HttpRequestException => "The language model is not reachable. Is Ollama running?",
        TaskCanceledException => "The language model took too long to answer.",
        _ => "The answer could not be generated."
    };

    private static string? Validate(AskRequest request) =>
        string.IsNullOrWhiteSpace(request.Question) ? "Question is required."
        : request.Question.Length > MaxQuestionLength ? $"Question must be at most {MaxQuestionLength} characters."
        : null;

    private static IResult Invalid(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["question"] = [message] });
}