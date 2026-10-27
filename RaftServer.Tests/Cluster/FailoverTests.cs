namespace RaftServer.Tests.Cluster;

[Trait("Category", "Integration")]
[Collection("Cluster")]
public class FailoverTests
{
    [Fact]
    public async Task Election_WithThreeNodes_ElectsSingleLeader()
    {
        await using var cluster = await TestCluster.StartAsync(7100, 3);

        await cluster.WaitUntilAsync(() => cluster.SingleLeaderIndex() is not null,
            TimeSpan.FromSeconds(20), "three fresh nodes elect a single leader");
    }
}
