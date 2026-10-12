namespace ContractWatcher.SDK.Tests.Fakes;

public sealed class FakeTimeProvider(DateTimeOffset timestamp) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => timestamp;
}