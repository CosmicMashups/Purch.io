/// Uniform error type for the whole app. Mirrors the backend's ProblemDetails
/// taxonomy (see backend/src/Purch.Api/ErrorHandling/GlobalExceptionHandler.cs)
/// so a 400/404/409/403/503/500 from the API becomes a specific, handleable
/// case here instead of every screen having to inspect raw HTTP status codes.
///
/// A `sealed class` in Dart works like the discriminated unions on the backend
/// (`LoginResult`): the compiler forces an exhaustive `switch` over every
/// subtype below, so a new Failure case can't be silently unhandled by a screen.
sealed class Failure {
  const Failure(this.message);

  final String message;
}

/// 400 — the request itself was invalid, with per-field detail (from a
/// ValidationProblemDetails "errors" object).
final class ValidationFailure extends Failure {
  const ValidationFailure(super.message, this.fieldErrors);

  final Map<String, List<String>> fieldErrors;
}

/// 401 — not authenticated, or credentials were rejected.
final class UnauthorizedFailure extends Failure {
  const UnauthorizedFailure(super.message);
}

/// 403 — authenticated, but not permitted to do this (wrong Role/scope).
final class ForbiddenFailure extends Failure {
  const ForbiddenFailure(super.message);
}

/// 404 — the requested resource doesn't exist.
final class NotFoundFailure extends Failure {
  const NotFoundFailure(super.message);
}

/// 409 — conflicts with existing state (e.g. a duplicate).
final class ConflictFailure extends Failure {
  const ConflictFailure(super.message);
}

/// 503 — the API couldn't reach its own database. Distinct from NetworkFailure:
/// the device *did* reach the server, the server just couldn't finish the job.
final class ServiceUnavailableFailure extends Failure {
  const ServiceUnavailableFailure(super.message);
}

/// The device never reached the server at all (no connectivity, DNS failure,
/// connection refused) — as opposed to the server responding with an error.
final class NetworkFailure extends Failure {
  const NetworkFailure(super.message);
}

/// 500, or any other unexpected shape — a real bug, not a user-fixable outcome.
final class UnknownFailure extends Failure {
  const UnknownFailure(super.message);
}
