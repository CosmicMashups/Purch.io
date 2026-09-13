import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/shift_models.dart';
import '../providers/shift_providers.dart';
import '../../../../core/errors/failure.dart';

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
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Shift / Cash Drawer')),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 500),
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 24),
              child: shiftAsync.when(
                loading: () => const Center(child: CircularProgressIndicator()),
                error:
                    (error, stackTrace) => Center(
                      child: Text(
                        'Could not load the shift: ${describeError(error)}',
                        style: const TextStyle(color: AppColors.error),
                      ),
                    ),
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

    return Container(
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: AppRadius.xlBorder,
        boxShadow: AppShadows.card,
        border: Border.all(color: AppColors.border),
      ),
      padding: const EdgeInsets.all(28),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Center(
            child: Container(
              width: 56,
              height: 56,
              decoration: BoxDecoration(
                color: AppColors.brandPrimaryContainer,
                borderRadius: BorderRadius.circular(AppRadius.lg),
              ),
              child: const Icon(
                Icons.point_of_sale_rounded,
                color: AppColors.brandPrimary,
                size: 28,
              ),
            ),
          ),
          const SizedBox(height: 16),
          const Text(
            'No shift is open on this device.',
            style: TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w700,
              color: AppColors.textPrimary,
            ),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 6),
          const Text(
            'Count the cash in your drawer and enter the opening float to start your shift.',
            style: TextStyle(fontSize: 13, color: AppColors.textSecondary),
            textAlign: TextAlign.center,
          ),
          const SizedBox(height: 24),
          TextField(
            controller: _openingCashController,
            enabled: !isLoading,
            decoration: const InputDecoration(
              labelText: 'Opening cash count',
              prefixText: '₱ ',
              prefixIcon: Icon(Icons.payments_outlined, size: 20),
            ),
            keyboardType: const TextInputType.numberWithOptions(decimal: true),
            onChanged: (_) => setState(() {}),
          ),
          if (failure != null) ...[
            const SizedBox(height: 16),
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: AppColors.error.withAlpha(20),
                borderRadius: AppRadius.smBorder,
                border: Border.all(color: AppColors.error.withAlpha(60)),
              ),
              child: Text(
                failure.message,
                style: const TextStyle(
                  color: AppColors.error,
                  fontSize: 13,
                  fontWeight: FontWeight.w500,
                ),
                textAlign: TextAlign.center,
              ),
            ),
          ],
          const SizedBox(height: 20),
          SizedBox(
            height: 50,
            child: FilledButton(
              style: FilledButton.styleFrom(
                backgroundColor: AppColors.brandPrimary,
                foregroundColor: AppColors.onBrandPrimary,
                shape: const RoundedRectangleBorder(
                  borderRadius: AppRadius.mdBorder,
                ),
              ),
              onPressed:
                  !isLoading && openingCash != null && openingCash >= 0
                      ? _open
                      : null,
              child:
                  isLoading
                      ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(
                          strokeWidth: 2.5,
                          color: AppColors.onBrandPrimary,
                        ),
                      )
                      : const Text(
                        'Open Shift',
                        style: TextStyle(
                          fontSize: 16,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
            ),
          ),
        ],
      ),
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
        Container(
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: AppRadius.lgBorder,
            boxShadow: AppShadows.subtle,
            border: Border.all(color: AppColors.border),
          ),
          padding: const EdgeInsets.all(20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  const Text(
                    'Shift open',
                    style: TextStyle(
                      fontSize: 18,
                      fontWeight: FontWeight.w700,
                      color: AppColors.textPrimary,
                    ),
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 10,
                      vertical: 4,
                    ),
                    decoration: BoxDecoration(
                      color: AppColors.accentEmeraldContainer,
                      borderRadius: BorderRadius.circular(AppRadius.full),
                    ),
                    child: const Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          Icons.circle,
                          size: 8,
                          color: AppColors.accentEmerald,
                        ),
                        SizedBox(width: 6),
                        Text(
                          'ACTIVE',
                          style: TextStyle(
                            fontSize: 11,
                            fontWeight: FontWeight.w700,
                            color: AppColors.onAccentEmeraldContainer,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 12),
              Text(
                'Opened by: ${widget.shift.openedByUserName}',
                style: const TextStyle(
                  fontSize: 14,
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: 4),
              Text(
                'Opening cash: ₱${widget.shift.openingCashAmount.toStringAsFixed(2)}',
                style: const TextStyle(
                  fontSize: 14,
                  fontWeight: FontWeight.w600,
                  color: AppColors.textPrimary,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 20),
        Container(
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: AppRadius.lgBorder,
            boxShadow: AppShadows.subtle,
            border: Border.all(color: AppColors.border),
          ),
          padding: const EdgeInsets.all(20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text(
                'End of Shift Reconciliation',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w700,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(height: 16),
              TextField(
                controller: _closingCashController,
                enabled: !isLoading,
                decoration: const InputDecoration(
                  labelText: 'Closing cash count',
                  prefixText: '₱ ',
                  prefixIcon: Icon(Icons.payments_outlined, size: 20),
                ),
                keyboardType: const TextInputType.numberWithOptions(
                  decimal: true,
                ),
                onChanged: (_) => setState(() {}),
              ),
              const SizedBox(height: 12),
              TextField(
                controller: _handoverNotesController,
                enabled: !isLoading,
                decoration: const InputDecoration(
                  labelText: 'Handover notes (optional)',
                  prefixIcon: Icon(Icons.note_alt_outlined, size: 20),
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
                  prefixIcon: Icon(Icons.shield_outlined, size: 20),
                ),
                obscureText: true,
                keyboardType: TextInputType.number,
              ),
              if (failure != null) ...[
                const SizedBox(height: 16),
                Container(
                  padding: const EdgeInsets.all(12),
                  decoration: BoxDecoration(
                    color: AppColors.error.withAlpha(20),
                    borderRadius: AppRadius.smBorder,
                    border: Border.all(color: AppColors.error.withAlpha(60)),
                  ),
                  child: Text(
                    failure.message,
                    style: const TextStyle(
                      color: AppColors.error,
                      fontSize: 13,
                      fontWeight: FontWeight.w500,
                    ),
                    textAlign: TextAlign.center,
                  ),
                ),
              ],
              const SizedBox(height: 20),
              SizedBox(
                height: 50,
                child: FilledButton(
                  style: FilledButton.styleFrom(
                    backgroundColor: AppColors.brandPrimary,
                    foregroundColor: AppColors.onBrandPrimary,
                    shape: const RoundedRectangleBorder(
                      borderRadius: AppRadius.mdBorder,
                    ),
                  ),
                  onPressed:
                      !isLoading && closingCash != null && closingCash >= 0
                          ? _close
                          : null,
                  child:
                      isLoading
                          ? const SizedBox(
                            height: 20,
                            width: 20,
                            child: CircularProgressIndicator(
                              strokeWidth: 2.5,
                              color: AppColors.onBrandPrimary,
                            ),
                          )
                          : const Text(
                            'Close Shift',
                            style: TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.w600,
                            ),
                          ),
                ),
              ),
            ],
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
        const Text(
          'Shift closed',
          style: TextStyle(
            fontSize: 22,
            fontWeight: FontWeight.w800,
            color: AppColors.textPrimary,
          ),
          textAlign: TextAlign.center,
        ),
        const SizedBox(height: 16),
        Container(
          decoration: BoxDecoration(
            color: AppColors.surface,
            borderRadius: AppRadius.xlBorder,
            boxShadow: AppShadows.card,
            border: Border.all(color: AppColors.border),
          ),
          padding: const EdgeInsets.all(24),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Opening cash: ₱${shift.openingCashAmount.toStringAsFixed(2)}',
                style: const TextStyle(
                  fontSize: 14,
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: 6),
              Text(
                'Expected cash: ₱${(shift.expectedCashAmount ?? 0).toStringAsFixed(2)}',
                style: const TextStyle(
                  fontSize: 14,
                  color: AppColors.textSecondary,
                ),
              ),
              const SizedBox(height: 6),
              Text(
                'Actual cash: ₱${(shift.closingCashAmount ?? 0).toStringAsFixed(2)}',
                style: const TextStyle(
                  fontSize: 15,
                  fontWeight: FontWeight.w600,
                  color: AppColors.textPrimary,
                ),
              ),
              const SizedBox(height: 14),
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color:
                      variance == 0
                          ? AppColors.accentEmeraldContainer
                          : AppColors.accentWarmContainer,
                  borderRadius: AppRadius.smBorder,
                ),
                child: Row(
                  children: [
                    Icon(
                      variance == 0
                          ? Icons.check_circle_rounded
                          : Icons.warning_amber_rounded,
                      color:
                          variance == 0
                              ? AppColors.accentEmerald
                              : AppColors.accentWarm,
                      size: 20,
                    ),
                    const SizedBox(width: 8),
                    Text(
                      varianceLabel,
                      style: TextStyle(
                        fontSize: 15,
                        fontWeight: FontWeight.w700,
                        color:
                            variance == 0
                                ? AppColors.onAccentEmeraldContainer
                                : AppColors.onAccentWarmContainer,
                      ),
                    ),
                  ],
                ),
              ),
              if (shift.approvedByUserName != null) ...[
                const SizedBox(height: 12),
                Text(
                  'Approved by: ${shift.approvedByUserName}',
                  style: const TextStyle(
                    fontSize: 14,
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
              if (shift.handoverNotes != null) ...[
                const SizedBox(height: 8),
                Text(
                  'Notes: ${shift.handoverNotes}',
                  style: const TextStyle(
                    fontSize: 14,
                    color: AppColors.textSecondary,
                  ),
                ),
              ],
            ],
          ),
        ),
        const SizedBox(height: 24),
        SizedBox(
          height: 50,
          child: FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: AppColors.brandPrimary,
              foregroundColor: AppColors.onBrandPrimary,
              shape: const RoundedRectangleBorder(
                borderRadius: AppRadius.mdBorder,
              ),
            ),
            onPressed:
                () =>
                    ref
                        .read(currentShiftNotifierProvider.notifier)
                        .acknowledgeClosedShift(),
            child: const Text(
              'Done',
              style: TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
            ),
          ),
        ),
      ],
    );
  }
}

