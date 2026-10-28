using Grpc.Net.Client;
using Microsoft.AspNetCore.SignalR;
using Raft.Grpc;
using RaftDashboard.Hubs;
using RaftDashboard.Models;

namespace RaftDashboard.Services;

public class ClusterPoller : BackgroundService
{
    private readonly List<string> _nodeAddresses;
    private readonly IHubContext<ClusterHub> _hub;
    private readonly Dictionary<string, RaftMonitoring.RaftMonitoringClient> _clients = new();

    public ClusterPoller(List<string> nodeAddresses, IHubContext<ClusterHub> hub)
    {
        _nodeAddresses = nodeAddresses;
        _hub = hub;
        foreach (var address in nodeAddresses)
        {
            _clients[address] = new RaftMonitoring.RaftMonitoringClient(GrpcChannel.ForAddress(address));
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var statuses = await Task.WhenAll(_nodeAddresses.Select(GetStatusSafe));
            var snapshot = new ClusterSnapshot(DateTime.UtcNow, statuses.ToList());
            await _hub.Clients.All.SendAsync("ClusterState", snapshot, stoppingToken);
        }
    }

    private async Task<NodeStatus> GetStatusSafe(string address)
    {
        try
        {
            var response = await _clients[address].GetStatusAsync(
                new StatusRequest(), deadline: DateTime.UtcNow.AddSeconds(1));
            return new NodeStatus(address, true, response.Id, response.Role,
                response.CurrentTerm, response.CurrentLeader, response.LogCount, response.PendingCount);
        }
        catch (Exception)
        {
            return new NodeStatus(address, false, string.Empty, string.Empty, 0, string.Empty, 0, 0);
        }
    }
}
