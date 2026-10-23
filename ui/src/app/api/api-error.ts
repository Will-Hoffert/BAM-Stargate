import { HttpErrorResponse } from '@angular/common/http';

/** A failed API call, with a message that can be shown to the user as it is. */
export class ApiError extends Error {
  override readonly name = 'ApiError';

  constructor(
    readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

export function toApiError(error: HttpErrorResponse): ApiError {
  // Status 0 means no response: the API isn't running, or the browser blocked the call (CORS) and won't say why.
  if (error.status === 0) {
    return new ApiError(0, "Can't reach the Stargate API. Check that it's running.");
  }

  // Every error the API sends itself carries a message written for people.
  const message: unknown = error.error?.message;
  if (typeof message === 'string' && message.trim() !== '') {
    return new ApiError(error.status, message);
  }

  // Anything else (an error from ASP.NET itself, a proxy's error page) has no message to show.
  return new ApiError(
    error.status,
    `The Stargate API sent an unexpected response (${error.status}).`,
  );
}
