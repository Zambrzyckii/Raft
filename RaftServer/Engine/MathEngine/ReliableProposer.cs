using System.Collections.Concurrent;
using Grpc.Net.Client;
using Raft.Grpc;
using Raft.Models;

namespace Raft.Engine.MathEngine;

public static class ReliableProposer
{
    private static readonly ConcurrentDictionary<string, GrpcChannel> _channels = new();
    public static async Task SendAndWaitForAckAsync(RaftNode context, int primeNumber, string searchTime, CancellationToken token)
    {
        bool isCommitted = false;
        var channel = _channels.GetOrAdd(
            context.currentLeader!, 
            address => GrpcChannel.ForAddress(address)
        );
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
                   var result=  await client.ProposeNumberAsync(package);
                   context.currentLeaderTerm = result.LeaderTerm;
                }
                catch {}
            }

            await Task.Delay(200, token);
            
            lock (context.OfficialPrimes)
            {
                if (context.OfficialPrimes.Exists(p => p.PrimeNumber == primeNumber)) 
                {
                    isCommitted = true;
                }
            }
        }
    }
}