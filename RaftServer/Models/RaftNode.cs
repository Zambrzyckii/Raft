using Raft.Grpc;
using Raft.Interfaces;
using Raft.RoleBehaviours;

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

    public List<int> OfficialPrimes { get; set; } = new();

    public Queue<ProposeRequest> pendingNumber { get; set; } = new ();
    public void CancelTokenTimeout()
    {
        _cancellationToken?.Cancel();
        _cancellationToken = new CancellationTokenSource();
    }
    
    

}