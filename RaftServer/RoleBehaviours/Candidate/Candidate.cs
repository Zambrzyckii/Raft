using Raft.Interfaces;
using Raft.Models;
using Grpc.Net.Client;
using Raft.Grpc;
namespace Raft.RoleBehaviours;

public class Candidate : IRaftState
{
    public async Task Execute(RaftNode _context)
    {
        
        Console.WriteLine($"[{_context.id}] I'm: {_context._currentState} and I start campain, my term is: {_context.currentTerm}");
        _context.votedFor = _context.id;
        _context.currentTerm++;

        VoteResponse[] responses = await FarmVoteResponses(_context);

        int grantedVotes = 1;

        foreach (var response in responses)
        {
            if (response.VoteGranted == true) grantedVotes++;
        }

        VoteResults(_context, grantedVotes);
    }
    private async Task<VoteResponse?> GetVoteSafe(RaftConsensus.RaftConsensusClient client, VoteRequest package)
    {
        try
        {
            return await client.RequestVoteAsync(package);
        }
        catch
        {
            return null;
        }
    }

    private async Task<VoteResponse[]> FarmVoteResponses(RaftNode _context)
    {
        List<Task<VoteResponse>> theVote = new();
        foreach (var candidate in _context.otherCandidates)
        {
            if (candidate != null)
            {
                var result = GrpcChannel.ForAddress(candidate);
                var client = new RaftConsensus.RaftConsensusClient(result);
                var package = new VoteRequest { Term = _context.currentTerm, Candidate = _context.id };
                theVote.Add(GetVoteSafe(client, package)!);
            }
        }

        return await Task.WhenAll(theVote);
    }
    private void VoteResults(RaftNode _context, int grantedVotes)
    {
        int allCandidates = _context.otherCandidates.Count + 1;
        int toWin = allCandidates / 2;
        toWin++;

        if (grantedVotes >= toWin)
        {
            Console.WriteLine($"[{_context.id}] I won, I've got {grantedVotes}/{allCandidates} votes");
            _context._currentState = Role.Leader;
        }
        else
        {
            Console.WriteLine($"[{_context.id}] I lost, I've got {grantedVotes} votes");
            _context._currentState = Role.Follower;
        }
    }
}