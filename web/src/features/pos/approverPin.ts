import { ApiError } from '../../lib/apiError';

/**
 * Whether a failed cart action needs a manager/admin PIN to proceed (a missing PIN, a wrong one, or one
 * that belongs to the same account asking) — see the backend's ApproverAuthorizationService. Distinct from
 * a lockout (too many wrong PINs at this terminal), which is a plain ForbiddenException and shown as any
 * other error instead of reopening the dialog.
 */
export function needsApproverPin(error: unknown): boolean {
  return error instanceof ApiError && error.kind === 'validation' && !!error.fieldErrors?.approverPin;
}

/** The one sentence to show under the PIN field: the server's own reason, or a first-ask prompt when there isn't one yet. */
export function approverPinPrompt(error: unknown): string {
  if (error instanceof ApiError && error.fieldErrors?.approverPin?.[0]) {
    return error.fieldErrors.approverPin[0];
  }
  return 'Enter a manager or admin PIN to approve this.';
}
