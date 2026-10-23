import { TestBed } from '@angular/core/testing';
import { PersonStatus } from '../person-status';
import { StatusPill } from './status-pill';

async function render(status: PersonStatus): Promise<HTMLElement> {
  await TestBed.configureTestingModule({
    imports: [StatusPill],
  }).compileComponents();
  const fixture = TestBed.createComponent(StatusPill);
  fixture.componentRef.setInput('status', status);
  await fixture.whenStable();
  return (fixture.nativeElement as HTMLElement).querySelector('.status') as HTMLElement;
}

describe('StatusPill', () => {
  it('should show an active status', async () => {
    const pill = await render('Active');
    expect(pill.textContent).toBe('Active');
    expect(pill.classList).toContain('active');
  });

  it('should show a retired status', async () => {
    const pill = await render('Retired');
    expect(pill.textContent).toBe('Retired');
    expect(pill.classList).toContain('retired');
  });

  it('should show someone who is not an astronaut yet', async () => {
    const pill = await render('Not an astronaut yet');
    expect(pill.textContent).toBe('Not an astronaut yet');
    expect(pill.classList).not.toContain('active');
    expect(pill.classList).not.toContain('retired');
  });
});
