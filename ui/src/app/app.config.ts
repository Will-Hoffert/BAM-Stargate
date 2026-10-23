import { provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  ApplicationConfig,
  inject,
  provideAppInitializer,
  provideBrowserGlobalErrorListeners,
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { routes } from './app.routes';
import { RuntimeConfig } from './config/runtime-config';
import { loadingInterceptor } from './loading/loading-interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    // Hands route parameters to page inputs, e.g. /people/:name to the timeline's name.
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([loadingInterceptor])),
    // Nothing can call the API without its address, so the app waits for config.json before starting.
    provideAppInitializer(() => inject(RuntimeConfig).load())
  ]
};
