export type ApiErrorKind =
  | 'validation'
  | 'unauthorized'
  | 'forbidden'
  | 'notFound'
  | 'conflict'
  | 'serviceUnavailable'
  | 'network'
  | 'unknown';

export class ApiError extends Error {
  kind: ApiErrorKind;
  fieldErrors?: Record<string, string[]>;

  constructor(kind: ApiErrorKind, message: string, fieldErrors?: Record<string, string[]>) {
    super(message);
    this.kind = kind;
    this.fieldErrors = fieldErrors;
  }
}
