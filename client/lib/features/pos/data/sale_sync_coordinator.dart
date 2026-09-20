import 'dart:async';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/foundation.dart';

import '../../../core/errors/failure.dart';
import '../domain/pos_repository.dart';
import 'sale_queue.dart';

/// Sends sales that were completed offline to the server, oldest first, through
/// the same idempotent checkout call an online sale uses.
///
/// Every sale carries its own sale id and receipt number, so sending one twice
/// (a retry after a dropped response, or a sale the server had in fact already
/// received when the terminal thought it was offline) just returns the sale the
/// server already holds. Nothing is ever deleted from the queue until the
/// server has acknowledged it.
class SaleSyncCoordinator {
  SaleSyncCoordinator({
    required SaleQueueStore store,
    required PosRepository remote,
    required void Function() onSynced,
    Connectivity? connectivity,
    this.retryEvery = const Duration(seconds: 60),
  }) : _store = store,
       _remote = remote,
       _onSynced = onSynced,
       _connectivity = connectivity;

  final SaleQueueStore _store;
  final PosRepository _remote;
  final void Function() _onSynced;
  final Connectivity? _connectivity;
  final Duration retryEvery;

  /// How many times a sale may hit a server error before it is set aside.
  static const maxServerErrorAttempts = 10;

  StreamSubscription<List<ConnectivityResult>>? _subscription;
  Timer? _timer;
  bool _draining = false;

  /// Begins watching: sends whatever is waiting now, again whenever the
  /// connection returns, and on a timer as a backstop (a connection can be
  /// "up" and still not reach the server).
  void start() {
    unawaited(_startUp());
    _subscription ??= _connectivity?.onConnectivityChanged.listen((results) {
      if (!results.contains(ConnectivityResult.none)) {
        unawaited(drain());
      }
    });
    _timer ??= Timer.periodic(retryEvery, (_) => unawaited(drain()));
  }

  Future<void> _startUp() async {
    // Rows left mid-send by a crash were never acknowledged; try them again.
    await _store.resetStuck();
    await _store.purgeOldSynced(const Duration(days: 7));
    await drain();
  }

  void dispose() {
    unawaited(_subscription?.cancel());
    _subscription = null;
    _timer?.cancel();
    _timer = null;
  }

  /// One pass over the queue. Safe to call at any time and from anywhere
  /// (e.g. right after a sale is queued, or from a "Sync now" button).
  Future<void> drain() async {
    if (_draining) {
      return;
    }
    _draining = true;
    try {
      final pending = await _store.pending();
      var anySynced = false;
      for (final sale in pending) {
        await _store.markSyncing(sale.saleId);
        try {
          await _remote.checkout(sale.request);
          await _store.markSynced(sale.saleId);
          anySynced = true;
        } on Failure catch (failure) {
          // A server error that repeats and repeats is a fault in the sale (or the
          // server), not a passing outage — stop letting it hold up every sale
          // behind it and put it in front of a person instead.
          final stuck = failure is UnknownFailure && sale.attempts >= maxServerErrorAttempts;
          if (!stuck && _shouldRetryLater(failure)) {
            // Nothing wrong with the sale — the server just can't be reached
            // right now. Keep it, and stop: the rest would fail the same way.
            await _store.markPending(sale.saleId, error: failure.message);
            break;
          }
          // The server refused this sale for good. The customer already has a
          // receipt, so it is set aside for a person to look at — never dropped,
          // and it doesn't hold up the sales behind it.
          await _store.markRejected(sale.saleId, failure.message);
        } on Object catch (error) {
          await _store.markPending(sale.saleId, error: '$error');
          break;
        }
      }
      if (anySynced) {
        _onSynced();
      }
    } finally {
      _draining = false;
    }
  }

  /// Failures that say nothing about the sale itself. An expired session
  /// counts: the sale is fine, it just can't be sent until someone signs in.
  @visibleForTesting
  static bool isTransient(Failure failure) => _shouldRetryLater(failure);

  static bool _shouldRetryLater(Failure failure) =>
      // The cashier is midway through a claimed kiosk order; once they finish or void it the queued
      // sale goes through, so it isn't refused for good. Other conflicts (a taken receipt number, a
      // sale id used elsewhere) will never succeed and still set the sale aside.
      (failure is ConflictFailure && failure.message.contains('claimed kiosk order')) ||
      failure is NetworkFailure ||
      failure is ServiceUnavailableFailure ||
      failure is UnknownFailure ||
      failure is UnauthorizedFailure;
}
