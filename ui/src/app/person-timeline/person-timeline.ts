import { DatePipe } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButton } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { Router, RouterLink } from '@angular/router';
import { toApiDate } from '../api/api-dates';
import { ApiError, errorMessage } from '../api/api-error';
import { AstronautDuty, IsoDate } from '../api/api-models';
import { StargateApi } from '../api/stargate-api';
import { PersonNameData, PersonNameDialog } from '../people/person-name-dialog/person-name-dialog';
import { personStatus } from '../people/person-status';
import { StatusPill } from '../people/status-pill/status-pill';
import { isRetirement } from './duty-rules';
import { formatDuration } from './duration';
import { NewDutyData, NewDutyDialog } from './new-duty-dialog/new-duty-dialog';

interface TimelineEntry extends AstronautDuty {
  kind: 'current' | 'past' | 'retirement';
  duration: string | null;
}

@Component({
  imports: [DatePipe, MatButton, RouterLink, StatusPill],
  selector: 'app-person-timeline',
  styleUrl: './person-timeline.scss',
  templateUrl: './person-timeline.html',
})
export class PersonTimeline {
  private readonly api = inject(StargateApi);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  /** From the URL, e.g. /people/John%20Doe. */
  readonly name = input.required<string>();

  protected readonly career = rxResource({
    params: () => this.name(),
    stream: ({ params: name }) => this.api.getDuties(name),
  });
  protected readonly errorMessage = errorMessage;

  protected readonly status = computed(() =>
    this.career.hasValue() ? personStatus(this.career.value().person) : null,
  );

  protected readonly careerLength = computed(() => {
    if (!this.career.hasValue()) return null;
    const { careerStartDate, careerEndDate } = this.career.value().person;
    return careerStartDate === null
      ? null
      : formatDuration(careerStartDate, careerEndDate ?? today());
  });

  // The RETIRED duty marks the end of a career rather than a duty with a length,
  // so it isn't highlighted as current either.
  protected readonly entries = computed(() => {
    if (!this.career.hasValue()) return [];
    return this.career.value().duties.map((duty): TimelineEntry =>
      isRetirement(duty.dutyTitle)
        ? { ...duty, kind: 'retirement', duration: null }
        : {
            ...duty,
            kind: duty.dutyEndDate === null ? 'current' : 'past',
            duration: formatDuration(duty.dutyStartDate, duty.dutyEndDate ?? today()),
          },
    );
  });

  // RETIRED is final: the API refuses any duty after it, so the page doesn't offer one.
  protected readonly retiredOn = computed(
    () => this.entries().find((entry) => entry.kind === 'retirement')?.dutyStartDate ?? null,
  );

  protected isNotFound(error: Error): boolean {
    return error instanceof ApiError && error.status === 404;
  }

  protected rename(): void {
    if (!this.career.hasValue()) return;
    const name = this.career.value().person.name;
    this.dialog
      .open<PersonNameDialog, PersonNameData, string>(PersonNameDialog, {
        width: '400px',
        data: {
          title: `Rename ${name}`,
          name,
          save: (newName) => this.api.renamePerson(name, newName),
        },
      })
      .afterClosed()
      .subscribe((newName) => {
        // The old address no longer exists, so it's replaced in the history and Back skips it.
        if (newName !== undefined) {
          void this.router.navigate(['/people', newName], { replaceUrl: true });
        }
      });
  }

  protected addDuty(): void {
    if (!this.career.hasValue()) return;
    const { person, duties } = this.career.value();
    this.dialog
      .open<NewDutyDialog, NewDutyData, boolean>(NewDutyDialog, {
        width: '440px',
        data: {
          name: person.name,
          currentRank: person.currentRank,
          // The API lists duties newest first.
          currentDutyStartDate: duties[0]?.dutyStartDate ?? null,
        },
      })
      .afterClosed()
      .subscribe((saved) => {
        if (saved) this.career.reload();
      });
  }
}

function today(): IsoDate {
  return toApiDate(new Date());
}
