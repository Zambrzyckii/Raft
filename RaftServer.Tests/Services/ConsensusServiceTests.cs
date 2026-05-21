using Raft.Models;
using Raft.Services;
using Raft.Grpc;

namespace RaftServer.Tests.Services;

public class ConsensusServiceTests
{
    [Fact]
    public async Task RequestVote_WhenTermIsLower_FalseAndDeny()
    {
        var node = new RaftNode { currentTerm = 5 };
        var service = new ConsensusService(node);
        var request = new VoteRequest { Term = 4, Candidate = "5123" };
        var response = await service.RequestVote(request, null!);

        Assert.False(response.VoteGranted);
        Assert.Equal(5, node.currentTerm);
    }

    [Fact]
    public async Task RequestVote_WhenAlreadyVoted_Deny()
    {
        var node = new RaftNode { currentTerm = 5, votedFor = "5001" };
        var servcie = new ConsensusService(node);
        var request = new VoteRequest { Term = 5, Candidate = "5002" };
        var response = await servcie.RequestVote(request, null!);

        Assert.False(response.VoteGranted);
        Assert.Equal("5001", node.votedFor);
    }

    [Fact]
    public async Task AppendEntries_ValidHearthbeat_UpdateLeaderAndRole()
    {
        var node = new RaftNode { currentTerm = 2, _currentState = Role.Candidate };
        var service = new ConsensusService(node);
        var request = new AppendRequest { LeaderTerm = 3, LeaderAddress = "5002", Number = 0 };
        await service.AppendEntries(request, null!);

        Assert.Equal(Role.Follower, node._currentState);
        Assert.Equal("5002", node.currentLeader);
        Assert.Equal(3, node.currentTerm);
    }

    [Fact]
    public async Task AppendEntries_NumberGreaterThanZero_AddToPrimes()
    {
        var node = new RaftNode {currentTerm = 1, OfficialPrimes = new List<LogEntry> {new(0, 2), new(1, 3)}};
        var service = new ConsensusService(node);
        var request = new AppendRequest { LeaderTerm = 1, Number = 5 };
        await service.AppendEntries(request, null!);

        Assert.True(node.OfficialPrimes.Exists(p => p.PrimeNumber == 5));
        Assert.Equal(3, node.OfficialPrimes.Count);
    }

    [Fact]
    public async Task ProposeNumber_WhenNotLeader_Ignore()
    {
        var node = new RaftNode { _currentState = Role.Follower, pendingNumber = new Queue<ProposeRequest>() };
        var service = new ConsensusService(node);
        var request = new ProposeRequest { Number = 7 };
        await service.ProposeNumber(request, null!);
        Assert.Empty(node.pendingNumber);
    }

    [Fact]
    public async Task ProposeNumber_WhenLeaderAndNewNumber_Enqueue()
    {
        var node = new RaftNode { _currentState = Role.Leader, pendingNumber = new Queue<ProposeRequest>() };
        var service = new ConsensusService(node);

        var request = new ProposeRequest { Number = 11 };

        await service.ProposeNumber(request, null!);

        Assert.Single(node.pendingNumber);
        Assert.Equal(11,node.pendingNumber.Peek().Number);
    }
}