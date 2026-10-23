import { Component, inject, signal } from '@angular/core';
import { FormField, FormRoot, form, validate } from '@angular/forms/signals';
import { MatButton } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { Observable, firstValueFrom } from 'rxjs';
import { ApiError, errorMessage } from '../../api/api-error';
import { maxNameLength, nameError } from '../person-rules';

export interface PersonNameData {
  /** For example "New person" or "Rename John Doe". */
  title: string;
  /** The name to start from: empty for a new person, the current name for a rename. */
  name: string;
  /** Saves the trimmed name. A failure is an ApiError. */
  save: (name: string) => Observable<unknown>;
}

/**
 * Asks for a person's name, for a new person or a rename, using the API's name rules.
 * Closes with the saved name, or with nothing when cancelled or left unchanged.
 */
@Component({
  imports: [
    FormField,
    FormRoot,
    MatButton,
    MatDialogActions,
    MatDialogContent,
    MatDialogTitle,
    MatError,
    MatFormField,
    MatHint,
    MatInput,
    MatLabel,
  ],
  selector: 'app-person-name-dialog',
  styleUrl: './person-name-dialog.scss',
  templateUrl: './person-name-dialog.html',
})
export class PersonNameDialog {
  private readonly dialogRef = inject<MatDialogRef<PersonNameDialog, string>>(MatDialogRef);
  protected readonly data = inject<PersonNameData>(MAT_DIALOG_DATA);

  protected readonly maxNameLength = maxNameLength;
  protected readonly problem = signal<string | null>(null);

  protected readonly person = form(
    signal({ name: this.data.name }),
    (person) => {
      validate(person.name, ({ value }) => {
        const message = nameError(value());
        return message === null ? undefined : { kind: 'name', message };
      });
    },
    {
      submission: {
        action: async (person) => {
          this.problem.set(null);
          const name = person.name().value().trim();
          // Renaming someone to exactly the name they have changes nothing.
          if (name === this.data.name) {
            this.dialogRef.close();
            return undefined;
          }
          try {
            await firstValueFrom(this.data.save(name));
          } catch (error) {
            // A taken name is for the user to fix, so it shows under the field.
            if (error instanceof ApiError && error.status === 409) {
              return { kind: 'taken', message: error.message, fieldTree: person.name };
            }
            this.problem.set(errorMessage(error));
            return undefined;
          }
          this.dialogRef.close(name);
          return undefined;
        },
      },
    },
  );

  // Closes with no result. A bare mat-dialog-close attribute would close with '' instead.
  protected cancel(): void {
    this.dialogRef.close();
  }
}
