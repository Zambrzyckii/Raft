using Raft.Grpc;
using Raft.Models;
using Raft.RoleBehaviours.Leader;

namespace RaftServer.Tests.RoleBehaviours;

public class TermGuardTests
{
    [Fact]
    public void StepDownIfBehind_WhenAckHasNewerTerm_BecomesFollower()
    {
        var node = new RaftNode { _currentState = Role.Leader, currentTerm = 2, votedFor = "5001" };
        var responses = new AppendResponse?[] { null, new AppendResponse { IsDone = false, Term = 5 } };

        TermGuard.StepDownIfBehind(node, responses);

        Assert.Equal(Role.Follower, node._currentState);
        Assert.Equal(5, node.currentTerm);
        Assert.Equal(string.Empty, node.votedFor);
    }

    [Fact]
    public void StepDownIfBehind_WhenAcksAtOwnTerm_StaysLeader()
    {
        var node = new RaftNode { _currentState = Role.Leader, currentTerm = 2 };
        var responses = new AppendResponse?[] { new AppendResponse { IsDone = true, Term = 2 }, null };

        TermGuard.StepDownIfBehind(node, responses);

        Assert.Equal(Role.Leader, node._currentState);
        Assert.Equal(2, node.currentTerm);
    }
}
