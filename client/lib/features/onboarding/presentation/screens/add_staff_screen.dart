import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/staff_models.dart';
import '../providers/onboarding_providers.dart';

/// Invites a person. Nothing is emailed: the admin gets a single-use link to hand
/// over, and the person opens it on their own phone to choose a password and PIN.
class AddStaffScreen extends ConsumerStatefulWidget {
  const AddStaffScreen({super.key});

  @override
  ConsumerState<AddStaffScreen> createState() => _AddStaffScreenState();
}

class _AddStaffScreenState extends ConsumerState<AddStaffScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  final _emailController = TextEditingController();
  MemberRole _role = MemberRole.staff;
  int _duties = StaffDuties.cashier;
  final Set<String> _branchIds = {};
  String? _selectionError;

  @override
  void dispose() {
    _nameController.dispose();
    _emailController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    if (_role == MemberRole.staff) {
      if (_duties == StaffDuties.none) {
        setState(() => _selectionError = 'Choose at least one duty.');
        return;
      }
      if (_branchIds.isEmpty) {
        setState(() => _selectionError = 'Choose at least one branch.');
        return;
      }
    }
    setState(() => _selectionError = null);

    final link = await ref
        .read(createStaffControllerProvider.notifier)
        .create(
          InviteStaffRequest(
            name: _nameController.text.trim(),
            email: _emailController.text.trim(),
            role: _role,
            duties: _duties,
            branchIds: _branchIds.toList(),
          ),
        );

    if (!mounted || link == null) {
      return;
    }
    await showInviteLinkDialog(context, link);
    if (mounted) {
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final createState = ref.watch(createStaffControllerProvider);
    final isLoading = createState.isLoading;
    final failure =
        ref.read(createStaffControllerProvider.notifier).currentFailure;
    final branches = ref.watch(branchListProvider).valueOrNull ?? const [];

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: const Text('Invite a person'), elevation: 0),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 480),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(AppSpacing.md),
              child: Form(
                key: _formKey,
                child: Container(
                  padding: const EdgeInsets.all(AppSpacing.md),
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius: AppRadius.mdBorder,
                    border: Border.all(color: AppColors.border),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      TextFormField(
                        controller: _nameController,
                        enabled: !isLoading,
                        decoration: const InputDecoration(
                          labelText: 'Full name',
                          hintText: 'e.g. Juan dela Cruz',
                          prefixIcon: Icon(Icons.person_outline),
                        ),
                        validator:
                            (value) =>
                                (value == null || value.trim().isEmpty)
                                    ? 'Required'
                                    : null,
                      ),
                      const SizedBox(height: AppSpacing.md),
                      TextFormField(
                        controller: _emailController,
                        enabled: !isLoading,
                        keyboardType: TextInputType.emailAddress,
                        decoration: const InputDecoration(
                          labelText: 'Email',
                          prefixIcon: Icon(Icons.email_outlined),
                        ),
                        validator: (value) {
                          final text = value?.trim() ?? '';
                          if (text.isEmpty) return 'Required';
                          return RegExp(r'^\S+@\S+\.\S+$').hasMatch(text)
                              ? null
                              : 'Enter a valid email address';
                        },
                      ),
                      const SizedBox(height: AppSpacing.md),
                      DropdownButtonFormField<MemberRole>(
                        value: _role,
                        decoration: const InputDecoration(
                          labelText: 'Role',
                          prefixIcon: Icon(Icons.badge_outlined),
                        ),
                        items:
                            MemberRole.values
                                .map(
                                  (role) => DropdownMenuItem(
                                    value: role,
                                    child: Text(_roleLabel(role)),
                                  ),
                                )
                                .toList(),
                        onChanged:
                            isLoading
                                ? null
                                : (value) =>
                                    setState(() => _role = value ?? _role),
                      ),
                      if (_role == MemberRole.staff) ...[
                        const SizedBox(height: AppSpacing.sm),
                        CheckboxListTile(
                          contentPadding: EdgeInsets.zero,
                          title: const Text('Cashier'),
                          value: StaffDuties.has(_duties, StaffDuties.cashier),
                          onChanged:
                              isLoading
                                  ? null
                                  : (on) => setState(
                                    () => _duties ^= StaffDuties.cashier,
                                  ),
                        ),
                        CheckboxListTile(
                          contentPadding: EdgeInsets.zero,
                          title: const Text('Warehouse'),
                          value: StaffDuties.has(_duties, StaffDuties.warehouse),
                          onChanged:
                              isLoading
                                  ? null
                                  : (on) => setState(
                                    () => _duties ^= StaffDuties.warehouse,
                                  ),
                        ),
                        const Padding(
                          padding: EdgeInsets.only(top: 8),
                          child: Text(
                            'Branches they work at',
                            style: TextStyle(fontWeight: FontWeight.w600),
                          ),
                        ),
                        for (final branch in branches)
                          CheckboxListTile(
                            contentPadding: EdgeInsets.zero,
                            title: Text(branch.name),
                            value: _branchIds.contains(branch.id),
                            onChanged:
                                isLoading
                                    ? null
                                    : (on) => setState(() {
                                      if (on ?? false) {
                                        _branchIds.add(branch.id);
                                      } else {
                                        _branchIds.remove(branch.id);
                                      }
                                    }),
                          ),
                      ] else
                        const Padding(
                          padding: EdgeInsets.only(top: 8),
                          child: Text(
                            'Admins and Managers work at every branch.',
                            style: TextStyle(color: AppColors.textSecondary),
                          ),
                        ),
                      if (_selectionError != null || failure != null) ...[
                        const SizedBox(height: AppSpacing.sm),
                        Text(
                          _selectionError ?? failure!.message,
                          textAlign: TextAlign.center,
                          style: const TextStyle(
                            color: AppColors.error,
                            fontSize: 13,
                            fontWeight: FontWeight.w500,
                          ),
                        ),
                      ],
                      const SizedBox(height: AppSpacing.md),
                      SizedBox(
                        height: 48,
                        child: FilledButton(
                          onPressed: isLoading ? null : _submit,
                          child:
                              isLoading
                                  ? const SizedBox(
                                    height: 20,
                                    width: 20,
                                    child: CircularProgressIndicator(
                                      strokeWidth: 2,
                                    ),
                                  )
                                  : const Text('Make invitation link'),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

String _roleLabel(MemberRole role) => switch (role) {
  MemberRole.admin => 'Admin',
  MemberRole.manager => 'Manager',
  MemberRole.staff => 'Staff',
};

/// Shows the single-use link once. Nothing is emailed; the admin hands it over.
Future<void> showInviteLinkDialog(BuildContext context, StaffInviteLink link) {
  return showDialog<void>(
    context: context,
    barrierDismissible: false,
    builder:
        (dialogContext) => AlertDialog(
          title: Text('Invitation for ${link.name}'),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'No email is sent. Give them this link: open it in the Purch.io web app on their own phone, behind the web app address. It works once and expires in a few days.',
                style: TextStyle(color: AppColors.textSecondary),
              ),
              const SizedBox(height: 12),
              SelectableText(
                link.path,
                style: const TextStyle(
                  fontFamily: 'monospace',
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ),
          actions: [
            FilledButton(
              onPressed: () => Navigator.of(dialogContext).pop(),
              child: const Text('Done'),
            ),
          ],
        ),
  );
}
