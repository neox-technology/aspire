using Xunit;

namespace Neox.Aspire.Hosting.Auth.Tests;

public class AuthStatusAggregatorTests
{
    [Fact]
    public void WorstWins_Empty_ReturnsWhenEmpty()
    {
        Assert.Equal(
            AuthDashboardStatus.Healthy,
            AuthStatusAggregator.WorstWins([]));
        Assert.Equal(
            AuthDashboardStatus.Waiting,
            AuthStatusAggregator.WorstWins([], whenEmpty: AuthDashboardStatus.Waiting));
    }

    [Fact]
    public void WorstWins_UnhealthyBeatsWaitingAndHealthy()
    {
        Assert.Equal(
            AuthDashboardStatus.Unhealthy,
            AuthStatusAggregator.WorstWins(
            [
                AuthDashboardStatus.Healthy,
                AuthDashboardStatus.Waiting,
                AuthDashboardStatus.Unhealthy
            ]));
    }

    [Fact]
    public void WorstWins_WaitingBeatsHealthy()
    {
        Assert.Equal(
            AuthDashboardStatus.Waiting,
            AuthStatusAggregator.WorstWins(
            [
                AuthDashboardStatus.Healthy,
                AuthDashboardStatus.Waiting,
                AuthDashboardStatus.Healthy
            ]));
    }

    [Fact]
    public void WorstWins_AllHealthy_ReturnsHealthy()
    {
        Assert.Equal(
            AuthDashboardStatus.Healthy,
            AuthStatusAggregator.WorstWins(
            [
                AuthDashboardStatus.Healthy,
                AuthDashboardStatus.Healthy
            ]));
    }
}
