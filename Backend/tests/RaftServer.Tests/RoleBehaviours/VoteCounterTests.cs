using Raft.Grpc;
using Raft.Models;
using Raft.RoleBehaviours.Leader;

namespace RaftServer.Tests.RoleBehaviours;

public class VoteCounterTests
{
    [Fact]
    public async Task VoteResultsMethod_WhenMajority_StampsCurrentTerm()
    {
        var node = new RaftNode { currentTerm = 4 };
        var proposal = new ProposeRequest { Number = 11, MyAddress = "5003", SearchTime = "0.7" };

        await VoteCounter.VoteResultsMethod(node, 11, 2, proposal);

        var entry = Assert.Single(node.OfficialPrimes);
        Assert.Equal(11, entry.PrimeNumber);
        Assert.Equal(4, entry.Term);
        Assert.Equal("5003", entry.FoundBy);
        Assert.Equal("0.7", entry.SearchTime);
    }
}
