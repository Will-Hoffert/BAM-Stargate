import { DatePipe } from '@angular/common';
import { Component, computed, inject, input } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { MatButton } from '@angular/material/button';
import { RouterLink } from '@angular/router';
import { toApiDate } from '../api/api-dates';
import { ApiError, errorMessage } from '../api/api-error';
import { AstronautDuty, IsoDate } from '../api/api-models';
import { StargateApi } from '../api/stargate-api';
import { personStatus } from '../people/person-status';
import { StatusPill } from '../people/status-pill/status-pill';
import { formatDuration } from './duration';

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

  protected isNotFound(error: Error): boolean {
    return error instanceof ApiError && error.status === 404;
  }
}

function today(): IsoDate {
  return toApiDate(new Date());
}

// The API stores RETIRED in capitals but recognizes it in any case, so this does too.
function isRetirement(dutyTitle: string): boolean {
  return dutyTitle.toUpperCase() === 'RETIRED';
}
