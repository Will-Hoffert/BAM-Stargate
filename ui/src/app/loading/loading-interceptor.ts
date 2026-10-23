import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize } from 'rxjs';
import { LoadingTracker } from './loading-tracker';

// finalize runs however the call ends: an answer, an error, or the caller cancelling it.
// Stopping only on success would leave the progress bar running after the first error.
export const loadingInterceptor: HttpInterceptorFn = (request, next) => {
  const loading = inject(LoadingTracker);
  loading.start();
  return next(request).pipe(finalize(() => loading.stop()));
};
