using Raft.Grpc;

namespace Raft.RoleBehaviours.Leader;

public static class GetSignal
{
    public static async Task<AppendResponse?> GetSignalSafe(RaftConsensus.RaftConsensusClient client, AppendRequest package)
    {
        try
        {
            return await client.AppendEntriesAsync(package);
        }
        catch
        {
            return null;
        }
    }
}