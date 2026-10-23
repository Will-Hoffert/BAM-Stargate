import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { LoadingTracker } from './loading/loading-tracker';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])],
    })
      .compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should show the app name in the toolbar', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('mat-toolbar')?.textContent).toContain('Stargate ACTS');
  });

  it('should show a progress bar while waiting on the API', async () => {
    const fixture = TestBed.createComponent(App);
    TestBed.inject(LoadingTracker).start();
    await fixture.whenStable();
    const progressBar = (fixture.nativeElement as HTMLElement).querySelector('mat-progress-bar');
    expect(progressBar?.getAttribute('aria-label')).toBe('Loading');
  });

  it('should show no progress bar when nothing is loading', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    expect((fixture.nativeElement as HTMLElement).querySelector('mat-progress-bar')).toBeNull();
  });
});
