using Grpc.Core;
using Raft.Models;
using Raft.Grpc;
namespace Raft.Services;

public class ConsensusService : RaftConsensus.RaftConsensusBase
{
    private readonly RaftNode _node;
    public ConsensusService(RaftNode node)
    {
        _node = node;
    }
    
    public override Task<AppendResponse> AppendEntries(AppendRequest request, ServerCallContext context)
    {
        if (request.LeaderAddress == _node.id) 
            return Task.FromResult(new AppendResponse { IsDone = true, Result = 1 });
        if(request.LeaderTerm >= _node.currentTerm)
        {
            _node.CancelTokenTimeout();
            _node.currentTerm = request.LeaderTerm;
            _node._currentState = Role.Follower;
            _node.currentLeader = request.LeaderAddress;
            if (request.Number > 0)
            {
                lock (_node.OfficialPrimes)
                {
                    if(!_node.OfficialPrimes.Contains(request.Number)) _node.OfficialPrimes.Add(request.Number);
                }
            }
        }
        return Task.FromResult(new AppendResponse { IsDone = true, Result = 1 });
    }

    public override Task<ProposeResponse> ProposeNumber(ProposeRequest request, ServerCallContext context)
    {
        if (_node._currentState == Role.Leader)
        {
            lock (_node.pendingNumber)
            {
                if (!_node.OfficialPrimes.Contains(request.Number) && _node.pendingNumber.All(p => p.Number != request.Number))
                {
                    _node.pendingNumber.Enqueue(request);
                }
            }
        }

        return Task.FromResult(new ProposeResponse { NumberToSave = request.Number, IsFine = true, LeaderAddress = request.LeaderAddress});
    }

    public override Task<VoteResponse> RequestVote(VoteRequest request, ServerCallContext context)
    {
        lock (_node)
        {
            if(request.Term < _node.currentTerm) return Task.FromResult(new VoteResponse { VoteGranted = false });
            
            if (request.Term > _node.currentTerm)
            {
                _node.currentTerm = request.Term;
                _node.votedFor = string.Empty;
                _node._currentState = Role.Follower;
            }

            if (request.Term >= _node.currentTerm &&
                (string.IsNullOrEmpty(_node.votedFor) || _node.votedFor == request.Candidate))
            {
                _node.CancelTokenTimeout();
                _node.votedFor = request.Candidate;
                return Task.FromResult(new VoteResponse { VoteGranted = true });
            }
        }

        return Task.FromResult(new VoteResponse { VoteGranted = false });
    }
    
}