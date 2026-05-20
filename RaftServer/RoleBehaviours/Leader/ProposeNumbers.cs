using Grpc.Net.Client;
using Raft.Grpc;
using Raft.Models;

namespace Raft.RoleBehaviours.Leader;

public static class ProposeNumbers
{
    public static async Task<AppendResponse[]> SendProposeNumbers(RaftNode _context, int numberToVote, Dictionary<string,GrpcChannel> _channelCache)
    {
        List<Task<AppendResponse>> toSend = new();

        foreach (var follower in _context.otherCandidates)
        {
            if (follower != null)
            {
                if (!_channelCache.ContainsKey(follower))
                {
                    _channelCache[follower] = GrpcChannel.ForAddress(follower);
                }
                var activeChannel = _channelCache[follower]; 
                var client = new RaftConsensus.RaftConsensusClient(activeChannel);
                var package = new AppendRequest
                {
                    LeaderTerm = _context.currentTerm,
                    Number = numberToVote,
                    LeaderAddress = _context.id
                };
                toSend.Add(GetSignal.GetSignalSafe(client, package)!);
            }
        }

        return await Task.WhenAll(toSend);
    }
}