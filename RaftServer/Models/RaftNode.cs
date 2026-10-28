using Raft.Grpc;

namespace Raft.Models;

public class RaftNode
{
    public Role _currentState { get; set; } = Role.Follower;
    public int currentTerm { get; set; } = 0;
    public string? id { get; set; }
    public CancellationTokenSource? _cancellationToken;

    public string? votedFor { get; set; } = string.Empty;
    public List<string?> otherCandidates { get; set; } = new() { "http://localhost:5001", "http://localhost:5002" };
    public string? currentLeader { get; set; }
    public bool paused { get; set; }
    public bool waitingForStart { get; set; }
    public int workDelay { get; set; } = 50;

    public List<NodeEvent> events { get; } = new();
    public long lastEventSeq { get; set; }

    public List<LogEntry> OfficialPrimes { get; set; } = new();

    public Queue<ProposeRequest> pendingNumber { get; set; } = new();

    public void CancelTokenTimeout()
    {
        _cancellationToken?.Cancel();
        _cancellationToken = new CancellationTokenSource();
    }

    public void AddEvent(string kind, int value = 0, string? peer = null)
    {
        lock (events)
        {
            lastEventSeq++;
            events.Add(new NodeEvent(lastEventSeq, kind, value, peer, currentTerm));
            if (events.Count > 200)
            {
                events.RemoveAt(0);
            }
        }
    }
}

public record LogEntry(int Term, int PrimeNumber, string? FoundBy = null, string? SearchTime = null);

public record NodeEvent(long Seq, string Kind, int Value, string? Peer, int Term);