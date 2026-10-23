import { fromApiDate, toApiDate } from './api-dates';

describe('toApiDate', () => {
  // toISOString() would send the previous day in time zones ahead of UTC.
  it('should send the local calendar date at midnight', () => {
    expect(toApiDate(new Date(2020, 0, 1))).toBe('2020-01-01');
  });

  // toISOString() would send the next day in time zones behind UTC.
  it('should send the local calendar date late in the evening', () => {
    expect(toApiDate(new Date(2024, 11, 31, 23, 30))).toBe('2024-12-31');
  });
});

describe('fromApiDate', () => {
  it('should keep only the calendar date the API sent', () => {
    expect(fromApiDate('2020-01-01T00:00:00')).toBe('2020-01-01');
  });

  it('should keep a missing date missing', () => {
    expect(fromApiDate(null)).toBeNull();
  });
});
