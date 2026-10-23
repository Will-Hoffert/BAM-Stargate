import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { appConfig } from './app.config';
import { RuntimeConfig } from './config/runtime-config';

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
});
