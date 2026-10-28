namespace RaftDashboard.Models;

public record NodeEvent(long Seq, string Kind, int Value, string Peer, int Term);

public record NodeStatus(
    int Index,
    string Address,
    bool Online,
    bool Paused,
    bool WaitingForStart,
    string Id,
    string Role,
    int CurrentTerm,
    string CurrentLeader,
    int LogCount,
    int PendingCount,
    int WorkDelay,
    List<NodeEvent> Events);

public record ClusterSnapshot(DateTime Timestamp, List<NodeStatus> Nodes, int DelayMs);
