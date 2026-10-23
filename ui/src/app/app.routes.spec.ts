import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { Router, provideRouter, withComponentInputBinding } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { PersonDuties } from './api/api-models';
import { StargateApi } from './api/stargate-api';
import { routes } from './app.routes';

const johnDoe: PersonDuties = {
  person: {
    personId: 1,
    name: 'John Doe',
    currentRank: null,
    currentDutyTitle: null,
    careerStartDate: null,
    careerEndDate: null,
  },
  duties: [],
};

describe('routes', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes, withComponentInputBinding()),
        {
          provide: StargateApi,
          useValue: { getPeople: () => of([]), getDuties: () => of(johnDoe) },
        },
      ],
    });
  });

  it('should show the not-found page for an unknown URL', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/no-such-page');
    expect(harness.routeNativeElement?.textContent).toContain('Page not found');
    expect(TestBed.inject(Title).getTitle()).toBe('Page not found – Stargate ACTS');
  });

  // A wildcard route also matches the empty path, so the home URL needs its own route.
  it('should not show the not-found page at the home URL', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/');
    expect(harness.routeNativeElement?.textContent ?? '').not.toContain('Page not found');
  });

  it('should open the possible astronauts page from the home URL', async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/');
    expect(TestBed.inject(Router).url).toBe('/people');
    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe(
      'Possible Astronauts',
    );
    expect(TestBed.inject(Title).getTitle()).toBe('Possible Astronauts – Stargate ACTS');
  });

  it("should open a person's timeline from their URL, titled with their name", async () => {
    const harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/people/John%20Doe');
    expect(harness.routeNativeElement?.querySelector('h1')?.textContent).toBe('John Doe');
    expect(TestBed.inject(Title).getTitle()).toBe('John Doe – Stargate ACTS');
  });
});
