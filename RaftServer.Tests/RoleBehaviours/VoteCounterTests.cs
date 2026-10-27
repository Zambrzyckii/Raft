using Raft.Grpc;
using Raft.Models;
using Raft.RoleBehaviours.Leader;
using Raft.Services;

namespace RaftServer.Tests.RoleBehaviours;

public class VoteCounterTests
{
    [Fact]
    public async Task VoteResultsMethod_WhenMajority_StampsCurrentTerm()
    {
        var node = new RaftNode { currentTerm = 4, currentLeaderTerm = 2 };
        var proposal = new ProposeRequest { Number = 11, MyAddress = "5003", SearchTime = "0" };

        await VoteCounter.VoteResultsMethod(node, 11, 2, proposal, new FileStorageService());

        var entry = Assert.Single(node.OfficialPrimes);
        Assert.Equal(11, entry.PrimeNumber);
        Assert.Equal(4, entry.Term);
    }
}
