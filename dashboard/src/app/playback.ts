import { Injectable, signal, WritableSignal } from '@angular/core';
import { ClusterSnapshot, NodeStatus } from './cluster-state';
import { centerIndex, Point, position } from './layout';

export interface FeedLine {
  id: number;
  key: string;
  text: string;
  status: string;
  done: boolean;
}

export interface Shot {
  id: number;
  label: string;
  from: Point;
  to: Point;
}

export interface Halo {
  id: number;
  at: Point;
}

interface Step {
  node: NodeStatus;
  kind: string;
  value: number;
  peer: string;
  term: number;
}

const FLIGHT_MS = 600;
const MAX_BATCH = 60;
const MAX_EFFECTS = 40;
const LEADER_KINDS = ['received', 'replicate', 'committed'];

@Injectable({
  providedIn: 'root'
})
export class Playback {
  readonly lines = signal<FeedLine[][]>([]);
  readonly shots = signal<Shot[]>([]);
  readonly halos = signal<Halo[]>([]);
  readonly pings = signal<number[]>([]);

  private readonly reducedMotion =
    typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches;
  private readonly flight = this.reducedMotion ? 0 : FLIGHT_MS;
  private nextId = 0;
  private readonly landing = new Map<number, number>();
  private readonly replicated = new Map<number, number>();

  play(snapshot: ClusterSnapshot): void {
    const steps: Step[] = snapshot.nodes.flatMap(node =>
      node.events.map(event => ({ node, kind: event.kind, value: event.value, peer: event.peer, term: event.term })));
    if (steps.length === 0 || steps.length > MAX_BATCH || document.hidden) {
      return;
    }
    const now = Date.now();
    const center = centerIndex(snapshot.nodes);
    const local = steps.filter(step => !LEADER_KINDS.includes(step.kind));
    const gap = this.reducedMotion ? 0 : Math.min(120, 400 / local.length);
    local.forEach((step, i) => {
      const time = now + i * gap;
      if (step.kind === 'propose') {
        const flies = center > 0 && step.node.index !== center;
        this.remember(this.landing, step.value, time + (flies ? this.flight : 0));
      }
      this.schedule(time - now, step, snapshot);
    });
    for (const step of steps.filter(step => LEADER_KINDS.includes(step.kind))) {
      let time = Math.max(now, this.landing.get(step.value) ?? now);
      if (step.kind === 'replicate') {
        this.remember(this.replicated, step.value, time + this.flight);
      } else if (step.kind === 'committed') {
        time = Math.max(time, this.replicated.get(step.value) ?? now);
      }
      this.schedule(time - now, step, snapshot);
    }
  }

  removeShot(id: number): void {
    this.shots.update(shots => shots.filter(shot => shot.id !== id));
  }

  removeHalo(id: number): void {
    this.halos.update(halos => halos.filter(halo => halo.id !== id));
  }

  removePing(id: number): void {
    this.pings.update(pings => pings.filter(ping => ping !== id));
  }

  private schedule(delay: number, step: Step, snapshot: ClusterSnapshot): void {
    setTimeout(() => this.apply(step, snapshot), delay);
  }

  private apply(step: Step, snapshot: ClusterSnapshot): void {
    const index = step.node.index;
    const count = snapshot.nodes.length;
    const center = centerIndex(snapshot.nodes);
    const number = `n:${step.value}`;
    const term = `t:${step.term}`;
    switch (step.kind) {
      case 'found':
        this.write(index, number, `${step.value}`, 'found');
        this.add(this.halos, { id: ++this.nextId, at: position(index, count, center) });
        break;
      case 'propose':
        if (center > 0 && index !== center) {
          this.shoot(step.value, position(index, count, center), position(center, count, center));
        }
        break;
      case 'received':
        this.write(index, number, `${step.value} from #${peerIndex(step.peer)}`, 'received');
        break;
      case 'replicate':
        this.write(index, number, `${step.value}`, 'replicating');
        for (const other of snapshot.nodes) {
          if (other.index !== index && other.online && !other.paused) {
            this.shoot(step.value, position(index, count, center), position(other.index, count, center));
          }
        }
        break;
      case 'committed':
        this.write(index, number, `${step.value}`, 'committed', true);
        this.settle(number);
        this.add(this.pings, ++this.nextId);
        this.landing.delete(step.value);
        this.replicated.delete(step.value);
        break;
      case 'campaign':
        this.write(index, term, `term ${step.value}`, 'campaigning');
        break;
      case 'vote':
        this.write(index, term, `term ${step.term}`, `votes ${step.value}/${count}`);
        break;
      case 'won':
        this.write(index, term, `term ${step.term}`, `won ${step.value}/${count}`, true);
        break;
      case 'lost':
        this.write(index, term, `term ${step.term}`, `lost ${step.value}/${count}`);
        break;
      case 'timeout':
        this.write(index, `x:${++this.nextId}`, 'leader', 'timed out');
        break;
    }
  }

  private write(index: number, key: string, text: string, status: string, done = false): void {
    this.lines.update(all => {
      const next = all.slice();
      const lines = next[index] ?? [];
      next[index] = lines.some(line => line.key === key)
        ? lines.map(line => (line.key === key ? { ...line, status, done } : line))
        : [{ id: ++this.nextId, key, text, status, done }, ...lines].slice(0, 3);
      return next;
    });
  }

  private settle(key: string): void {
    this.lines.update(all =>
      all.map(lines => lines.map(line => (line.key === key ? { ...line, status: 'committed', done: true } : line))));
  }

  private shoot(value: number, from: Point, to: Point): void {
    this.add(this.shots, { id: ++this.nextId, label: `${value}`, from, to });
  }

  private add<T>(effects: WritableSignal<T[]>, effect: T): void {
    if (!this.reducedMotion) {
      effects.update(items => [...items, effect].slice(-MAX_EFFECTS));
    }
  }

  private remember(times: Map<number, number>, value: number, time: number): void {
    times.set(value, time);
    if (times.size > 100) {
      times.delete(times.keys().next().value as number);
    }
  }
}

function peerIndex(peer: string): number {
  const port = Number(peer.split(':').pop());
  return Number.isFinite(port) ? port - 5000 : 0;
}
