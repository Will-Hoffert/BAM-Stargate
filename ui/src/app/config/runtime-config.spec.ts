import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RuntimeConfig } from './runtime-config';

describe('RuntimeConfig', () => {
  let config: RuntimeConfig;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    config = TestBed.inject(RuntimeConfig);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('should read the API base URL from config.json', async () => {
    const loaded = config.load();
    http.expectOne('config.json').flush({ apiBaseUrl: 'http://api.test/' });
    await loaded;
    expect(config.apiBaseUrl).toBe('http://api.test');
  });

  it('should ask the browser to check for a newer config.json', () => {
    config.load();
    const request = http.expectOne('config.json');
    expect(request.request.cache).toBe('no-cache');
    request.flush({ apiBaseUrl: 'http://api.test' });
  });

  it('should fail when config.json has no API base URL', async () => {
    const loaded = config.load();
    http.expectOne('config.json').flush({ apiBaseUrl: ' ' });
    await expect(loaded).rejects.toThrow('config.json must set apiBaseUrl.');
  });

  it('should fail when config.json cannot be loaded', async () => {
    const loaded = config.load();
    http.expectOne('config.json').flush('', { status: 404, statusText: 'Not Found' });
    await expect(loaded).rejects.toThrow(/^Could not load config\.json: .*404 Not Found$/);
  });

  it('should not give out an API base URL before loading', () => {
    expect(() => config.apiBaseUrl).toThrow('The runtime configuration has not been loaded.');
  });
});
