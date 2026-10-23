import { Component, inject, signal } from '@angular/core';
import { FormField, FormRoot, form, validate } from '@angular/forms/signals';
import { MatButton } from '@angular/material/button';
import {
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { firstValueFrom } from 'rxjs';
import { ApiError, errorMessage } from '../../api/api-error';
import { StargateApi } from '../../api/stargate-api';
import { maxNameLength, nameError } from '../person-rules';

/** Asks for a new person's name and creates them. Closes with the name that was saved. */
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
  selector: 'app-new-person-dialog',
  styleUrl: './new-person-dialog.scss',
  templateUrl: './new-person-dialog.html',
})
export class NewPersonDialog {
  private readonly api = inject(StargateApi);
  private readonly dialogRef = inject<MatDialogRef<NewPersonDialog, string>>(MatDialogRef);

  protected readonly maxNameLength = maxNameLength;
  protected readonly problem = signal<string | null>(null);

  protected readonly person = form(
    signal({ name: '' }),
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
          try {
            await firstValueFrom(this.api.createPerson(name));
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
