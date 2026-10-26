using ContractWatcher.SDK.Data.Mappings;
using ContractWatcher.SDK.Tests.Fixtures;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ContractFixture;

namespace ContractWatcher.SDK.Tests.Data.Services;

public sealed class DatabaseAccessTests
{
    [Fact]
    public async Task Read_WhenCallbackThrows_ReleasesGateForNextWrite()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var session = fixture.Open();

        // Act
        Assert.Throws<InvalidOperationException>(() => 
            session.Access.Read<int>(_ => throw new InvalidOperationException()));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var result = await session.Access.WriteAsync(_ => 42, timeout.Token);

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task WriteAsync_WhenCallbackThrows_ReleasesGateAndRetainsPendingChanges()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var session = fixture.Open();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.Access.WriteAsync<int>(db =>
            {
                db.Contracts.Add(CreateContract().ToEntity(fixture.Clock.GetUtcNow()));

                throw new InvalidOperationException();
            }));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await session.Access.WriteAsync(_ => true, timeout.Token);

        // Assert
        // Ошибка callback не откатывает изменения сущностей в памяти.
        using var reopened = fixture.Open();

        Assert.NotNull(reopened.Contracts.Get("products"));
    }

    [Fact]
    public async Task WriteAsync_WhenTokenAlreadyCanceled_DoesNotInvokeCallback()
    {
        // Arrange
        using var fixture = new MicroDbFixture();
        using var session = fixture.Open();
        using var cancellation = new CancellationTokenSource();

        cancellation.Cancel();

        var invoked = false;

        // Act
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => 
            session.Access.WriteAsync(_ => invoked = true, cancellation.Token));

        // Assert
        Assert.False(invoked);
    }
}