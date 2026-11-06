using Raft.Engine.MathEngine;
using Raft.Models;

namespace RaftServer.Tests.Engine.MathEngine;

public class DynamicWorkDividerTest
{
    [Fact]
    public void IsMyTurn_WhenImTheFollower_True()
    {
        var dummyNode = new RaftNode
            { id = "5002", currentLeader = "5001", otherCandidates = new List<string?> { "5001" } };
        int numberToCheck = 1;
        bool isMyTurn = DynamicWorkDivider.IsMyTurn(dummyNode, numberToCheck);
        Assert.True(isMyTurn);
    }

    [Fact]
    public void IsMyTurn_WhemImTheLeader_False()
    {
        var dummyNode = new RaftNode
        {
            id = "5001", 
            currentLeader = "5001", 
            otherCandidates = new List<string?> { "5002", "5003" }
        };
        int numberToCheck = 2;
        bool isMyTurn = DynamicWorkDivider.IsMyTurn(dummyNode, numberToCheck);
        Assert.False(isMyTurn);
    }

    [Fact]
    public void IsMyTurn_WhenNumberBelongsToOtherFollower_False()
    {
        var dummyNode = new RaftNode
        {
            id = "5002", 
            currentLeader = "5001",
            otherCandidates = new List<string?> { "5001", "5003" }
        };
        int numberToCheck = 3; // this num is for 5003
        bool isMyTurn = DynamicWorkDivider.IsMyTurn(dummyNode, numberToCheck);
        Assert.False(isMyTurn);
    }
}