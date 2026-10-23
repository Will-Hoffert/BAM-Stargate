import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { catchError, firstValueFrom, throwError } from 'rxjs';

interface RuntimeConfigFile {
  apiBaseUrl?: unknown;
}

/**
 * Settings read from config.json when the app starts, so one build can be deployed anywhere:
 * a deployment replaces config.json instead of rebuilding the app.
 */
@Injectable({ providedIn: 'root' })
export class RuntimeConfig {
  private readonly http = inject(HttpClient);
  private loadedApiBaseUrl?: string;

  get apiBaseUrl(): string {
    if (this.loadedApiBaseUrl === undefined) {
      throw new Error('The runtime configuration has not been loaded.');
    }
    return this.loadedApiBaseUrl;
  }

  async load(): Promise<void> {
    const file = await firstValueFrom(
      // no-cache makes the browser check for a newer config.json, so a redeployed one takes effect.
      this.http
        .get<RuntimeConfigFile | null>('config.json', { cache: 'no-cache' })
        .pipe(
          catchError((error: HttpErrorResponse) =>
            throwError(() => new Error(`Could not load config.json: ${error.message}`)),
          ),
        ),
    );

    const apiBaseUrl = file?.apiBaseUrl;
    if (typeof apiBaseUrl !== 'string' || apiBaseUrl.trim() === '') {
      throw new Error('config.json must set apiBaseUrl.');
    }
    this.loadedApiBaseUrl = apiBaseUrl.trim().replace(/\/+$/, '');
  }
}
