import { Component, computed, inject, signal } from '@angular/core';
import { FormField, FormRoot, form, validate } from '@angular/forms/signals';
import { MatButton } from '@angular/material/button';
import { provideNativeDateAdapter } from '@angular/material/core';
import {
  MatDatepicker,
  MatDatepickerInput,
  MatDatepickerToggle,
} from '@angular/material/datepicker';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatError, MatFormField, MatHint, MatLabel, MatSuffix } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { firstValueFrom } from 'rxjs';
import { toApiDate } from '../../api/api-dates';
import { errorMessage } from '../../api/api-error';
import { IsoDate } from '../../api/api-models';
import { StargateApi } from '../../api/stargate-api';
import { dutyFieldError, isRetirement, maxDutyFieldLength } from '../duty-rules';

export interface NewDutyData {
  name: string;
  currentRank: string | null;
  currentDutyStartDate: IsoDate | null;
}

interface DutyModel {
  rank: string;
  dutyTitle: string;
  dutyStartDate: Date | null;
}

/** Asks for a person's next duty and adds it. Closes with true once it's saved. */
@Component({
  imports: [
    FormField,
    FormRoot,
    MatButton,
    MatDatepicker,
    MatDatepickerInput,
    MatDatepickerToggle,
    MatDialogActions,
    MatDialogContent,
    MatDialogTitle,
    MatError,
    MatFormField,
    MatHint,
    MatInput,
    MatLabel,
    MatSuffix,
  ],
  providers: [provideNativeDateAdapter()],
  selector: 'app-new-duty-dialog',
  styleUrl: './new-duty-dialog.scss',
  templateUrl: './new-duty-dialog.html',
})
export class NewDutyDialog {
  private readonly api = inject(StargateApi);
  private readonly dialogRef = inject<MatDialogRef<NewDutyDialog, boolean>>(MatDialogRef);
  protected readonly data = inject<NewDutyData>(MAT_DIALOG_DATA);

  protected readonly maxLength = maxDutyFieldLength;
  protected readonly isRetirement = isRetirement;
  // The API only accepts a duty that starts after the current one did, so the calendar starts the day after.
  protected readonly minStartDate =
    this.data.currentDutyStartDate === null ? null : dayAfter(this.data.currentDutyStartDate);
  protected readonly problem = signal<string | null>(null);

  protected readonly duty = form(
    // Most new duties keep the person's rank.
    signal<DutyModel>({ rank: this.data.currentRank ?? '', dutyTitle: '', dutyStartDate: null }),
    (duty) => {
      validate(duty.rank, ({ value }) => asError(dutyFieldError(value(), 'rank')));
      validate(duty.dutyTitle, ({ value }) => asError(dutyFieldError(value(), 'duty title')));
      validate(duty.dutyStartDate, ({ value }) => asError(this.startDateError(value())));
    },
    {
      submission: {
        action: async (duty) => {
          this.problem.set(null);
          const { rank, dutyTitle, dutyStartDate } = duty().value();
          try {
            await firstValueFrom(
              this.api.addDuty({
                name: this.data.name,
                rank: rank.trim(),
                dutyTitle: dutyTitle.trim(),
                // Validation has already required a date.
                dutyStartDate: dutyStartDate!,
              }),
            );
          } catch (error) {
            // The rules only the API checks (a duplicate duty, RETIRED as a first duty) come back as messages.
            this.problem.set(errorMessage(error));
            return undefined;
          }
          this.dialogRef.close(true);
          return undefined;
        },
      },
    },
  );

  // The date picker adds its own errors, without messages: matDatepickerMin repeats the check above,
  // and matDatepickerParse means the typed text isn't a date (the value is then empty, which would
  // otherwise read as "required").
  protected readonly startDateErrors = computed(() => {
    const errors = this.duty.dutyStartDate().errors();
    if (errors.some((error) => error.kind === 'matDatepickerParse')) {
      return ['Enter the date as month/day/year, for example 6/1/2022.'];
    }
    return errors.flatMap((error) => (error.message ? [error.message] : []));
  });

  protected cancel(): void {
    this.dialogRef.close();
  }

  private startDateError(date: Date | null): string | null {
    if (date === null) return 'A duty start date is required.';
    const current = this.data.currentDutyStartDate;
    if (current !== null && toApiDate(date) <= current) {
      return `A new duty for '${this.data.name}' must start after their current duty, which started ${current}.`;
    }
    return null;
  }
}

function asError(message: string | null) {
  return message === null ? undefined : { kind: 'rule', message };
}

function dayAfter(date: IsoDate): Date {
  const [year, month, day] = date.split('-').map(Number);
  return new Date(year, month - 1, day + 1);
}
