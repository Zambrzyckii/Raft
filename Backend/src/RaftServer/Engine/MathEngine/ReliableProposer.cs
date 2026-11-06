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
        context.AddEvent("propose", primeNumber, context.currentLeader);

        while (!isCommitted && !token.IsCancellationRequested && !context.paused)
        {
            var currentLeader = context.currentLeader;
            if (!string.IsNullOrEmpty(currentLeader))
            {
                var channel = _channels.GetOrAdd(
                    currentLeader,
                    address => GrpcChannel.ForAddress(address)
                );
                var client = new RaftConsensus.RaftConsensusClient(channel);

                var package = new ProposeRequest
                {
                    Number = primeNumber,
                    LeaderAddress = currentLeader,
                    MyAddress = context.id,
                    SearchTime = searchTime
                };

                try
                {
                   await client.ProposeNumberAsync(package);
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