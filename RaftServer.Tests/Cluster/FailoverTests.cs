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

    [Fact]
    public async Task Failover_WhenLeaderStops_ElectsNewLeader()
    {
        await using var cluster = await TestCluster.StartAsync(7200, 3);
        await cluster.WaitUntilAsync(() => cluster.SingleLeaderIndex() is not null,
            TimeSpan.FromSeconds(20), "three fresh nodes elect a single leader");

        int firstLeader = cluster.SingleLeaderIndex()!.Value;
        await cluster.StopNodeAsync(firstLeader);

        await cluster.WaitUntilAsync(() => cluster.SingleLeaderIndex() is not null,
            TimeSpan.FromSeconds(30), "surviving nodes elect a new leader after the old one stopped");
    }

    [Fact]
    public async Task Failover_WhenFollowerStops_KeepsCommitting()
    {
        await using var cluster = await TestCluster.StartAsync(7300, 3);
        await cluster.WaitUntilAsync(() => cluster.SingleLeaderIndex() is not null,
            TimeSpan.FromSeconds(20), "three fresh nodes elect a single leader");

        int leader = cluster.SingleLeaderIndex()!.Value;
        int follower = leader == 0 ? 1 : 0;
        await cluster.StopNodeAsync(follower);

        int logBefore = cluster.LogCount(leader);
        await cluster.WaitUntilAsync(() => cluster.LogCount(leader) >= logBefore + 3,
            TimeSpan.FromSeconds(30), "leader keeps committing new numbers after a follower stopped");
    }
}
