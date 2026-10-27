using DocQA.Rag;

namespace DocQA.Features.Ask;

public sealed record AskRequest(string Question, int? TopK);

public static class AskEndpoints
{
    private const int MaxQuestionLength = 2000;

    public static IEndpointRouteBuilder MapAskEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/ask", Ask).WithTags("Ask");
        return app;
    }

    private static async Task<IResult> Ask(AskRequest request, AskService ask, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return Invalid("Question is required.");

        if (request.Question.Length > MaxQuestionLength)
            return Invalid($"Question must be at most {MaxQuestionLength} characters.");

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

    private static IResult Invalid(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["question"] = [message] });
}