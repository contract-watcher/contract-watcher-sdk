using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace ContractWatcher.SDK.Tests.Fixtures;

public static class AspNetCoreFixture
{
    public static ResourceExecutingContext CreateResourceContext(string body = "{}", params object[] metadata)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(body));

        var endpoint = new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(metadata), "test");
        httpContext.SetEndpoint(endpoint);

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());

        return new ResourceExecutingContext(actionContext, [], []);
    }
}