using Raft.Models;

namespace Raft.Engine.MathEngine;

public static class DynamicWorkDivider
{
    public static bool IsMyTurn(RaftNode context, int currentNumber)
    {
        var activeWorkers = context.otherCandidates.ToList();
        activeWorkers.Add(context.id);
        activeWorkers.Sort();
        
        if (!string.IsNullOrEmpty(context.currentLeader))
        {
            activeWorkers.Remove(context.currentLeader);
        }
        
        int totalFollowers = activeWorkers.Count;
        int myDynamicIndex = activeWorkers.IndexOf(context.id);
        
        return currentNumber % totalFollowers == myDynamicIndex;
    }
}