using Raft.Interfaces;
using Raft.Models;
using Raft.Services;

namespace Raft.RoleBehaviours;

public class Follower(CancellationTokenSource _cancellationToken) : IRaftState
{
    public async Task Execute(RaftNode _context)
    {
        
    }
}