using ContractWatcher.SDK.Reporting.Delegates;
using ContractWatcher.SDK.Reporting.Handlers;
using ContractWatcher.SDK.Tests.Fakes;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ReportingFixture;

namespace ContractWatcher.SDK.Tests.Reporting.Handlers;

public sealed class ReportMetadataHandlerTests
{
    [Fact]
    public void Constructor_WhenTimeProviderIsNull_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new ReportMetadataHandler(null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void Handle_GeneratesReportId()
    {
        // Arrange
        var handler = new ReportMetadataHandler(TimeProvider.System);
        var context = CreateReportBuildContext();

        ReportBuildDelegate next = _ => { };

        // Act
        handler.Handle(context, next);

        // Assert
        Assert.NotEqual(Guid.Empty, context.ReportId);
    }

    [Fact]
    public void Handle_SetsTimestamp()
    {
        // Arrange
        var timestamp = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var timeProvider = new FakeTimeProvider(timestamp);
        var handler = new ReportMetadataHandler(timeProvider);
        var context = CreateReportBuildContext();

        ReportBuildDelegate next = _ => { };

        // Act
        handler.Handle(context, next);

        // Assert
        Assert.Equal(timestamp, context.Timestamp);
    }

    [Fact]
    public void Handle_CallsNext()
    {
        // Arrange
        var handler = new ReportMetadataHandler(TimeProvider.System);
        var context = CreateReportBuildContext();
        var nextCalled = false;

        ReportBuildDelegate next = _ => { nextCalled = true; };

        // Act
        handler.Handle(context, next);

        // Assert
        Assert.True(nextCalled);
    }

    [Fact]
    public void Handle_WhenContextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new ReportMetadataHandler(TimeProvider.System);

        ReportBuildDelegate next = _ => { };

        // Act
        var action = () => handler.Handle(null!, next);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }

    [Fact]
    public void Handle_WhenNextIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var handler = new ReportMetadataHandler(TimeProvider.System);
        var context = CreateReportBuildContext();

        // Act
        var action = () => handler.Handle(context, null!);

        // Assert
        Assert.Throws<ArgumentNullException>(action);
    }
}