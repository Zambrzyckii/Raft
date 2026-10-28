import { NodeStatus } from './cluster-state';

export interface Point {
  x: number;
  y: number;
}

export const CENTER: Point = { x: 50, y: 48 };
const RADIUS = 38;

export function centerIndex(nodes: NodeStatus[]): number {
  let center = 0;
  let bestTerm = -1;
  for (const node of nodes) {
    if (node.online && !node.paused && node.role === 'Leader' && node.currentTerm > bestTerm) {
      center = node.index;
      bestTerm = node.currentTerm;
    }
  }
  return center;
}

export function position(index: number, count: number, center: number): Point {
  if (index === center) {
    return CENTER;
  }
  const angle = (-90 + (index - 1) * (360 / count)) * (Math.PI / 180);
  return { x: CENTER.x + RADIUS * Math.cos(angle), y: CENTER.y + RADIUS * Math.sin(angle) };
}
