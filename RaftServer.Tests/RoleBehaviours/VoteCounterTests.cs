using Raft.Models;
using Raft.RoleBehaviours.Leader;

namespace RaftServer.Tests.RoleBehaviours;

public class VoteCounterTests
{
    [Fact]
    public async Task VoteResultsMethod_WhenMajority_StampsCurrentTerm()
    {
        var node = new RaftNode { currentTerm = 4 };

        await VoteCounter.VoteResultsMethod(node, 11, 2);

        var entry = Assert.Single(node.OfficialPrimes);
        Assert.Equal(11, entry.PrimeNumber);
        Assert.Equal(4, entry.Term);
    }
}
