namespace RaftDashboard.Models;

public record NodeStatus(
    string Address,
    bool Online,
    string Id,
    string Role,
    int CurrentTerm,
    string CurrentLeader,
    int LogCount,
    int PendingCount);

public record ClusterSnapshot(DateTime Timestamp, List<NodeStatus> Nodes);
