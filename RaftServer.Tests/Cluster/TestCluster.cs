using System.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Raft.Hosting;
using Raft.Models;

namespace RaftServer.Tests.Cluster;

[CollectionDefinition("Cluster")]
public class ClusterCollection
{
}

public sealed class TestCluster : IAsyncDisposable
{
    private readonly List<(RaftNode Node, WebApplication App)> _nodes = new();
    private readonly HashSet<int> _stopped = new();

    public static async Task<TestCluster> StartAsync(int basePort, int nodesCount)
    {
        List<string> allAdress = new();
        for (int i = 1; i <= nodesCount; i++)
        {
            allAdress.Add($"http://localhost:{basePort + i}");
        }

        var cluster = new TestCluster();
        foreach (var adress in allAdress)
        {
            var app = NodeHost.Build(adress, allAdress, Array.Empty<string>());
            cluster._nodes.Add((app.Services.GetRequiredService<RaftNode>(), app));
        }

        for (int i = 0; i < cluster._nodes.Count; i++)
        {
            try
            {
                await cluster._nodes[i].App.StartAsync();
            }
            catch (Exception exception)
            {
                await cluster.DisposeAsync();
                throw new InvalidOperationException(
                    $"Node {allAdress[i]} failed to start, its port may be taken by another process", exception);
            }
        }

        return cluster;
    }

    public RaftNode Node(int index) => _nodes[index].Node;

    public async Task StopNodeAsync(int index)
    {
        _stopped.Add(index);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try
        {
            await _nodes[index].App.StopAsync(timeout.Token);
        }
        catch
        {
            // in-flight consensus calls may throw while the node is torn down
        }
    }

    public int LogCount(int index)
    {
        var node = _nodes[index].Node;
        lock (node.OfficialPrimes)
        {
            return node.OfficialPrimes.Count;
        }
    }

    public int? SingleLeaderIndex()
    {
        List<int> leaders = new();
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (!_stopped.Contains(i) && _nodes[i].Node._currentState == Role.Leader)
            {
                leaders.Add(i);
            }
        }

        return leaders.Count == 1 ? leaders[0] : null;
    }

    public async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string description)
    {
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"Condition not met within {timeout.TotalSeconds}s: {description}");
    }

    public async ValueTask DisposeAsync()
    {
        for (int i = 0; i < _nodes.Count; i++)
        {
            if (!_stopped.Contains(i))
            {
                await StopNodeAsync(i);
            }
        }
    }
}
