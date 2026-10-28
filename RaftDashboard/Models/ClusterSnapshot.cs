namespace RaftDashboard.Models;

public record NodeStatus(
    int Index,
    string Address,
    bool Online,
    bool Paused,
    string Id,
    string Role,
    int CurrentTerm,
    string CurrentLeader,
    int LogCount,
    int PendingCount);

public record ClusterSnapshot(DateTime Timestamp, List<NodeStatus> Nodes);
