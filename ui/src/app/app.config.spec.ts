import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ApplicationInitStatus } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { StargateApi } from './api/stargate-api';
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

  it("should give a page the name from the URL to load that person's timeline", async () => {
    const getDuties = vi.fn(() =>
      of({
        person: {
          personId: 1,
          name: 'John Doe',
          currentRank: null,
          currentDutyTitle: null,
          careerStartDate: null,
          careerEndDate: null,
        },
        duties: [],
      }),
    );
    TestBed.configureTestingModule({
      providers: [
        ...appConfig.providers,
        provideHttpClientTesting(),
        { provide: StargateApi, useValue: { getDuties } },
      ],
    });
    TestBed.inject(HttpTestingController)
      .expectOne('config.json')
      .flush({ apiBaseUrl: 'http://api.test' });

    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/people/John%20Doe');

    expect(getDuties).toHaveBeenCalledWith('John Doe');
  });
});
