import { Component, inject } from '@angular/core';
import { Cluster } from './cluster';

@Component({
  selector: 'app-root',
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  protected readonly cluster = inject(Cluster);
}
