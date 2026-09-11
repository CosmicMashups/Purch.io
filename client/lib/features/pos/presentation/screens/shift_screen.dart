import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/shift_models.dart';
import '../providers/shift_providers.dart';

/// D7 — opening/closing the device's cash-drawer session and reconciling the
/// actual count against what cash sales say should be in the drawer. A
/// mismatched count needs a manager/admin PIN to approve, entered right on
/// this screen rather than a separate confirmation step.
class ShiftScreen extends ConsumerWidget {
  const ShiftScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final shiftAsync = ref.watch(currentShiftNotifierProvider);

    return Scaffold(
      appBar: AppBar(title: const Text('Shift / Cash Drawer')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(24),
              child: shiftAsync.when(
                loading: () => const Center(child: CircularProgressIndicator()),
                error:
                    (error, stackTrace) =>
                        Center(child: Text('Could not load the shift: $error')),
                data: (shift) {
                  if (shift == null) {
                    return const _OpenShiftForm();
                  }
                  if (shift.status == ShiftStatus.closed) {
                    return _ClosedShiftSummary(shift: shift);
                  }
                  return _OpenShiftDetail(shift: shift);
                },
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _OpenShiftForm extends ConsumerStatefulWidget {
  const _OpenShiftForm();

  @override
  ConsumerState<_OpenShiftForm> createState() => _OpenShiftFormState();
}

class _OpenShiftFormState extends ConsumerState<_OpenShiftForm> {
  final _openingCashController = TextEditingController();

  @override
  void dispose() {
    _openingCashController.dispose();
    super.dispose();
  }

  Future<void> _open() async {
    final openingCash = double.tryParse(_openingCashController.text.trim());
    if (openingCash == null || openingCash < 0) {
      return;
    }

    await ref
        .read(currentShiftNotifierProvider.notifier)
        .openShift(OpenShiftRequest(openingCashAmount: openingCash));
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(currentShiftNotifierProvider);
    final isLoading = state.isLoading;
    final failure =
        ref.read(currentShiftNotifierProvider.notifier).currentFailure;
    final openingCash = double.tryParse(_openingCashController.text.trim());

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'No shift is open on this device.',
          style: Theme.of(context).textTheme.titleMedium,
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 24),
        TextField(
          controller: _openingCashController,
          enabled: !isLoading,
          decoration: const InputDecoration(
            labelText: 'Opening cash count',
            border: OutlineInputBorder(),
            prefixText: '₱ ',
          ),
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
          onChanged: (_) => setState(() {}),
        ),
        if (failure != null) ...[
          const SizedBox(height: 16),
          Text(
            failure.message,
            style: TextStyle(color: Theme.of(context).colorScheme.error),
            textAlign: TextAlign.center,
          ),
        ],
        const SizedBox(height: 24),
        SizedBox(
          height: 56,
          child: FilledButton(
            onPressed:
                !isLoading && openingCash != null && openingCash >= 0
                    ? _open
                    : null,
            child: const Text('Open Shift'),
          ),
        ),
      ],
    );
  }
}

class _OpenShiftDetail extends ConsumerStatefulWidget {
  const _OpenShiftDetail({required this.shift});

  final Shift shift;

  @override
  ConsumerState<_OpenShiftDetail> createState() => _OpenShiftDetailState();
}

class _OpenShiftDetailState extends ConsumerState<_OpenShiftDetail> {
  final _closingCashController = TextEditingController();
  final _handoverNotesController = TextEditingController();
  final _approverPinController = TextEditingController();

  @override
  void dispose() {
    _closingCashController.dispose();
    _handoverNotesController.dispose();
    _approverPinController.dispose();
    super.dispose();
  }

  Future<void> _close() async {
    final closingCash = double.tryParse(_closingCashController.text.trim());
    if (closingCash == null || closingCash < 0) {
      return;
    }

    await ref
        .read(currentShiftNotifierProvider.notifier)
        .closeShift(
          CloseShiftRequest(
            closingCashAmount: closingCash,
            handoverNotes:
                _handoverNotesController.text.trim().isEmpty
                    ? null
                    : _handoverNotesController.text.trim(),
            approverPin:
                _approverPinController.text.trim().isEmpty
                    ? null
                    : _approverPinController.text.trim(),
          ),
        );
  }

  @override
  Widget build(BuildContext context) {
    final state = ref.watch(currentShiftNotifierProvider);
    final isLoading = state.isLoading;
    final failure =
        ref.read(currentShiftNotifierProvider.notifier).currentFailure;
    final closingCash = double.tryParse(_closingCashController.text.trim());

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Shift open',
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                const SizedBox(height: 8),
                Text('Opened by: ${widget.shift.openedByUserName}'),
                Text(
                  'Opening cash: ₱${widget.shift.openingCashAmount.toStringAsFixed(2)}',
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 24),
        TextField(
          controller: _closingCashController,
          enabled: !isLoading,
          decoration: const InputDecoration(
            labelText: 'Closing cash count',
            border: OutlineInputBorder(),
            prefixText: '₱ ',
          ),
          keyboardType: const TextInputType.numberWithOptions(decimal: true),
          onChanged: (_) => setState(() {}),
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _handoverNotesController,
          enabled: !isLoading,
          decoration: const InputDecoration(
            labelText: 'Handover notes (optional)',
            border: OutlineInputBorder(),
          ),
          maxLines: 2,
        ),
        const SizedBox(height: 12),
        TextField(
          controller: _approverPinController,
          enabled: !isLoading,
          decoration: const InputDecoration(
            labelText: 'Manager/Admin PIN',
            helperText: 'Only needed if the cash count doesn\'t match.',
            border: OutlineInputBorder(),
          ),
          obscureText: true,
          keyboardType: TextInputType.number,
        ),
        if (failure != null) ...[
          const SizedBox(height: 16),
          Text(
            failure.message,
            style: TextStyle(color: Theme.of(context).colorScheme.error),
            textAlign: TextAlign.center,
          ),
        ],
        const SizedBox(height: 24),
        SizedBox(
          height: 56,
          child: FilledButton(
            onPressed:
                !isLoading && closingCash != null && closingCash >= 0
                    ? _close
                    : null,
            child: const Text('Close Shift'),
          ),
        ),
      ],
    );
  }
}

class _ClosedShiftSummary extends ConsumerWidget {
  const _ClosedShiftSummary({required this.shift});

  final Shift shift;

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final variance = shift.varianceAmount ?? 0;
    final varianceLabel =
        variance == 0
            ? 'Matched exactly'
            : variance > 0
            ? 'Over by ₱${variance.toStringAsFixed(2)}'
            : 'Short by ₱${(-variance).toStringAsFixed(2)}';

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          'Shift closed',
          style: Theme.of(context).textTheme.titleLarge,
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 16),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'Opening cash: ₱${shift.openingCashAmount.toStringAsFixed(2)}',
                ),
                Text(
                  'Expected cash: ₱${(shift.expectedCashAmount ?? 0).toStringAsFixed(2)}',
                ),
                Text(
                  'Actual cash: ₱${(shift.closingCashAmount ?? 0).toStringAsFixed(2)}',
                ),
                const SizedBox(height: 8),
                Text(
                  varianceLabel,
                  style: Theme.of(context).textTheme.titleMedium,
                ),
                if (shift.approvedByUserName != null)
                  Text('Approved by: ${shift.approvedByUserName}'),
                if (shift.handoverNotes != null) ...[
                  const SizedBox(height: 8),
                  Text('Notes: ${shift.handoverNotes}'),
                ],
              ],
            ),
          ),
        ),
        const SizedBox(height: 24),
        SizedBox(
          height: 56,
          child: FilledButton(
            onPressed:
                () =>
                    ref
                        .read(currentShiftNotifierProvider.notifier)
                        .acknowledgeClosedShift(),
            child: const Text('Done'),
          ),
        ),
      ],
    );
  }
}
