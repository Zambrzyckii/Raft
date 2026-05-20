using Raft.Models;

namespace Raft.Interfaces;

public interface IRaftState
{
    Task Execute(RaftNode _context);
}