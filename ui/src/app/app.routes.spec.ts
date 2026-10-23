import { TestBed } from '@angular/core/testing';
import { Title } from '@angular/platform-browser';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { StargateApi } from './api/stargate-api';
import { routes } from './app.routes';

describe('routes', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter(routes),
        { provide: StargateApi, useValue: { getPeople: () => of([]) } },
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
});
