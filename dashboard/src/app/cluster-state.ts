export interface NodeStatus {
  index: number;
  address: string;
  online: boolean;
  paused: boolean;
  id: string;
  role: string;
  currentTerm: number;
  currentLeader: string;
  logCount: number;
  pendingCount: number;
}

export interface ClusterSnapshot {
  timestamp: string;
  nodes: NodeStatus[];
}
