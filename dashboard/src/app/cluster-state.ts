export interface NodeStatus {
  address: string;
  online: boolean;
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
