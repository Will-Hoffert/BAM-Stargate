import { nameError } from './person-rules';

describe('nameError', () => {
  it('should accept a name of up to 100 characters, ignoring surrounding spaces', () => {
    expect(nameError('John Doe')).toBeNull();
    expect(nameError(` ${'x'.repeat(100)} `)).toBeNull();
  });

  it('should require a name', () => {
    expect(nameError('')).toBe("A person's name is required.");
    expect(nameError('   ')).toBe("A person's name is required.");
  });

  it('should reject a name over 100 characters', () => {
    expect(nameError('x'.repeat(101))).toBe("A person's name can't be longer than 100 characters.");
  });
});
