import { ApiError, errorMessage } from './api-error';

describe('errorMessage', () => {
  it("should show an API error's own message", () => {
    expect(errorMessage(new ApiError(409, "A person named 'John Doe' already exists."))).toBe(
      "A person named 'John Doe' already exists.",
    );
  });

  it('should not show the details of an unexpected error', () => {
    expect(
      errorMessage(new TypeError("Cannot read properties of undefined (reading 'people')")),
    ).toBe('Something went wrong. Please try again.');
  });
});
