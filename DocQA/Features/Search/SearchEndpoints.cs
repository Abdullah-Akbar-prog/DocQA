using DocQA.Rag;

namespace DocQA.Features.Search;

public sealed record SearchRequest(string Question, int? TopK);

public static class SearchEndpoints
{
    private const int MaxQuestionLength = 2000;

    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/search", Search).WithTags("Search");
        return app;
    }

    private static async Task<IResult> Search(SearchRequest request, RetrievalService retrieval, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return Invalid("Question is required.");

        if (request.Question.Length > MaxQuestionLength)
            return Invalid($"Question must be at most {MaxQuestionLength} characters.");

        return Results.Ok(await retrieval.SearchAsync(request.Question, request.TopK, ct));
    }

    private static IResult Invalid(string message) =>
        Results.ValidationProblem(new Dictionary<string, string[]> { ["question"] = [message] });
}