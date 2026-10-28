import { Injectable, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { ClusterSnapshot } from './cluster-state';

@Injectable({
  providedIn: 'root'
})
export class Cluster {
  readonly snapshot = signal<ClusterSnapshot | null>(null);
  readonly connected = signal(false);

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

  private async start(): Promise<void> {
    try {
      await this.connection.start();
      this.connected.set(true);
    } catch {
      setTimeout(() => void this.start(), 2000);
    }
  }
}
