using ContractWatcher.SDK.Execution.Delegates;
using ContractWatcher.SDK.Execution.Interfaces;
using ContractWatcher.SDK.Execution.Models;

namespace ContractWatcher.SDK.Tests.Fakes;

public sealed class FakeMiddleware(Func<WatcherContext, WatcherDelegate, CancellationToken, Task> action) : IWatcherMiddleware
{
    public Task InvokeAsync(WatcherContext context, WatcherDelegate next, CancellationToken cancellationToken) =>
        action(context, next, cancellationToken);
}