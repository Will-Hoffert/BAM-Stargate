import { Injectable, computed, signal } from '@angular/core';

/** Counts the HTTP calls still waiting for an answer, for the progress bar. */
@Injectable({ providedIn: 'root' })
export class LoadingTracker {
  private readonly pending = signal(0);

  readonly isLoading = computed(() => this.pending() > 0);

  start(): void {
    this.pending.update((count) => count + 1);
  }

  stop(): void {
    this.pending.update((count) => count - 1);
  }
}
