import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { appConfig } from './app.config';
import { RuntimeConfig } from './config/runtime-config';
import { LoadingTracker } from './loading/loading-tracker';

describe('appConfig', () => {
  it('should load config.json before the app starts', async () => {
    TestBed.configureTestingModule({
      providers: [...appConfig.providers, provideHttpClientTesting()],
    });

    TestBed.inject(HttpTestingController)
      .expectOne('config.json')
      .flush({ apiBaseUrl: 'http://api.test' });
    await TestBed.inject(ApplicationInitStatus).donePromise;

    expect(TestBed.inject(RuntimeConfig).apiBaseUrl).toBe('http://api.test');
  });

  it('should show progress for every HTTP call the app makes', () => {
    TestBed.configureTestingModule({
      providers: [...appConfig.providers, provideHttpClientTesting()],
    });

    // Loading config.json at startup is the first HTTP call.
    const request = TestBed.inject(HttpTestingController).expectOne('config.json');
    expect(TestBed.inject(LoadingTracker).isLoading()).toBe(true);

    request.flush({ apiBaseUrl: 'http://api.test' });
    expect(TestBed.inject(LoadingTracker).isLoading()).toBe(false);
  });
});
