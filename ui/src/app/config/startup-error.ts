/** Shows why the app couldn't start (for example, a missing config.json) instead of leaving a blank page. */
export function showStartupError(error: unknown): void {
  const reason = error instanceof Error ? error.message : String(error);
  const alert = document.createElement('p');
  alert.setAttribute('role', 'alert');
  alert.style.padding = '24px';
  alert.textContent = `Stargate ACTS couldn't start. ${reason}`;
  document.body.replaceChildren(alert);
}
