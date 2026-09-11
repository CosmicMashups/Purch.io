/// Mirrors backend/src/Purch.Application/Sync/SyncDto.cs — keep in sync with
/// that file's shape.
class SyncItemRequest {
  SyncItemRequest({
    required this.idempotencyKey,
    required this.entityType,
    required this.entityId,
    required this.clientTimestamp,
    required this.payloadJson,
  });

  final String idempotencyKey;
  final String entityType;
  final String entityId;
  final DateTime clientTimestamp;
  final String payloadJson;

  Map<String, dynamic> toJson() => {
    'idempotencyKey': idempotencyKey,
    'entityType': entityType,
    'entityId': entityId,
    'clientTimestamp': clientTimestamp.toUtc().toIso8601String(),
    'payloadJson': payloadJson,
  };
}

enum SyncItemStatus { applied, alreadySynced, conflictFlagged }

SyncItemStatus _syncItemStatusFromJson(String value) {
  switch (value) {
    case 'Applied':
      return SyncItemStatus.applied;
    case 'AlreadySynced':
      return SyncItemStatus.alreadySynced;
    case 'ConflictFlagged':
      return SyncItemStatus.conflictFlagged;
    default:
      throw ArgumentError('Unknown SyncItemStatus: $value');
  }
}

class SyncItemResult {
  SyncItemResult({
    required this.idempotencyKey,
    required this.status,
    required this.message,
  });

  factory SyncItemResult.fromJson(Map<String, dynamic> json) {
    return SyncItemResult(
      idempotencyKey: json['idempotencyKey'] as String,
      status: _syncItemStatusFromJson(json['status'] as String),
      message: json['message'] as String,
    );
  }

  final String idempotencyKey;
  final SyncItemStatus status;
  final String message;
}

class SyncBatchResult {
  SyncBatchResult({required this.results});

  factory SyncBatchResult.fromJson(Map<String, dynamic> json) {
    return SyncBatchResult(
      results:
          (json['results'] as List<dynamic>)
              .cast<Map<String, dynamic>>()
              .map(SyncItemResult.fromJson)
              .toList(),
    );
  }

  final List<SyncItemResult> results;
}

class FlaggedSyncRecord {
  FlaggedSyncRecord({
    required this.id,
    required this.deviceId,
    required this.entityType,
    required this.entityId,
    required this.clientTimestamp,
    required this.reviewedAt,
  });

  factory FlaggedSyncRecord.fromJson(Map<String, dynamic> json) {
    return FlaggedSyncRecord(
      id: json['id'] as String,
      deviceId: json['deviceId'] as String,
      entityType: json['entityType'] as String,
      entityId: json['entityId'] as String,
      clientTimestamp: DateTime.parse(json['clientTimestamp'] as String),
      reviewedAt:
          json['reviewedAt'] == null
              ? null
              : DateTime.parse(json['reviewedAt'] as String),
    );
  }

  final String id;
  final String deviceId;
  final String entityType;
  final String entityId;
  final DateTime clientTimestamp;
  final DateTime? reviewedAt;
}
