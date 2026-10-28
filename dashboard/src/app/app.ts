import { Component, computed, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Cluster } from './cluster';
import { NodeStatus } from './cluster-state';
import { Header } from './header';
import { centerIndex, Point, position } from './layout';
import { Playback } from './playback';

interface SceneNode {
  status: NodeStatus;
  at: Point;
  isCenter: boolean;
  tag: string;
}

@Component({
  selector: 'app-root',
  imports: [Header],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly cluster = inject(Cluster);
  protected readonly playback = inject(Playback);

  protected readonly scene = computed<SceneNode[]>(() => {
    const nodes = this.cluster.snapshot()?.nodes ?? [];
    const center = centerIndex(nodes);
    return nodes.map(node => ({
      status: node,
      at: position(node.index, nodes.length, center),
      isCenter: node.index === center,
      tag: tagOf(node)
    }));
  });

  constructor() {
    this.cluster.snapshots.pipe(takeUntilDestroyed()).subscribe(snapshot => this.playback.play(snapshot));
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
}

function tagOf(node: NodeStatus): string {
  if (!node.online) {
    return 'offline';
  }
  if (node.paused) {
    return 'paused';
  }
  return node.role === 'Follower' ? '' : node.role.toLowerCase();
}
