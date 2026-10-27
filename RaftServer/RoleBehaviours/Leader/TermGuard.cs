using Raft.Grpc;
using Raft.Models;

namespace Raft.RoleBehaviours.Leader;

public static class TermGuard
{
    public static void StepDownIfBehind(RaftNode _context, AppendResponse?[] responses)
    {
        foreach (var response in responses)
        {
            if (response != null && response.Term > _context.currentTerm)
            {
                Console.WriteLine($"[{_context.id}] I step down, term {response.Term} in ack is newer than mine {_context.currentTerm}");
                _context.currentTerm = response.Term;
                _context.votedFor = string.Empty;
                _context._currentState = Role.Follower;
            }
        }
    }
}
