using DocQA.Auth;
using DocQA.Models;
using DocQA.Rag;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
// ...
[Authorize]
[ApiController]
[Route("api/search")]
public class SearchController(RetrievalService retrieval) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<IReadOnlyList<RetrievedChunk>>> Search(AskRequest request, CancellationToken ct) =>
        Ok(await retrieval.SearchAsync(request.Question, request.TopK, User.GetUserId(), ct));
}