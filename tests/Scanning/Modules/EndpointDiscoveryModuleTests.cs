using ContractWatcher.SDK.Scanning.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

// Fixtures
using static ContractWatcher.SDK.Tests.Fixtures.ScanningFixture;

namespace ContractWatcher.SDK.Tests.Scanning.Modules;

public sealed class EndpointDiscoveryModuleTests
{
    [Fact]
    public async Task ExecuteAsync_WhenActionsAreMixed_FindsOnlyMarkedBody()
    {
        // Arrange
        var action = ActionDescriptor(
            nameof(ScanController.Explicit),
            BindingSource.Body,
            BindingSource.Query,
            BindingSource.Special);

        var module = Discovery(action, ActionDescriptor(nameof(ScanController.Ordinary)));

        var context = new ScanningContext();

        // Act
        await module.ExecuteAsync(context, default);

        // Assert
        var endpoint = Assert.Single(context.Endpoints!);

        Assert.Equal("products.explicit", endpoint.Slug);
        Assert.Equal(typeof(Request), endpoint.RequestType);
        Assert.Equal(action.MethodInfo, endpoint.Method);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSameActionHasSeveralRoutes_Deduplicates()
    {
        // Arrange
        var module = Discovery(
            ActionDescriptor(nameof(ScanController.Explicit), BindingSource.Body),
            ActionDescriptor(nameof(ScanController.Explicit), BindingSource.Body));

        var context = new ScanningContext();

        // Act
        await module.ExecuteAsync(context, default);

        // Assert
        Assert.Single(context.Endpoints!);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSlugsConflict_DoesNotCommitPartialResult()
    {
        // Arrange
        var module = Discovery(
            ActionDescriptor(nameof(ScanController.Explicit), BindingSource.Body),
            ActionDescriptor(nameof(ScanController.Duplicate), BindingSource.Body));

        var context = new ScanningContext();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => module.ExecuteAsync(context, default));

        // Assert
        Assert.Null(context.Endpoints);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task ExecuteAsync_WhenBodyCountIsInvalid_RejectsAction(int count)
    {
        // Arrange
        var module = Discovery(ActionDescriptor(nameof(ScanController.Explicit),
            count == 0 ? BindingSource.Query : BindingSource.Body,
            count == 2 ? BindingSource.Body : BindingSource.Query));

        var context = new ScanningContext();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => module.ExecuteAsync(context, default));

        // Assert
        Assert.Null(context.Endpoints);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoEndpointsExist_CommitsEmptyCollection()
    {
        // Arrange
        var module = Discovery(
            ActionDescriptor(nameof(ScanController.Ordinary)));

        var context = new ScanningContext();

        // Act
        await module.ExecuteAsync(context, default);

        // Assert
        Assert.NotNull(context.Endpoints);
        Assert.Empty(context.Endpoints);
    }
    
    [Fact]
    public async Task ExecuteAsync_WhenBindingInfoIsMissing_UsesMethodAndBody()
    {
        // Arrange
        var descriptor = ActionDescriptor(
            nameof(ScanController.Explicit), BindingSource.Body, BindingSource.Query);

        descriptor.Parameters[1].BindingInfo = null;

        var module = Discovery(descriptor);
        var context = new ScanningContext();

        // Act
        await module.ExecuteAsync(context, default);

        // Assert
        Assert.Single(context.Endpoints!);
        Assert.Equal(nameof(ScanController.Explicit), context.CurrentItem);
    }
}