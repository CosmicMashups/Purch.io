export type ApiErrorKind =
  | 'validation'
  | 'unauthorized'
  | 'forbidden'
  | 'notFound'
  | 'conflict'
  | 'serviceUnavailable'
  | 'rateLimited'
  | 'network'
  | 'unknown';

const GENERIC_MESSAGES: Record<Exclude<ApiErrorKind, 'validation' | 'conflict'>, string> = {
  unauthorized: 'Your session has ended. Please sign in again.',
  forbidden: 'You do not have permission to do that.',
  notFound: 'That record could not be found. It may have been removed.',
  serviceUnavailable: 'The server is busy. Please wait a moment and try again.',
  rateLimited: 'Too many attempts. Please wait a few minutes and try again.',
  network: 'Cannot reach the server. Check the connection and try again.',
  unknown: 'Something went wrong. Please try again.',
};

/**
 * The sentence shown to staff. Validation and conflict messages are written for users by the API's
 * business rules, so they pass through. Everything else is a fixed message, so raw server or
 * framework text never reaches the screen.
 */
export function userMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.kind === 'validation' || error.kind === 'conflict') return error.message;
    return GENERIC_MESSAGES[error.kind];
  }
  return GENERIC_MESSAGES.unknown;
}

export class ApiError extends Error {
  kind: ApiErrorKind;
  fieldErrors?: Record<string, string[]>;

  constructor(kind: ApiErrorKind, message: string, fieldErrors?: Record<string, string[]>) {
    super(message);
    this.kind = kind;
    this.fieldErrors = fieldErrors;
  }
}
