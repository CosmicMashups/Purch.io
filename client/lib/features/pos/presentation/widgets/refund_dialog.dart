import 'package:flutter/material.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/formatting/money.dart';
import '../../../../core/theming/app_tokens.dart';

/// Refund reason and manager/admin PIN, collected together — unlike voiding an open cart, a refund is
/// never free, so there is no point asking for the PIN only after a first refusal. Both fields stay
/// editable if the server refuses either one; the dialog only closes once the refund has actually gone
/// through. Returns true once it has, false if the cashier cancelled.
Future<bool> showRefundDialog({
  required BuildContext context,
  required double total,
  required Future<Failure?> Function(String reason, String approverPin) submit,
}) async {
  return showDialog<bool>(
    context: context,
    barrierDismissible: false,
    builder: (context) => _RefundDialog(total: total, submit: submit),
  ).then((result) => result ?? false);
}

class _RefundDialog extends StatefulWidget {
  const _RefundDialog({required this.total, required this.submit});

  final double total;

  /// Returns null on success, or the failure's message to show and keep the dialog open.
  final Future<Failure?> Function(String reason, String approverPin) submit;

  @override
  State<_RefundDialog> createState() => _RefundDialogState();
}

class _RefundDialogState extends State<_RefundDialog> {
  final _reasonController = TextEditingController();
  final _pinController = TextEditingController();
  String? _error;
  bool _busy = false;

  @override
  void dispose() {
    _reasonController.dispose();
    _pinController.dispose();
    super.dispose();
  }

  bool get _canSubmit =>
      _reasonController.text.trim().isNotEmpty &&
      _pinController.text.trim().isNotEmpty &&
      !_busy;

  Future<void> _submit() async {
    if (!_canSubmit) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    final failure = await widget.submit(
      _reasonController.text.trim(),
      _pinController.text.trim(),
    );
    if (!mounted) return;
    if (failure == null) {
      Navigator.of(context).pop(true);
      return;
    }
    setState(() {
      _error = failure.message;
      _busy = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      shape: const RoundedRectangleBorder(borderRadius: AppRadius.lgBorder),
      title: Text('Refund ${formatCurrency(widget.total)}'),
      content: SingleChildScrollView(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              'This marks the whole sale Refunded and needs a manager or admin to approve it.',
              style: TextStyle(color: AppColors.textSecondary),
            ),
            const SizedBox(height: 16),
            TextField(
              key: const Key('refund-reason'),
              controller: _reasonController,
              enabled: !_busy,
              autofocus: true,
              minLines: 2,
              maxLines: 3,
              decoration: const InputDecoration(labelText: 'Reason'),
              onChanged: (_) => setState(() {}),
            ),
            const SizedBox(height: 12),
            TextField(
              key: const Key('refund-pin'),
              controller: _pinController,
              enabled: !_busy,
              decoration: const InputDecoration(
                labelText: 'Manager or admin PIN',
                prefixIcon: Icon(Icons.shield_outlined, size: 20),
              ),
              obscureText: true,
              keyboardType: TextInputType.number,
              onChanged: (_) => setState(() {}),
              onSubmitted: (_) => _submit(),
            ),
            if (_error != null) ...[
              const SizedBox(height: 12),
              Text(
                _error!,
                style: const TextStyle(
                  color: AppColors.error,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ],
        ),
      ),
      actions: [
        TextButton(
          onPressed: _busy ? null : () => Navigator.of(context).pop(false),
          child: const Text('Cancel'),
        ),
        FilledButton(
          key: const Key('confirm-refund'),
          style: FilledButton.styleFrom(
            backgroundColor: AppColors.error,
            foregroundColor: Colors.white,
          ),
          onPressed: _canSubmit ? _submit : null,
          child: Text(_busy ? 'Refunding...' : 'Refund'),
        ),
      ],
    );
  }
}
