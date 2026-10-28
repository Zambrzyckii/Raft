import { Component, computed, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Cluster } from './cluster';
import { NodeStatus } from './cluster-state';
import { centerIndex, Point, position } from './layout';
import { Playback } from './playback';

interface SceneNode {
  status: NodeStatus;
  at: Point;
  isCenter: boolean;
}

@Component({
  selector: 'app-root',
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
      isCenter: node.index === center
    }));
  });

  protected readonly leader = computed(() => this.scene().find(node => node.isCenter) ?? null);

  protected readonly term = computed(() => {
    const nodes = this.cluster.snapshot()?.nodes.filter(node => node.online) ?? [];
    return nodes.length > 0 ? Math.max(...nodes.map(node => node.currentTerm)) : 0;
  });

  protected readonly sliderValue = computed(() => 2100 - (this.cluster.snapshot()?.delayMs ?? 800));

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

  protected onSpeedChange(event: Event): void {
    const value = Number((event.target as HTMLInputElement).value);
    this.cluster.setSpeed(2100 - value);
  }
}
