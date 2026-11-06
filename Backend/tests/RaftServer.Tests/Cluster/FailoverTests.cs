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
        int? firstLeader = null;
        await cluster.WaitUntilAsync(() => (firstLeader = cluster.SingleLeaderIndex()) is not null,
            TimeSpan.FromSeconds(20), "three fresh nodes elect a single leader");

        await cluster.StopNodeAsync(firstLeader!.Value);

        await cluster.WaitUntilAsync(() => cluster.SingleLeaderIndex() is not null,
            TimeSpan.FromSeconds(30), "surviving nodes elect a new leader after the old one stopped");
    }

    [Fact]
    public async Task Failover_WhenFollowerStops_KeepsCommitting()
    {
        await using var cluster = await TestCluster.StartAsync(7300, 3);
        int? leaderIndex = null;
        await cluster.WaitUntilAsync(() => (leaderIndex = cluster.SingleLeaderIndex()) is not null,
            TimeSpan.FromSeconds(20), "three fresh nodes elect a single leader");

        int leader = leaderIndex!.Value;
        int follower = leader == 0 ? 1 : 0;
        await cluster.StopNodeAsync(follower);

        int logBefore = cluster.LogCount(leader);
        await cluster.WaitUntilAsync(() => cluster.LogCount(leader) >= logBefore + 3,
            TimeSpan.FromSeconds(30), "leader keeps committing new numbers after a follower stopped");
    }

    // Four nodes, not three: work shards follow the static peer list (DynamicWorkDivider.cs),
    // so with two survivors the dead node can own every odd number and starve the test of primes.
    [Fact]
    public async Task Failover_WhenLeaderStops_ResumesCommitting()
    {
        await using var cluster = await TestCluster.StartAsync(7400, 4);
        int? firstLeader = null;
        await cluster.WaitUntilAsync(() => (firstLeader = cluster.SingleLeaderIndex()) is not null,
            TimeSpan.FromSeconds(20), "four fresh nodes elect a single leader");

        await cluster.StopNodeAsync(firstLeader!.Value);

        int? newLeaderIndex = null;
        await cluster.WaitUntilAsync(() => (newLeaderIndex = cluster.SingleLeaderIndex()) is not null,
            TimeSpan.FromSeconds(30), "surviving nodes elect a new leader after the old one stopped");

        int newLeader = newLeaderIndex!.Value;
        int logBefore = cluster.LogCount(newLeader);
        await cluster.WaitUntilAsync(() => cluster.LogCount(newLeader) >= logBefore + 3,
            TimeSpan.FromSeconds(30), "new leader commits numbers proposed by the surviving follower");
    }
}
