import { showStartupError } from './startup-error';

describe('showStartupError', () => {
  afterEach(() => document.body.replaceChildren());

  it('should replace the page with the reason the app could not start', () => {
    document.body.innerHTML = '<app-root></app-root>';

    showStartupError(new Error('config.json must set apiBaseUrl.'));

    expect(document.querySelector('app-root')).toBeNull();
    expect(document.body.textContent).toBe(
      "Stargate ACTS couldn't start. config.json must set apiBaseUrl.",
    );
  });

  it('should show the reason as text, not markup', () => {
    showStartupError(new Error('<img src="x">'));

    expect(document.querySelector('img')).toBeNull();
    expect(document.body.textContent).toContain('<img src="x">');
  });
});
