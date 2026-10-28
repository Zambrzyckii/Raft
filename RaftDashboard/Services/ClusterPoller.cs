using Microsoft.AspNetCore.SignalR;
using Raft.Grpc;
using RaftDashboard.Hubs;
using RaftDashboard.Models;

namespace RaftDashboard.Services;

public class ClusterPoller : BackgroundService
{
    private readonly NodeClients _clients;
    private readonly IHubContext<ClusterHub> _hub;

    public ClusterPoller(NodeClients clients, IHubContext<ClusterHub> hub)
    {
        _clients = clients;
        _hub = hub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var statuses = await Task.WhenAll(
                _clients.Addresses.Select((address, i) => GetStatusSafe(address, i + 1)));
            var snapshot = new ClusterSnapshot(DateTime.UtcNow, statuses.ToList());
            await _hub.Clients.All.SendAsync("ClusterState", snapshot, stoppingToken);
        }
    }

    private async Task<NodeStatus> GetStatusSafe(string address, int index)
    {
        try
        {
            var response = await _clients.For(address).GetStatusAsync(
                new StatusRequest(), deadline: DateTime.UtcNow.AddSeconds(1));
            return new NodeStatus(index, address, true, response.Paused, response.Id, response.Role,
                response.CurrentTerm, response.CurrentLeader, response.LogCount, response.PendingCount);
        }
        catch (Exception)
        {
            return new NodeStatus(index, address, false, false, string.Empty, string.Empty, 0, string.Empty, 0, 0);
        }
    }
}
