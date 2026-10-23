import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { loadingInterceptor } from './loading-interceptor';
import { LoadingTracker } from './loading-tracker';

describe('loadingInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let loading: LoadingTracker;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([loadingInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    loading = TestBed.inject(LoadingTracker);
  });

  afterEach(() => backend.verify());

  it('should be loading only while a request waits for its answer', () => {
    expect(loading.isLoading()).toBe(false);

    http.get('/people').subscribe();
    expect(loading.isLoading()).toBe(true);

    backend.expectOne('/people').flush([]);
    expect(loading.isLoading()).toBe(false);
  });

  it('should stop loading when a request fails', () => {
    http.get('/people').subscribe({ error: () => {} });

    backend.expectOne('/people').flush(null, { status: 500, statusText: 'Server Error' });

    expect(loading.isLoading()).toBe(false);
  });

  it('should stop loading when the caller cancels a request', () => {
    const subscription = http.get('/people').subscribe();
    const request = backend.expectOne('/people');

    subscription.unsubscribe();

    expect(request.cancelled).toBe(true);
    expect(loading.isLoading()).toBe(false);
  });

  it('should keep loading until every overlapping request has finished', () => {
    http.get('/people').subscribe();
    http.get('/log').subscribe();

    backend.expectOne('/people').flush([]);
    expect(loading.isLoading()).toBe(true);

    backend.expectOne('/log').flush([]);
    expect(loading.isLoading()).toBe(false);
  });
});
