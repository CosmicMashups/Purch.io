import 'package:flutter/material.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/theming/app_tokens.dart';

/// Runs a cart action that may come back needing a manager/admin PIN (voiding a cart with items, or
/// editing a claimed kiosk order the kitchen hasn't started yet — see the backend's
/// ApproverAuthorizationService). [action] is tried with no PIN first; only a refusal that names
/// `approverPin` opens the dialog, so the common case (an ordinary cart, or an Admin/Manager who needs
/// none) never sees it. [currentFailure] must read whatever the same action just left behind (the cart
/// notifier's own `currentFailure` getter) — the provider's `_mutate` pattern sets that as state rather
/// than throwing, so it can't be read from a caught exception.
///
/// Returns true once the action has actually succeeded, false if the cashier cancelled or it failed for
/// a reason other than needing a PIN — the caller's own failure display (already wired to the same
/// provider state) shows that case as it would any other failure.
Future<bool> runApproverGatedCartAction({
  required BuildContext context,
  required String title,
  required Future<bool> Function(String? approverPin) action,
  required Failure? Function() currentFailure,
}) async {
  if (await action(null)) return true;

  final approverPinError = _approverPinMessage(currentFailure());
  if (approverPinError == null) return false;
  if (!context.mounted) return false;

  return showDialog<bool>(
    context: context,
    barrierDismissible: false,
    builder:
        (context) => _ApproverPinDialog(
          title: title,
          initialMessage: approverPinError,
          attempt: (pin) async {
            if (await action(pin)) return null;
            return _approverPinMessage(currentFailure());
          },
        ),
  ).then((result) => result ?? false);
}

/// The server's own reason a PIN is needed, or null when the failure was something else entirely.
String? _approverPinMessage(Failure? failure) {
  if (failure is! ValidationFailure) return null;
  final messages = failure.fieldErrors['approverPin'];
  return messages == null || messages.isEmpty ? null : messages.first;
}

class _ApproverPinDialog extends StatefulWidget {
  const _ApproverPinDialog({
    required this.title,
    required this.initialMessage,
    required this.attempt,
  });

  final String title;
  final String initialMessage;

  /// Returns null on success, or the server's next approver-PIN message to show and keep the dialog open.
  final Future<String?> Function(String pin) attempt;

  @override
  State<_ApproverPinDialog> createState() => _ApproverPinDialogState();
}

class _ApproverPinDialogState extends State<_ApproverPinDialog> {
  final _pinController = TextEditingController();
  late String _message = widget.initialMessage;
  bool _isError = false;
  bool _busy = false;

  @override
  void dispose() {
    _pinController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    final pin = _pinController.text.trim();
    if (pin.isEmpty || _busy) return;

    setState(() => _busy = true);
    final nextMessage = await widget.attempt(pin);
    if (!mounted) return;

    if (nextMessage == null) {
      Navigator.of(context).pop(true);
      return;
    }
    setState(() {
      _message = nextMessage;
      _isError = true;
      _busy = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    return AlertDialog(
      shape: const RoundedRectangleBorder(borderRadius: AppRadius.lgBorder),
      title: Text(widget.title),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            _message,
            style: TextStyle(
              color: _isError ? AppColors.error : AppColors.textSecondary,
              fontWeight: _isError ? FontWeight.w600 : FontWeight.w400,
            ),
          ),
          const SizedBox(height: 16),
          TextField(
            controller: _pinController,
            enabled: !_busy,
            autofocus: true,
            decoration: const InputDecoration(
              labelText: 'Manager or admin PIN',
              prefixIcon: Icon(Icons.shield_outlined, size: 20),
            ),
            obscureText: true,
            keyboardType: TextInputType.number,
            onChanged: (_) => setState(() {}),
            onSubmitted: (_) => _submit(),
          ),
        ],
      ),
      actions: [
        TextButton(
          onPressed: _busy ? null : () => Navigator.of(context).pop(false),
          child: const Text('Cancel'),
        ),
        FilledButton(
          onPressed:
              _busy || _pinController.text.trim().isEmpty ? null : _submit,
          child: Text(_busy ? 'Checking...' : 'Approve'),
        ),
      ],
    );
  }
}
