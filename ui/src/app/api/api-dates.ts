import { IsoDate } from './api-models';

// Duty and career dates are calendar dates. Going through UTC (toISOString(), or new Date('2020-01-01'))
// moves them by a day in some time zones, so they're built from the local year, month and day, and kept as text.

export function toApiDate(date: Date): IsoDate {
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${date.getFullYear()}-${month}-${day}`;
}

/** The API sends a calendar date as midnight with no time zone, e.g. 2020-01-01T00:00:00. */
export function fromApiDate(value: string): IsoDate;
export function fromApiDate(value: string | null): IsoDate | null;
export function fromApiDate(value: string | null): IsoDate | null {
  return value === null ? null : value.slice(0, 10);
}
