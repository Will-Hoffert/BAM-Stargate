import { IsoDate } from '../api/api-models';

/**
 * How long a span of calendar days lasted, in whole years and months, counting its last day:
 * 2020-01-01 to 2022-05-31 is "2 years 5 months", because the next duty starts on June 1.
 */
export function formatDuration(start: IsoDate, lastDay: IsoDate): string {
  if (lastDay < start) return 'Not started yet';

  const [startYear, startMonth, startDay] = parts(start);
  const [endYear, endMonth, endDay] = dayAfter(lastDay);
  let months = (endYear - startYear) * 12 + (endMonth - startMonth);
  if (endDay < startDay) months -= 1;

  if (months === 0) return 'Less than a month';
  return [plural(Math.floor(months / 12), 'year'), plural(months % 12, 'month')]
    .filter((part) => part !== '')
    .join(' ');
}

function parts(date: IsoDate): [number, number, number] {
  const [year, month, day] = date.split('-').map(Number);
  return [year, month, day];
}

// Date.UTC handles month ends and leap years, with no time zone involved.
function dayAfter(date: IsoDate): [number, number, number] {
  const [year, month, day] = parts(date);
  const next = new Date(Date.UTC(year, month - 1, day + 1));
  return [next.getUTCFullYear(), next.getUTCMonth() + 1, next.getUTCDate()];
}

function plural(count: number, unit: string): string {
  if (count === 0) return '';
  return `${count} ${unit}${count === 1 ? '' : 's'}`;
}
