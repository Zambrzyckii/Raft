import { inject, Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { ClusterSnapshot } from './cluster-state';

@Injectable({
  providedIn: 'root'
})
export class Cluster {
  readonly snapshot = signal<ClusterSnapshot | null>(null);
  readonly connected = signal(false);

  private readonly http = inject(HttpClient);

  private readonly connection: HubConnection = new HubConnectionBuilder()
    .withUrl('/hub/cluster')
    .withAutomaticReconnect({ nextRetryDelayInMilliseconds: () => 2000 })
    .build();

  constructor() {
    this.connection.on('ClusterState', (snapshot: ClusterSnapshot) => this.snapshot.set(snapshot));
    this.connection.onreconnecting(() => this.connected.set(false));
    this.connection.onreconnected(() => this.connected.set(true));
    void this.start();
  }

  pause(index: number): void {
    this.http.post(`/api/nodes/${index}/pause`, null).subscribe({
      error: error => console.error(`pause of node ${index} failed`, error)
    });
  }

  resume(index: number): void {
    this.http.post(`/api/nodes/${index}/resume`, null).subscribe({
      error: error => console.error(`resume of node ${index} failed`, error)
    });
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
