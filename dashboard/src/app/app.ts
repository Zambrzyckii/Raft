import { Component, computed, effect, ElementRef, inject, signal } from '@angular/core';
import { Cluster } from './cluster';
import { ClusterSnapshot, NodeStatus } from './cluster-state';

interface SceneNode {
  status: NodeStatus;
  x: number;
  y: number;
  isCenter: boolean;
}

interface NodeMessage {
  id: number;
  text: string;
}

interface PlayItem {
  node: NodeStatus;
  kind: string;
  value: number;
  peer: string;
}

@Component({
  selector: 'app-root',
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly cluster = inject(Cluster);
  private readonly host: ElementRef<HTMLElement> = inject(ElementRef);
  private readonly reducedMotion =
    typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches;

  private messageId = 0;
  protected readonly messages = signal<NodeMessage[][]>([]);

  protected readonly scene = computed<SceneNode[]>(() => {
    const snapshot = this.cluster.snapshot();
    if (!snapshot) {
      return [];
    }
    const center = this.centerIndex(snapshot.nodes);
    return snapshot.nodes.map(node => {
      const position = this.position(node.index, snapshot.nodes.length, center);
      return { status: node, x: position.x, y: position.y, isCenter: node.index === center };
    });
  });

  protected readonly leader = computed(() => this.scene().find(node => node.isCenter) ?? null);

  protected readonly term = computed(() => {
    const nodes = this.cluster.snapshot()?.nodes.filter(node => node.online) ?? [];
    return nodes.length > 0 ? Math.max(...nodes.map(node => node.currentTerm)) : 0;
  });

  protected readonly sliderValue = computed(() => 2100 - (this.cluster.snapshot()?.delayMs ?? 800));

  constructor() {
    effect(() => {
      const snapshot = this.cluster.snapshot();
      if (snapshot) {
        this.play(snapshot);
      }
    });
  }

  protected toggle(status: NodeStatus): void {
    if (!status.online) {
      return;
    }
    if (status.paused) {
      this.cluster.resume(status.index);
    } else {
      this.cluster.pause(status.index);
    }
  }

  protected onSpeedChange(event: Event): void {
    const value = Number((event.target as HTMLInputElement).value);
    this.cluster.setSpeed(2100 - value);
  }

  private play(snapshot: ClusterSnapshot): void {
    const queue: PlayItem[] = [];
    for (const node of snapshot.nodes) {
      for (const nodeEvent of node.events) {
        queue.push({ node, kind: nodeEvent.kind, value: nodeEvent.value, peer: nodeEvent.peer });
      }
    }
    queue.forEach((item, i) => {
      if (this.reducedMotion) {
        this.playOne(item, snapshot);
      } else {
        setTimeout(() => this.playOne(item, snapshot), i * 120);
      }
    });
  }

  private playOne(item: PlayItem, snapshot: ClusterSnapshot): void {
    const text = this.messageFor(item, snapshot.nodes.length);
    if (text) {
      this.pushMessage(item.node.index, text);
    }
    if (this.reducedMotion) {
      return;
    }
    const count = snapshot.nodes.length;
    const center = this.centerIndex(snapshot.nodes);
    switch (item.kind) {
      case 'found':
        this.pulse(item.node.index);
        break;
      case 'propose':
        if (center > 0 && item.node.index !== center) {
          this.shoot(String(item.value), this.position(item.node.index, count, center), { x: 50, y: 50 });
        }
        break;
      case 'replicate':
        for (const other of snapshot.nodes) {
          if (other.index !== item.node.index && other.online && !other.paused) {
            this.shoot(String(item.value), { x: 50, y: 50 }, this.position(other.index, count, center));
          }
        }
        break;
      case 'committed':
        this.ping();
        break;
    }
  }

  private messageFor(item: PlayItem, count: number): string {
    switch (item.kind) {
      case 'found': return `found ${item.value}`;
      case 'propose': return `propose ${item.value}`;
      case 'received': return `got ${item.value} from #${this.peerIndex(item.peer)}`;
      case 'replicate': return `replicating ${item.value}`;
      case 'committed': return `committed ${item.value}`;
      case 'campaign': return `campaigning t${item.value}`;
      case 'vote': return `got ${item.value}/${count} votes`;
      case 'won': return `won ${item.value}/${count}`;
      case 'lost': return `lost ${item.value}/${count}`;
      case 'timeout': return 'leader timeout';
      case 'paused': return 'paused';
      case 'resumed': return 'resumed';
      default: return '';
    }
  }

  private peerIndex(peer: string): number {
    const port = Number(peer.split(':').pop());
    return Number.isFinite(port) ? port - 5000 : 0;
  }

  private pushMessage(index: number, text: string): void {
    const all = this.messages().slice();
    const line = { id: ++this.messageId, text };
    all[index] = [line, ...(all[index] ?? [])].slice(0, 3);
    this.messages.set(all);
  }

  private centerIndex(nodes: NodeStatus[]): number {
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

  private position(index: number, count: number, center: number): { x: number; y: number } {
    if (index === center) {
      return { x: 50, y: 50 };
    }
    const angle = (-90 + (index - 1) * (360 / count)) * (Math.PI / 180);
    return { x: 50 + 38 * Math.cos(angle), y: 50 + 38 * Math.sin(angle) };
  }

  private stage(): HTMLElement | null {
    return this.host.nativeElement.querySelector('.stage');
  }

  private shoot(label: string, from: { x: number; y: number }, to: { x: number; y: number }): void {
    const stage = this.stage();
    if (!stage) {
      return;
    }
    const shot = document.createElement('span');
    shot.className = 'shot';
    shot.textContent = label;
    shot.style.left = `${from.x}%`;
    shot.style.top = `${from.y}%`;
    stage.appendChild(shot);
    const animation = shot.animate(
      [
        { left: `${from.x}%`, top: `${from.y}%`, opacity: 0.2 },
        { opacity: 1, offset: 0.2 },
        { left: `${to.x}%`, top: `${to.y}%`, opacity: 0.9 }
      ],
      { duration: 600, easing: 'cubic-bezier(0.3, 0, 0.4, 1)' }
    );
    animation.onfinish = () => shot.remove();
  }

  private pulse(index: number): void {
    const circle = this.host.nativeElement.querySelector(`[data-node="${index}"] .circle`);
    circle?.animate(
      [{ transform: 'scale(1)' }, { transform: 'scale(1.18)' }, { transform: 'scale(1)' }],
      { duration: 400, easing: 'ease-out' }
    );
  }

  private ping(): void {
    const stage = this.stage();
    if (!stage) {
      return;
    }
    const ring = document.createElement('span');
    ring.className = 'ping';
    stage.appendChild(ring);
    setTimeout(() => ring.remove(), 1000);
  }
}
