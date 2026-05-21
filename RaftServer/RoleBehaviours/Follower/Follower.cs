using Raft.Interfaces;
using Raft.Models;

namespace Raft.RoleBehaviours;

public class Follower() : IRaftState
{
    public async Task Execute(RaftNode _context)
    {
        _context._cancellationToken = new CancellationTokenSource();
        int randomAwaitTime = Random.Shared.Next(2000, 4000);

        try
        {
            await Task.Delay(randomAwaitTime, _context._cancellationToken.Token);

            Console.WriteLine($"[{_context.id}] Timeout leader is dead");
            _context._currentState = Role.Candidate;
        }
        catch (TaskCanceledException)
        {
        }
    }
}