using Raft.Interfaces;
using Raft.Models;
using Grpc.Net.Client;
using Raft.Grpc;
using Raft.Services;

namespace Raft.RoleBehaviours.Leader;

public class Leader : IRaftState
{
    private static FileStorageService _fileStorageService = new();
    private readonly Dictionary<string, GrpcChannel> _channelCache = new();
    public async Task Execute(RaftNode _context)
    {
        ProposeRequest? currentProposal = null;

        lock (_context.pendingNumber)
        {
            if (_context.pendingNumber.Count > 0)
            {
                currentProposal = _context.pendingNumber.Peek();
            }
        }

        if (currentProposal != null)
        {
            if (_context.OfficialPrimes.Contains(currentProposal.Number))
            {
                lock (_context.pendingNumber)
                {
                    _context.pendingNumber.Dequeue();
                }
                return; 
            }
            int numberToVote = currentProposal.Number;

            AppendResponse[] theResults = await ProposeNumbers.SendProposeNumbers(_context, numberToVote, _channelCache);

            int sendAgreed = 1;
            foreach (var response in theResults)
            {
                if (response.IsDone) sendAgreed++;
            }

            await VoteCounter.VoteResultsMethod(_context, numberToVote, sendAgreed, currentProposal, _fileStorageService);

            lock (_context.pendingNumber)
            {
                if (_context.pendingNumber.Count > 0)
                {
                    _context.pendingNumber.Dequeue();
                }
            }
        }
        else
        {
            List<Task<AppendResponse>> heartbeats = new();

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
                        Number = 0,
                        LeaderAddress = _context.id
                    };
                    heartbeats.Add(GetSignal.GetSignalSafe(client, package)!);
                }
            }

            await Task.WhenAll(heartbeats);
        }

        await Task.Delay(50);
    }
}