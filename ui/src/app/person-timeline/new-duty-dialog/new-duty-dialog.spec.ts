import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatButtonHarness } from '@angular/material/button/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MatDatepickerInputHarness } from '@angular/material/datepicker/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { MatFormFieldHarness } from '@angular/material/form-field/testing';
import { MatInputHarness } from '@angular/material/input/testing';
import { Observable, Subject, firstValueFrom, of, throwError } from 'rxjs';
import { ApiError } from '../../api/api-error';
import { NewDuty } from '../../api/api-models';
import { StargateApi } from '../../api/stargate-api';
import { NewDutyData, NewDutyDialog } from './new-duty-dialog';

@Component({ template: '' })
class Host {}

const johnDoe: NewDutyData = {
  name: 'John Doe',
  currentRank: '1LT',
  currentDutyStartDate: '2022-06-01',
};

async function open(addDuty: (duty: NewDuty) => Observable<number>, data = johnDoe) {
  TestBed.configureTestingModule({
    providers: [
      { provide: StargateApi, useValue: { addDuty } },
      { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
    ],
  });
  const fixture = TestBed.createComponent(Host);
  const dialogRef = TestBed.inject(MatDialog).open<NewDutyDialog, NewDutyData, boolean>(
    NewDutyDialog,
    { data },
  );
  const closed = firstValueFrom(dialogRef.afterClosed());
  const loader = TestbedHarnessEnvironment.documentRootLoader(fixture);
  const dialog = await loader.getHarness(MatDialogHarness);
  const field = (label: string) =>
    dialog.getHarness(MatFormFieldHarness.with({ floatingLabelText: label }));
  return {
    closed,
    dialog,
    rankField: await field('Rank'),
    titleField: await field('Duty title'),
    dateField: await field('Start date'),
    rank: (await (await field('Rank')).getControl(MatInputHarness))!,
    title: (await (await field('Duty title')).getControl(MatInputHarness))!,
    date: await dialog.getHarness(MatDatepickerInputHarness),
    save: await dialog.getHarness(MatButtonHarness.with({ text: 'Save' })),
    cancel: await dialog.getHarness(MatButtonHarness.with({ text: 'Cancel' })),
    isOpen: () => TestBed.inject(MatDialog).openDialogs.length > 0,
  };
}

describe('NewDutyDialog', () => {
  it("should be titled with the person's name and start with their current rank", async () => {
    const { dialog, rank } = await open(() => of(1));

    expect(await dialog.getTitleText()).toBe('New duty for John Doe');
    expect(await rank.getValue()).toBe('1LT');
  });

  it('should add the duty with trimmed values and the chosen date, and close', async () => {
    const addDuty = vi.fn(() => of(3));
    const { rank, title, date, save, closed } = await open(addDuty);

    await rank.setValue('  2LT ');
    await title.setValue('  Commander ');
    await date.setValue('6/2/2022');
    await save.click();

    expect(addDuty).toHaveBeenCalledWith({
      name: 'John Doe',
      rank: '2LT',
      dutyTitle: 'Commander',
      dutyStartDate: new Date(2022, 5, 2),
    });
    expect(await closed).toBe(true);
  });

  it('should require a rank, a duty title and a start date', async () => {
    const addDuty = vi.fn(() => of(3));
    const { rank, rankField, titleField, dateField, save, isOpen } = await open(addDuty);

    await rank.setValue('');
    await save.click();

    expect(await rankField.getTextErrors()).toEqual(['A rank is required.']);
    expect(await titleField.getTextErrors()).toEqual(['A duty title is required.']);
    expect(await dateField.getTextErrors()).toEqual(['A duty start date is required.']);
    expect(addDuty).not.toHaveBeenCalled();
    expect(isOpen()).toBe(true);
  });

  it('should not send a rank or duty title over 100 characters', async () => {
    const addDuty = vi.fn(() => of(3));
    const { rank, title, date, rankField, titleField, save } = await open(addDuty);

    await rank.setValue('x'.repeat(101));
    await title.setValue('y'.repeat(101));
    await date.setValue('6/2/2022');
    await save.click();

    expect(await rankField.getTextErrors()).toEqual([
      "A rank can't be longer than 100 characters.",
    ]);
    expect(await titleField.getTextErrors()).toEqual([
      "A duty title can't be longer than 100 characters.",
    ]);
    expect(addDuty).not.toHaveBeenCalled();
  });

  it('should only offer dates after the current duty started', async () => {
    const addDuty = vi.fn(() => of(3));
    const { title, date, dateField, save } = await open(addDuty);

    // The calendar, not the input's min attribute: Material's native adapter writes that attribute
    // from the date's UTC parts, so east of UTC it reads a day early.
    await date.setValue('6/3/2022');
    await date.openCalendar();
    const calendar = await date.getCalendar();
    const [june1] = await calendar.getCells({ text: '1' });
    const [june2] = await calendar.getCells({ text: '2' });
    expect(await june1.isDisabled()).toBe(true);
    expect(await june2.isDisabled()).toBe(false);

    await title.setValue('Commander');
    await date.setValue('6/1/2022');
    await save.click();

    expect(await dateField.getTextErrors()).toEqual([
      "A new duty for 'John Doe' must start after their current duty, which started 2022-06-01.",
    ]);
    expect(addDuty).not.toHaveBeenCalled();
  });

  it('should explain a start date it cannot read', async () => {
    const addDuty = vi.fn(() => of(3));
    const { title, date, dateField, save } = await open(addDuty);

    await title.setValue('Commander');
    await date.setValue('next tuesday');
    await save.click();

    expect(await dateField.getTextErrors()).toEqual([
      'Enter the date as month/day/year, for example 6/1/2022.',
    ]);
    expect(addDuty).not.toHaveBeenCalled();
  });

  it('should allow any date for someone with no duties yet', async () => {
    const addDuty = vi.fn(() => of(1));
    const { rank, title, date, save, closed } = await open(addDuty, {
      name: 'Zed Park',
      currentRank: null,
      currentDutyStartDate: null,
    });

    expect(await rank.getValue()).toBe('');
    expect(await date.getMin()).toBeNull();

    await rank.setValue('2LT');
    await title.setValue('Pilot');
    await date.setValue('3/15/1990');
    await save.click();

    expect(addDuty).toHaveBeenCalledWith({
      name: 'Zed Park',
      rank: '2LT',
      dutyTitle: 'Pilot',
      dutyStartDate: new Date(1990, 2, 15),
    });
    expect(await closed).toBe(true);
  });

  it('should warn that RETIRED ends the career', async () => {
    const { title, titleField } = await open(() => of(1));

    await title.setValue('retired');

    expect(await titleField.getTextHints()).toContain(
      "This ends John Doe's career. No duties can be added after it.",
    );
  });

  it("should show the API's rule message in the dialog and keep what was typed", async () => {
    const { title, date, dialog, save, isOpen } = await open(() =>
      throwError(
        () => new ApiError(409, "'John Doe' already has a Commander duty starting 2022-06-02."),
      ),
    );

    await title.setValue('Commander');
    await date.setValue('6/2/2022');
    await save.click();

    expect(await dialog.getContentText()).toContain(
      "'John Doe' already has a Commander duty starting 2022-06-02.",
    );
    expect(await title.getValue()).toBe('Commander');
    expect(isOpen()).toBe(true);
  });

  it('should not send twice while saving', async () => {
    const addDuty = vi.fn(() => new Subject<number>());
    const { title, date, save } = await open(addDuty);

    await title.setValue('Commander');
    await date.setValue('6/2/2022');
    await save.click();

    expect(await save.isDisabled()).toBe(true);
    await (await save.host()).click();
    expect(addDuty).toHaveBeenCalledTimes(1);
  });

  it('should close without saving on Cancel', async () => {
    const addDuty = vi.fn(() => of(1));
    const { cancel, closed } = await open(addDuty);

    await cancel.click();

    expect(await closed).toBeUndefined();
    expect(addDuty).not.toHaveBeenCalled();
  });
});
