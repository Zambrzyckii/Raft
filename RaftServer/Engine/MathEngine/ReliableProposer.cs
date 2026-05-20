using Grpc.Net.Client;
using Raft.Grpc;
using Raft.Models;

namespace Raft.Engine.MathEngine;

public static class ReliableProposer
{
    public static async Task SendAndWaitForAckAsync(RaftNode context, int primeNumber, string searchTime, CancellationToken token)
    {
        bool isCommitted = false;
        var channel = GrpcChannel.ForAddress(context.currentLeader!);
        var client = new RaftConsensus.RaftConsensusClient(channel);
        
        var package = new ProposeRequest
        {
            Number = primeNumber, 
            LeaderAddress = context.currentLeader,
            MyAddress = context.id, 
            SearchTime = searchTime
        };

        while (!isCommitted && !token.IsCancellationRequested)
        {
            if (!string.IsNullOrEmpty(context.currentLeader))
            {
                try
                {
                    await client.ProposeNumberAsync(package);
                }
                catch {}
            }

            await Task.Delay(200, token);
            
            lock (context.OfficialPrimes)
            {
                if (context.OfficialPrimes.Contains(primeNumber)) 
                {
                    isCommitted = true;
                }
            }
        }
    }
}