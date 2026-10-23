import { TestbedHarnessEnvironment } from '@angular/cdk/testing/testbed';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MatDialog } from '@angular/material/dialog';
import { MatButtonHarness } from '@angular/material/button/testing';
import { MatDialogHarness } from '@angular/material/dialog/testing';
import { MatFormFieldHarness } from '@angular/material/form-field/testing';
import { MatInputHarness } from '@angular/material/input/testing';
import { Observable, Subject, firstValueFrom, of, throwError } from 'rxjs';
import { ApiError } from '../../api/api-error';
import { PersonNameData, PersonNameDialog } from './person-name-dialog';

@Component({ template: '' })
class Host {}

/** The dialog as Possible Astronauts opens it. */
function open(createPerson: (name: string) => Observable<unknown>) {
  return openWith({ title: 'New person', name: '', save: createPerson });
}

/** The dialog as John Doe's timeline opens it. */
function openRename(renamePerson: (newName: string) => Observable<unknown>) {
  return openWith({ title: 'Rename John Doe', name: 'John Doe', save: renamePerson });
}

async function openWith(data: PersonNameData) {
  const fixture = TestBed.createComponent(Host);
  const dialogRef = TestBed.inject(MatDialog).open<PersonNameDialog, PersonNameData, string>(
    PersonNameDialog,
    { data },
  );
  const closed = firstValueFrom(dialogRef.afterClosed());
  const loader = TestbedHarnessEnvironment.documentRootLoader(fixture);
  const dialog = await loader.getHarness(MatDialogHarness);
  return {
    closed,
    dialog,
    name: await dialog.getHarness(MatInputHarness),
    nameField: await dialog.getHarness(MatFormFieldHarness),
    save: await dialog.getHarness(MatButtonHarness.with({ text: 'Save' })),
    cancel: await dialog.getHarness(MatButtonHarness.with({ text: 'Cancel' })),
    isOpen: () => TestBed.inject(MatDialog).openDialogs.length > 0,
  };
}

describe('PersonNameDialog for a new person', () => {
  it('should be titled New person', async () => {
    const { dialog } = await open(() => of(1));
    expect(await dialog.getTitleText()).toBe('New person');
  });

  it('should create the person with the name trimmed, and close with that name', async () => {
    const createPerson = vi.fn(() => of(7));
    const { name, save, closed } = await open(createPerson);

    await name.setValue('  John Doe  ');
    await save.click();

    expect(createPerson).toHaveBeenCalledWith('John Doe');
    expect(await closed).toBe('John Doe');
  });

  it('should not send a blank name', async () => {
    const createPerson = vi.fn(() => of(7));
    const { name, nameField, save, isOpen } = await open(createPerson);

    await name.setValue('   ');
    await save.click();

    expect(await nameField.getTextErrors()).toEqual(["A person's name is required."]);
    expect(createPerson).not.toHaveBeenCalled();
    expect(isOpen()).toBe(true);
  });

  it('should not send a name over 100 characters', async () => {
    const createPerson = vi.fn(() => of(7));
    const { name, nameField, save } = await open(createPerson);

    await name.setValue('x'.repeat(101));
    await save.click();

    expect(await nameField.getTextErrors()).toEqual([
      "A person's name can't be longer than 100 characters.",
    ]);
    expect(createPerson).not.toHaveBeenCalled();
  });

  it('should show a taken name under the field and keep the dialog open', async () => {
    const { name, nameField, save, isOpen } = await open(() =>
      throwError(() => new ApiError(409, "A person named 'John Doe' already exists.")),
    );

    await name.setValue('John Doe');
    await save.click();

    expect(await nameField.getTextErrors()).toEqual(["A person named 'John Doe' already exists."]);
    expect(isOpen()).toBe(true);
  });

  it('should save again once a taken name is changed', async () => {
    const createPerson = vi
      .fn()
      .mockReturnValueOnce(
        throwError(() => new ApiError(409, "A person named 'John Doe' already exists.")),
      )
      .mockReturnValueOnce(of(8));
    const { name, nameField, save, closed } = await open(createPerson);

    await name.setValue('John Doe');
    await save.click();
    await name.setValue('Jane Doe');

    expect(await nameField.getTextErrors()).toEqual([]);
    await save.click();
    expect(createPerson).toHaveBeenLastCalledWith('Jane Doe');
    expect(await closed).toBe('Jane Doe');
  });

  it('should show other problems in the dialog and keep what was typed', async () => {
    const { dialog, name, save, isOpen } = await open(() =>
      throwError(() => new ApiError(0, "Can't reach the Stargate API. Check that it's running.")),
    );

    await name.setValue('John Doe');
    await save.click();

    expect(await dialog.getContentText()).toContain(
      "Can't reach the Stargate API. Check that it's running.",
    );
    expect(await name.getValue()).toBe('John Doe');
    expect(isOpen()).toBe(true);
  });

  it('should not send twice while saving', async () => {
    const createPerson = vi.fn(() => new Subject<number>());
    const { name, save } = await open(createPerson);

    await name.setValue('John Doe');
    await save.click();

    expect(await save.isDisabled()).toBe(true);
    await (await save.host()).click();
    expect(createPerson).toHaveBeenCalledTimes(1);
  });

  it('should close without saving on Cancel', async () => {
    const createPerson = vi.fn(() => of(7));
    const { cancel, closed } = await open(createPerson);

    await cancel.click();

    expect(await closed).toBeUndefined();
    expect(createPerson).not.toHaveBeenCalled();
  });
});

describe('PersonNameDialog for a rename', () => {
  it('should start from the current name, selected so it can be typed over', async () => {
    const { dialog, name } = await openRename(() => of(1));

    expect(await dialog.getTitleText()).toBe('Rename John Doe');
    expect(await name.getValue()).toBe('John Doe');
    await name.focus();
    const input = await name.host();
    expect(await input.getProperty<number>('selectionStart')).toBe(0);
    expect(await input.getProperty<number>('selectionEnd')).toBe('John Doe'.length);
  });

  it('should save the new name trimmed, and close with it', async () => {
    const renamePerson = vi.fn(() => of(1));
    const { name, save, closed } = await openRename(renamePerson);

    await name.setValue('  Jane Doe ');
    await save.click();

    expect(renamePerson).toHaveBeenCalledWith('Jane Doe');
    expect(await closed).toBe('Jane Doe');
  });

  it('should close without saving when the name is unchanged', async () => {
    const renamePerson = vi.fn(() => of(1));
    const { name, save, closed } = await openRename(renamePerson);

    await name.setValue(' John Doe ');
    await save.click();

    expect(await closed).toBeUndefined();
    expect(renamePerson).not.toHaveBeenCalled();
  });

  it('should save a change of case, which the API allows', async () => {
    const renamePerson = vi.fn(() => of(1));
    const { name, save, closed } = await openRename(renamePerson);

    await name.setValue('john doe');
    await save.click();

    expect(renamePerson).toHaveBeenCalledWith('john doe');
    expect(await closed).toBe('john doe');
  });

  it('should say so when someone else renamed them first', async () => {
    const { dialog, name, save, isOpen } = await openRename(() =>
      throwError(() => new ApiError(404, "No person named 'John Doe' exists.")),
    );

    await name.setValue('Jane Doe');
    await save.click();

    expect(await dialog.getContentText()).toContain("No person named 'John Doe' exists.");
    expect(isOpen()).toBe(true);
  });
});
