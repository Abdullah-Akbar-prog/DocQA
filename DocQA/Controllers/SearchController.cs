using DocQA.Models;
using DocQA.Rag;
using Microsoft.AspNetCore.Mvc;

namespace DocQA.Controllers;

[ApiController]
[Route("api/search")]
public class SearchController(RetrievalService retrieval) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<IReadOnlyList<RetrievedChunk>>> Search(AskRequest request, CancellationToken ct) =>
        Ok(await retrieval.SearchAsync(request.Question, request.TopK, ct));
}