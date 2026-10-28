import { Component, computed, ElementRef, inject, signal, viewChild } from '@angular/core';
import { Cluster } from './cluster';
import { centerIndex } from './layout';

const TEMPO_STEPS = [2000, 1500, 1200, 1000, 800, 600, 400, 300, 200, 150, 100];

@Component({
  selector: 'app-header',
  templateUrl: './header.html',
  styleUrl: './header.css'
})
export class Header {
  protected readonly cluster = inject(Cluster);
  protected readonly lastStep = TEMPO_STEPS.length - 1;
  private readonly dragged = signal<number | null>(null);
  private readonly tempo = viewChild.required<ElementRef<HTMLInputElement>>('tempo');

  protected readonly leader = computed(() => {
    const nodes = this.cluster.snapshot()?.nodes ?? [];
    const center = centerIndex(nodes);
    return nodes.find(node => node.index === center) ?? null;
  });

  protected readonly term = computed(() => {
    const nodes = this.cluster.snapshot()?.nodes.filter(node => node.online) ?? [];
    return nodes.length > 0 ? Math.max(...nodes.map(node => node.currentTerm)) : 0;
  });

  protected readonly delay = computed(() => this.dragged() ?? this.cluster.delay());

  protected readonly step = computed(() => {
    const delay = this.delay();
    return TEMPO_STEPS.reduce(
      (best, step, i) => (Math.abs(step - delay) < Math.abs(TEMPO_STEPS[best] - delay) ? i : best), 0);
  });

  protected readonly fill = computed(() => `${(this.step() / this.lastStep) * 100}%`);

  protected onTempoInput(event: Event): void {
    this.dragged.set(TEMPO_STEPS[Number((event.target as HTMLInputElement).value)]);
  }

  protected onTempoChange(event: Event): void {
    this.cluster.setSpeed(TEMPO_STEPS[Number((event.target as HTMLInputElement).value)]);
    this.dragged.set(null);
  }

  protected start(): void {
    this.cluster.startSearch();
    this.tempo().nativeElement.focus();
  }
}
