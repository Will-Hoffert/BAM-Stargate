import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { TestBed } from '@angular/core/testing';
import { MatButtonHarness } from '@angular/material/button/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MatDatepickerInputHarness } from '@angular/material/datepicker/testing';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { MatFormFieldHarness } from '@angular/material/form-field/testing';
import { MatInputHarness } from '@angular/material/input/testing';
import { Router, provideRouter } from '@angular/router';
import { Observable, of, throwError } from 'rxjs';
import { ApiError } from '../api/api-error';
import { NewDuty, Person, PersonDuties } from '../api/api-models';
import { StargateApi } from '../api/stargate-api';
import { PersonTimeline } from './person-timeline';

const johnDoe: Person = {
  personId: 1,
  name: 'John Doe',
  currentRank: '1LT',
  currentDutyTitle: 'Commander',
  careerStartDate: '2020-01-01',
  careerEndDate: null,
};

const activeCareer: PersonDuties = {
  person: johnDoe,
  duties: [
    { id: 2, rank: '1LT', dutyTitle: 'Commander', dutyStartDate: '2022-06-01', dutyEndDate: null },
    {
      id: 1,
      rank: '2LT',
      dutyTitle: 'Pilot',
      dutyStartDate: '2020-01-01',
      dutyEndDate: '2022-05-31',
    },
  ],
};

const retiredCareer: PersonDuties = {
  person: {
    personId: 2,
    name: 'Amy Ray',
    currentRank: 'COL',
    currentDutyTitle: 'RETIRED',
    careerStartDate: '2001-03-15',
    careerEndDate: '2024-12-31',
  },
  duties: [
    { id: 4, rank: 'COL', dutyTitle: 'RETIRED', dutyStartDate: '2025-01-01', dutyEndDate: null },
    {
      id: 3,
      rank: 'COL',
      dutyTitle: 'Pilot',
      dutyStartDate: '2001-03-15',
      dutyEndDate: '2024-12-31',
    },
  ],
};

async function render(
  name: string,
  getDuties: (name: string) => Observable<PersonDuties>,
  addDuty: (duty: NewDuty) => Observable<number> = () => of(1),
  renamePerson: (name: string, newName: string) => Observable<number> = () => of(1),
) {
  TestBed.configureTestingModule({
    imports: [PersonTimeline],
    providers: [
      provideRouter([]),
      { provide: StargateApi, useValue: { getDuties, addDuty, renamePerson } },
      // Without animations a dialog closes straight away, so tests don't wait on a timer.
      { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
    ],
  });
  const fixture = TestBed.createComponent(PersonTimeline);
  fixture.componentRef.setInput('name', name);
  await fixture.whenStable();
  return {
    element: fixture.nativeElement as HTMLElement,
    loader: TestbedHarnessEnvironment.loader(fixture),
    // Dialogs open outside the page's own element.
    documentLoader: TestbedHarnessEnvironment.documentRootLoader(fixture),
  };
}

/** The profile's facts, by label. */
function facts(element: HTMLElement): Record<string, string> {
  const labels = [...element.querySelectorAll('dt')].map((dt) => text(dt));
  const values = [...element.querySelectorAll('dd')].map((dd) => text(dd));
  return Object.fromEntries(labels.map((label, i) => [label, values[i]]));
}

function entries(element: HTMLElement): HTMLElement[] {
  return [...element.querySelectorAll<HTMLElement>('.timeline > li')];
}

function text(element: Element): string {
  return (element.textContent ?? '').replace(/\s+/g, ' ').trim();
}

describe('PersonTimeline', () => {
  // Durations of current duties run to today.
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 23));
  });

  afterEach(() => vi.useRealTimers());

  it('should load the person named in the URL', async () => {
    const getDuties = vi.fn(() => of(activeCareer));
    await render('John Doe', getDuties);
    expect(getDuties).toHaveBeenCalledWith('John Doe');
  });

  it('should show who the person is and how long their career has been', async () => {
    const { element } = await render('John Doe', () => of(activeCareer));

    expect(text(element.querySelector('h1')!)).toBe('John Doe');
    expect(text(element.querySelector('.status')!)).toBe('Active');
    expect(facts(element)).toEqual({
      'Current rank': '1LT',
      'Current duty title': 'Commander',
      'Career start': 'Jan 1, 2020',
      'Career length': '6 years 9 months',
    });
  });

  it('should list duties newest first, highlighting the current one', async () => {
    const { element } = await render('John Doe', () => of(activeCareer));

    const [current, past] = entries(element);
    expect(current.querySelector('.label')?.textContent).toBe('Current');
    expect(text(current.querySelector('.duty')!)).toBe('Commander · 1LT');
    expect(text(current.querySelector('.when')!)).toBe('Jun 1, 2022 – present · 4 years 4 months');
    expect(current.classList).toContain('current');
    expect(past.querySelector('.label')).toBeNull();
    expect(text(past.querySelector('.duty')!)).toBe('Pilot · 2LT');
    expect(text(past.querySelector('.when')!)).toBe(
      'Jan 1, 2020 – May 31, 2022 · 2 years 5 months',
    );
    expect(past.classList).not.toContain('current');
  });

  it('should end a retired career with a retirement marker', async () => {
    const { element } = await render('Amy Ray', () => of(retiredCareer));

    const [retirement, last] = entries(element);
    expect(text(retirement.querySelector('.duty')!)).toBe('Retired');
    expect(text(retirement.querySelector('.when')!)).toBe('Jan 1, 2025');
    expect(retirement.classList).toContain('retirement');
    expect(text(last.querySelector('.duty')!)).toBe('Pilot · COL');
    expect(text(last.querySelector('.when')!)).toBe(
      'Mar 15, 2001 – Dec 31, 2024 · 23 years 9 months',
    );
    expect(element.querySelector('.timeline > li.current')).toBeNull();
    expect(facts(element)).toEqual({
      'Current rank': 'COL',
      'Current duty title': 'RETIRED',
      'Career start': 'Mar 15, 2001',
      'Career end': 'Dec 31, 2024',
      'Career length': '23 years 9 months',
    });
  });

  it('should say when someone has no duties yet', async () => {
    const { element } = await render('Zed Park', () =>
      of({
        person: {
          personId: 3,
          name: 'Zed Park',
          currentRank: null,
          currentDutyTitle: null,
          careerStartDate: null,
          careerEndDate: null,
        },
        duties: [],
      }),
    );

    expect(text(element.querySelector('.status')!)).toBe('Not an astronaut yet');
    expect(element.textContent).toContain('No duties yet.');
    expect(facts(element)).toEqual({});
    expect(entries(element)).toEqual([]);
  });

  it('should rename the person and replace the old address in the history', async () => {
    const renamePerson = vi.fn(() => of(1));
    const { loader, documentLoader } = await render(
      'John Doe',
      () => of(activeCareer),
      undefined,
      renamePerson,
    );
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);

    await (await loader.getHarness(MatButtonHarness.with({ text: 'Rename' }))).click();
    const dialog = await documentLoader.getHarness(MatDialogHarness);
    expect(await dialog.getTitleText()).toBe('Rename John Doe');
    await (await dialog.getHarness(MatInputHarness)).setValue('Jane Doe');
    await (await dialog.getHarness(MatButtonHarness.with({ text: 'Save' }))).click();

    expect(renamePerson).toHaveBeenCalledWith('John Doe', 'Jane Doe');
    // The old address no longer exists, so Back shouldn't lead to it.
    expect(navigate).toHaveBeenCalledWith(['/people', 'Jane Doe'], { replaceUrl: true });
  });

  it('should offer Add duty to someone who has not retired', async () => {
    const { loader } = await render('John Doe', () => of(activeCareer));
    expect(
      await loader.getHarnessOrNull(MatButtonHarness.with({ text: 'Add duty' })),
    ).not.toBeNull();
  });

  it('should not offer Add duty to someone who has retired', async () => {
    const { element, loader } = await render('Amy Ray', () => of(retiredCareer));

    expect(await loader.getHarnessOrNull(MatButtonHarness.with({ text: 'Add duty' }))).toBeNull();
    expect(text(element.querySelector('.retired-note')!)).toBe(
      'Retired on Jan 1, 2025, so no more duties can be added.',
    );
  });

  it('should add a duty and show the updated timeline', async () => {
    const updatedCareer: PersonDuties = {
      person: { ...johnDoe, currentDutyTitle: 'Flight Engineer' },
      duties: [
        {
          id: 5,
          rank: '1LT',
          dutyTitle: 'Flight Engineer',
          dutyStartDate: '2026-10-01',
          dutyEndDate: null,
        },
        { ...activeCareer.duties[0], dutyEndDate: '2026-09-30' },
        activeCareer.duties[1],
      ],
    };
    const getDuties = vi
      .fn()
      .mockReturnValueOnce(of(activeCareer))
      .mockReturnValue(of(updatedCareer));
    const addDuty = vi.fn(() => of(5));
    const { element, loader, documentLoader } = await render('John Doe', getDuties, addDuty);

    await (await loader.getHarness(MatButtonHarness.with({ text: 'Add duty' }))).click();
    const dialog = await documentLoader.getHarness(MatDialogHarness);
    const titleField = await dialog.getHarness(
      MatFormFieldHarness.with({ floatingLabelText: 'Duty title' }),
    );
    await (await titleField.getControl(MatInputHarness))!.setValue('Flight Engineer');
    await (await dialog.getHarness(MatDatepickerInputHarness)).setValue('10/1/2026');
    await (await dialog.getHarness(MatButtonHarness.with({ text: 'Save' }))).click();

    expect(addDuty).toHaveBeenCalledWith({
      name: 'John Doe',
      rank: '1LT',
      dutyTitle: 'Flight Engineer',
      dutyStartDate: new Date(2026, 9, 1),
    });
    expect(getDuties).toHaveBeenCalledTimes(2);
    expect(entries(element).map((entry) => text(entry.querySelector('.duty')!))).toEqual([
      'Flight Engineer · 1LT',
      'Commander · 1LT',
      'Pilot · 2LT',
    ]);
  });

  it('should say so when no one has that name, with a way back to the list', async () => {
    const { element, loader } = await render('Nobody', () =>
      throwError(() => new ApiError(404, "No person named 'Nobody' exists.")),
    );

    expect(text(element.querySelector('[role="alert"]')!)).toContain(
      "No person named 'Nobody' exists.",
    );
    const back = await loader.getHarness(
      MatButtonHarness.with({ text: 'Back to Possible Astronauts' }),
    );
    expect(await (await back.host()).getAttribute('href')).toBe('/people');
    expect(await loader.getHarnessOrNull(MatButtonHarness.with({ text: 'Try again' }))).toBeNull();
  });

  it('should show other errors, and load again on Try again', async () => {
    let calls = 0;
    const { element, loader } = await render('John Doe', () =>
      ++calls === 1
        ? throwError(
            () => new ApiError(0, "Can't reach the Stargate API. Check that it's running."),
          )
        : of(activeCareer),
    );

    expect(text(element.querySelector('[role="alert"]')!)).toContain(
      "Can't reach the Stargate API. Check that it's running.",
    );

    await (await loader.getHarness(MatButtonHarness.with({ text: 'Try again' }))).click();

    expect(text(element.querySelector('h1')!)).toBe('John Doe');
    expect(element.querySelector('[role="alert"]')).toBeNull();
  });
});
