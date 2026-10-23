import { dutyFieldError, isRetirement } from './duty-rules';

describe('dutyFieldError', () => {
  it('should accept up to 100 characters, ignoring surrounding spaces', () => {
    expect(dutyFieldError('1LT', 'rank')).toBeNull();
    expect(dutyFieldError(` ${'x'.repeat(100)} `, 'duty title')).toBeNull();
  });

  it('should require a value', () => {
    expect(dutyFieldError('', 'rank')).toBe('A rank is required.');
    expect(dutyFieldError('   ', 'duty title')).toBe('A duty title is required.');
  });

  it('should reject a value over 100 characters', () => {
    expect(dutyFieldError('x'.repeat(101), 'duty title')).toBe(
      "A duty title can't be longer than 100 characters.",
    );
  });
});

describe('isRetirement', () => {
  it('should recognize RETIRED in any case, as the API does', () => {
    expect(isRetirement('RETIRED')).toBe(true);
    expect(isRetirement('retired')).toBe(true);
    expect(isRetirement(' Retired ')).toBe(true);
    expect(isRetirement('Retired Commander')).toBe(false);
  });
});
