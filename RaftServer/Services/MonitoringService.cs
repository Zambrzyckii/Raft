using Grpc.Core;
using Raft.Grpc;
using Raft.Models;

namespace Raft.Services;

public class MonitoringService : RaftMonitoring.RaftMonitoringBase
{
    private readonly RaftNode _node;

    public MonitoringService(RaftNode node)
    {
        _node = node;
    }

    public override Task<StatusResponse> GetStatus(StatusRequest request, ServerCallContext context)
    {
        int logCount;
        lock (_node.OfficialPrimes)
        {
            logCount = _node.OfficialPrimes.Count;
        }
        int pendingCount;
        lock (_node.pendingNumber)
        {
            pendingCount = _node.pendingNumber.Count;
        }

        return Task.FromResult(new StatusResponse
        {
            Id = _node.id ?? string.Empty,
            Role = _node._currentState.ToString(),
            CurrentTerm = _node.currentTerm,
            CurrentLeader = _node.currentLeader ?? string.Empty,
            LogCount = logCount,
            PendingCount = pendingCount,
            Paused = _node.paused
        });
    }

    public override Task<SetPausedResponse> SetPaused(SetPausedRequest request, ServerCallContext context)
    {
        _node.paused = request.Paused;
        Console.WriteLine($"[{_node.id}] {(request.Paused ? "Paused" : "Resumed")} from the dashboard");
        return Task.FromResult(new SetPausedResponse { Paused = _node.paused });
    }
}
