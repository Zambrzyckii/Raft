using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Raft.Grpc;
using RaftDashboard.Hubs;
using RaftDashboard.Models;

namespace RaftDashboard.Services;

public class ClusterPoller : BackgroundService
{
    private readonly NodeClients _clients;
    private readonly IHubContext<ClusterHub> _hub;
    private readonly ClusterSpeed _speed;
    private readonly ConcurrentDictionary<string, long> _cursors = new();

    public ClusterPoller(NodeClients clients, IHubContext<ClusterHub> hub, ClusterSpeed speed)
    {
        _clients = clients;
        _hub = hub;
        _speed = speed;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var statuses = await Task.WhenAll(
                _clients.Addresses.Select((address, i) => GetStatusSafe(address, i + 1)));
            var snapshot = new ClusterSnapshot(DateTime.UtcNow, statuses.ToList(), _speed.DelayMs);
            await _hub.Clients.All.SendAsync("ClusterState", snapshot, stoppingToken);
        }
    }

    private async Task<NodeStatus> GetStatusSafe(string address, int index)
    {
        try
        {
            long sinceSeq = _cursors.GetValueOrDefault(address);
            var response = await _clients.For(address).GetStatusAsync(
                new StatusRequest { SinceSeq = sinceSeq }, deadline: DateTime.UtcNow.AddSeconds(1));
            _cursors[address] = response.LastSeq;

            if (response.WorkDelay != _speed.DelayMs)
            {
                await SetSpeedSafe(address);
            }

            var events = response.Events
                .Select(e => new Models.NodeEvent(e.Seq, e.Kind, e.Value, e.Peer, e.Term))
                .ToList();
            return new NodeStatus(index, address, true, response.Paused, response.Id, response.Role,
                response.CurrentTerm, response.CurrentLeader, response.LogCount, response.PendingCount,
                response.WorkDelay, events);
        }
        catch (Exception)
        {
            return new NodeStatus(index, address, false, false, string.Empty, string.Empty, 0,
                string.Empty, 0, 0, 0, new List<Models.NodeEvent>());
        }
    }

    private async Task SetSpeedSafe(string address)
    {
        try
        {
            await _clients.For(address).SetSpeedAsync(
                new SetSpeedRequest { DelayMs = _speed.DelayMs }, deadline: DateTime.UtcNow.AddSeconds(1));
        }
        catch (Exception)
        {
        }
    }
}
