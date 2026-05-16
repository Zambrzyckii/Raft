using Grpc.Core;
using Raft.Models;
using Raft.Grpc;
namespace Raft.Services;

public class ConsensusService : RaftConsensus.RaftConsensusBase
{
    private readonly RaftNode _node;

    public override Task<AppendResponse> AppendEntries(AppendRequest request, ServerCallContext context)
    {
        return Task.FromResult(new AppendResponse { IsDone = true, Result = 1 });
    }
}