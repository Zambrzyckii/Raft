import { computed, inject, Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { Observable, Subject } from 'rxjs';
import { ClusterSnapshot } from './cluster-state';

export interface Notice {
  command: string;
  ok: boolean;
  text: string;
}

@Injectable({
  providedIn: 'root'
})
export class Cluster {
  readonly snapshot = signal<ClusterSnapshot | null>(null);
  readonly connected = signal(false);
  readonly notice = signal<Notice | null>(null);

  private readonly http = inject(HttpClient);
  private readonly incoming = new Subject<ClusterSnapshot>();
  readonly snapshots: Observable<ClusterSnapshot> = this.incoming.asObservable();

  private readonly pendingDelay = signal<number | null>(null);
  readonly delay = computed(() => this.pendingDelay() ?? this.snapshot()?.delayMs ?? 800);
  private readonly startRequested = signal(false);
  readonly waiting = computed(() => !this.startRequested() && isWaiting(this.snapshot()));
  private noticeTimer?: ReturnType<typeof setTimeout>;

  private readonly connection: HubConnection = new HubConnectionBuilder()
    .withUrl('/hub/cluster')
    .withAutomaticReconnect({ nextRetryDelayInMilliseconds: () => 2000 })
    .build();

  constructor() {
    this.connection.on('ClusterState', (snapshot: ClusterSnapshot) => {
      if (snapshot.delayMs === this.pendingDelay()) {
        this.pendingDelay.set(null);
      }
      if (!isWaiting(snapshot)) {
        this.startRequested.set(false);
      }
      this.snapshot.set(snapshot);
      this.incoming.next(snapshot);
    });
    this.connection.onreconnecting(() => this.connected.set(false));
    this.connection.onreconnected(() => this.connected.set(true));
    void this.start();
  }

  pause(index: number): void {
    this.send(`pause ${index}`, `/api/nodes/${index}/pause`, `node #${index} paused`, `node #${index}`);
  }

  resume(index: number): void {
    this.send(`resume ${index}`, `/api/nodes/${index}/resume`, `node #${index} resumed`, `node #${index}`);
  }

  setSpeed(delayMs: number): void {
    this.pendingDelay.set(delayMs);
    this.send(`tempo ${delayMs}`, `/api/speed/${delayMs}`, `${delayMs} ms per checked number`, 'tempo');
  }

  startSearch(): void {
    this.startRequested.set(true);
    this.send('start', '/api/search/start', 'prime search started', 'cluster');
  }

  private send(command: string, url: string, success: string, subject: string): void {
    this.http.post(url, null).subscribe({
      next: () => this.show({ command, ok: true, text: success }),
      error: (error: HttpErrorResponse) => {
        this.pendingDelay.set(null);
        this.startRequested.set(false);
        this.show({ command, ok: false, text: describe(error, subject) });
      }
    });
  }

  private show(notice: Notice): void {
    clearTimeout(this.noticeTimer);
    this.notice.set(notice);
    this.noticeTimer = setTimeout(() => this.notice.set(null), 3500);
  }

  private async start(): Promise<void> {
    try {
      await this.connection.start();
      this.connected.set(true);
    } catch {
      setTimeout(() => void this.start(), 2000);
    }
  }
}

function isWaiting(snapshot: ClusterSnapshot | null): boolean {
  return snapshot?.nodes.some(node => node.online && node.waitingForStart) ?? false;
}

function describe(error: HttpErrorResponse, subject: string): string {
  switch (error.status) {
    case 0:
      return 'gateway unreachable';
    case 404:
      return `${subject} not found`;
    case 502:
      return `${subject} unreachable`;
    default:
      return `failed with status ${error.status}`;
  }
}
