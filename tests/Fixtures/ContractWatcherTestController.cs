using ContractWatcher.SDK.AspNetCore.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace ContractWatcher.SDK.Tests.Fixtures;

[ApiController]
[Route("integration")]
public sealed class ContractWatcherTestController : ControllerBase
{
    [HttpPost("watched")]
    [ContractWatcher("products")]
    public ActionResult<TestRequest> Watched([FromBody] TestRequest request) => Ok(request);

    [HttpPost("plain")]
    public ActionResult<TestRequest> Plain([FromBody] TestRequest request) => Ok(request);
}

public sealed record TestRequest(string Name, decimal Price);