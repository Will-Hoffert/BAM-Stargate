import { formatDuration } from './duration';

describe('formatDuration', () => {
  it('should count whole years and months, including the last day', () => {
    expect(formatDuration('2020-01-01', '2022-05-31')).toBe('2 years 5 months');
  });

  it('should use the singular for one year or one month', () => {
    expect(formatDuration('2020-01-01', '2020-12-31')).toBe('1 year');
    expect(formatDuration('2020-01-01', '2020-01-31')).toBe('1 month');
    expect(formatDuration('2020-01-01', '2021-01-31')).toBe('1 year 1 month');
  });

  it('should say less than a month for shorter spans, including a one-day duty', () => {
    expect(formatDuration('2020-01-01', '2020-01-30')).toBe('Less than a month');
    expect(formatDuration('2020-01-01', '2020-01-01')).toBe('Less than a month');
  });

  it('should know how long February is in a leap year', () => {
    expect(formatDuration('2020-02-01', '2020-02-29')).toBe('1 month');
    expect(formatDuration('2020-02-01', '2020-02-28')).toBe('Less than a month');
    expect(formatDuration('2021-02-01', '2021-02-28')).toBe('1 month');
  });

  it('should count across the end of a year', () => {
    expect(formatDuration('2023-11-15', '2024-02-14')).toBe('3 months');
  });

  it('should say not started yet when the start is after the last day', () => {
    expect(formatDuration('2027-03-01', '2026-10-23')).toBe('Not started yet');
  });
});
