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

        var response = new StatusResponse
        {
            Id = _node.id ?? string.Empty,
            Role = _node._currentState.ToString(),
            CurrentTerm = _node.currentTerm,
            CurrentLeader = _node.currentLeader ?? string.Empty,
            LogCount = logCount,
            PendingCount = pendingCount,
            Paused = _node.paused,
            WorkDelay = _node.workDelay,
            WaitingForStart = _node.waitingForStart
        };

        lock (_node.events)
        {
            foreach (var nodeEvent in _node.events)
            {
                if (nodeEvent.Seq > request.SinceSeq)
                {
                    response.Events.Add(new Raft.Grpc.NodeEvent
                    {
                        Seq = nodeEvent.Seq,
                        Kind = nodeEvent.Kind,
                        Value = nodeEvent.Value,
                        Peer = nodeEvent.Peer ?? string.Empty,
                        Term = nodeEvent.Term
                    });
                }
            }
            response.LastSeq = _node.lastEventSeq;
        }

        return Task.FromResult(response);
    }

    public override Task<SetPausedResponse> SetPaused(SetPausedRequest request, ServerCallContext context)
    {
        _node.paused = request.Paused;
        _node.AddEvent(request.Paused ? "paused" : "resumed");
        Console.WriteLine($"[{_node.id}] {(request.Paused ? "Paused" : "Resumed")} from the dashboard");
        return Task.FromResult(new SetPausedResponse { Paused = _node.paused });
    }

    public override Task<SetSpeedResponse> SetSpeed(SetSpeedRequest request, ServerCallContext context)
    {
        _node.workDelay = Math.Clamp(request.DelayMs, 10, 3000);
        return Task.FromResult(new SetSpeedResponse { DelayMs = _node.workDelay });
    }

    public override Task<StartSearchResponse> StartSearch(StartSearchRequest request, ServerCallContext context)
    {
        if (_node.waitingForStart)
        {
            _node.waitingForStart = false;
            Console.WriteLine($"[{_node.id}] Search started from the dashboard");
        }
        return Task.FromResult(new StartSearchResponse { WaitingForStart = _node.waitingForStart });
    }
}
