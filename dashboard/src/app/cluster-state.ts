export interface NodeEvent {
  seq: number;
  kind: string;
  value: number;
  peer: string;
  term: number;
}

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
  workDelay: number;
  events: NodeEvent[];
}

export interface ClusterSnapshot {
  timestamp: string;
  nodes: NodeStatus[];
  delayMs: number;
}
