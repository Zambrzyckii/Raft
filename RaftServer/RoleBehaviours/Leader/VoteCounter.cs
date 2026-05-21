using Raft.Grpc;
using Raft.Models;
using Raft.Services;

namespace Raft.RoleBehaviours.Leader;

public static class VoteCounter
{
    public static async Task VoteResultsMethod(RaftNode _context, int numberToVote, int sendAgreed, ProposeRequest currentProposal, FileStorageService fileStorageService)
    {
        int toWin = (_context.otherCandidates.Count + 1) / 2 + 1;

        if (sendAgreed >= toWin)
        {
            lock (_context.OfficialPrimes)
            {
                if (!_context.OfficialPrimes.Exists(p=> p.PrimeNumber == numberToVote))
                {
                    _context.OfficialPrimes.Add(new LogEntry(_context.currentLeaderTerm, numberToVote));

                    fileStorageService.SaveToFile(numberToVote, currentProposal.MyAddress, currentProposal.SearchTime);

                }
            }
        }
    }
}