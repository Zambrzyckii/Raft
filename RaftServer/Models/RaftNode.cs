using Raft.Interfaces;
using Raft.RoleBehaviours;

namespace Raft.Models;

public class RaftNode
{
    private IRaftState _currentState;
    public int currentTerm { get; set; } = 0;
    public string id { get; set; }
    public CancellationTokenSource _cancellationToken;

    
    public RaftNode(IRaftState state)
    {
        _currentState = new Follower(_cancellationToken);
    }

    public void cancelTokenTimeout()
    {
        _cancellationToken?.Cancel();
        _cancellationToken = new CancellationTokenSource();
    }

    public CancellationTokenSource GetCurrentToken() => _cancellationToken;
    
    public void NewState(IRaftState newState)
    {
        _currentState = newState;
    }

    public async Task StartNode()
    {
        while (true)
        {
            await _currentState.Execute(this);
        }
    }
}