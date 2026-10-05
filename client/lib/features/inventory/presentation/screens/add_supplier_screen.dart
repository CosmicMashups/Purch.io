import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/supplier_models.dart';
import '../providers/supplier_providers.dart';

InputDecoration _decoration(String label, {String? hint}) {
  return InputDecoration(
    labelText: label,
    hintText: hint,
    isDense: true,
    border: OutlineInputBorder(
      borderRadius: AppRadius.mdBorder,
      borderSide: const BorderSide(color: AppColors.border),
    ),
    enabledBorder: OutlineInputBorder(
      borderRadius: AppRadius.mdBorder,
      borderSide: const BorderSide(color: AppColors.border),
    ),
    focusedBorder: OutlineInputBorder(
      borderRadius: AppRadius.mdBorder,
      borderSide: const BorderSide(color: AppColors.brandPrimary, width: 2),
    ),
    filled: true,
    fillColor: AppColors.cardHover,
  );
}

/// One contact being edited: its text fields live here so they survive rebuilds.
class _ContactDraft {
  _ContactDraft({SupplierContact? contact})
    : person = TextEditingController(text: contact?.contactPerson ?? ''),
      modes = {...?contact?.modes},
      numbers = [
        for (final n in (contact?.numbers.isNotEmpty ?? false) ? contact!.numbers : [''])
          TextEditingController(text: n),
      ],
      emails = [
        for (final e in (contact?.emails.isNotEmpty ?? false) ? contact!.emails : [''])
          TextEditingController(text: e),
      ];

  final TextEditingController person;
  final Set<String> modes;
  final List<TextEditingController> numbers;
  final List<TextEditingController> emails;

  void dispose() {
    person.dispose();
    for (final c in [...numbers, ...emails]) {
      c.dispose();
    }
  }

  SupplierContact toContact() => SupplierContact(
    contactPerson: person.text.trim(),
    modes: [
      for (final mode in supplierContactModes)
        if (modes.contains(mode)) mode,
    ],
    numbers: [for (final c in numbers) if (c.text.trim().isNotEmpty) c.text.trim()],
    emails: [for (final c in emails) if (c.text.trim().isNotEmpty) c.text.trim()],
  );
}

/// Adds a supplier, or edits [supplier] when one is given.
class AddSupplierScreen extends ConsumerStatefulWidget {
  const AddSupplierScreen({super.key, this.supplier});

  final Supplier? supplier;

  @override
  ConsumerState<AddSupplierScreen> createState() => _AddSupplierScreenState();
}

class _AddSupplierScreenState extends ConsumerState<AddSupplierScreen> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _nameController;
  late final TextEditingController _specializationController;
  late final TextEditingController _addressController;
  late final TextEditingController _tinController;
  late final TextEditingController _remarksController;
  late final List<_ContactDraft> _contacts;

  bool get _editing => widget.supplier != null;

  @override
  void initState() {
    super.initState();
    final s = widget.supplier;
    _nameController = TextEditingController(text: s?.name ?? '');
    _specializationController = TextEditingController(text: s?.specialization ?? '');
    _addressController = TextEditingController(text: s?.address ?? '');
    _tinController = TextEditingController(text: s?.tin ?? '');
    // A supplier saved before contacts were structured keeps its old text as a remark.
    _remarksController = TextEditingController(
      text: s?.remarks ?? ((s != null && s.contacts.isEmpty) ? (s.contactInfo ?? '') : ''),
    );
    _contacts = [
      for (final c in (s?.contacts.isNotEmpty ?? false) ? s!.contacts : <SupplierContact?>[null])
        _ContactDraft(contact: c),
    ];
  }

  @override
  void dispose() {
    _nameController.dispose();
    _specializationController.dispose();
    _addressController.dispose();
    _tinController.dispose();
    _remarksController.dispose();
    for (final c in _contacts) {
      c.dispose();
    }
    super.dispose();
  }

  String? _blankToNull(TextEditingController controller) {
    final text = controller.text.trim();
    return text.isEmpty ? null : text;
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final contacts = [
      for (final draft in _contacts) draft.toContact(),
    ].where((c) => c.contactPerson.isNotEmpty || c.numbers.isNotEmpty || c.emails.isNotEmpty).toList();

    final controller = ref.read(createSupplierControllerProvider.notifier);
    final bool succeeded;
    if (_editing) {
      succeeded = await controller.updateSupplier(
        widget.supplier!.id,
        UpdateSupplierRequest(
          name: _nameController.text.trim(),
          specialization: _blankToNull(_specializationController),
          address: _blankToNull(_addressController),
          tin: _blankToNull(_tinController),
          remarks: _blankToNull(_remarksController),
          contacts: contacts,
          isActive: widget.supplier!.isActive,
        ),
      );
    } else {
      succeeded = await controller.create(
        CreateSupplierRequest(
          name: _nameController.text.trim(),
          specialization: _blankToNull(_specializationController),
          address: _blankToNull(_addressController),
          tin: _blankToNull(_tinController),
          remarks: _blankToNull(_remarksController),
          contacts: contacts,
        ),
      );
    }

    if (!mounted) {
      return;
    }

    if (succeeded) {
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final createState = ref.watch(createSupplierControllerProvider);
    final isLoading = createState.isLoading;
    final failure =
        ref.read(createSupplierControllerProvider.notifier).currentFailure;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Text(_editing ? 'Edit Supplier' : 'Add Supplier'),
        backgroundColor: AppColors.surface,
        elevation: 0,
        centerTitle: false,
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 520),
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.md,
                vertical: AppSpacing.sm,
              ),
              child: Card(
                elevation: 0,
                color: AppColors.surface,
                shape: RoundedRectangleBorder(
                  borderRadius: AppRadius.lgBorder,
                  side: const BorderSide(color: AppColors.border),
                ),
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.md),
                  child: Form(
                    key: _formKey,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        TextFormField(
                          controller: _nameController,
                          enabled: !isLoading,
                          decoration: _decoration('Supplier name', hint: 'e.g. San Miguel Distribution'),
                          validator: (value) => (value == null || value.trim().isEmpty) ? 'Required' : null,
                        ),
                        const SizedBox(height: AppSpacing.md),
                        TextFormField(
                          controller: _specializationController,
                          enabled: !isLoading,
                          decoration: _decoration('Specialization (optional)', hint: 'What they mainly supply'),
                        ),
                        const SizedBox(height: AppSpacing.md),
                        TextFormField(
                          controller: _addressController,
                          enabled: !isLoading,
                          decoration: _decoration('Address (optional)'),
                        ),
                        const SizedBox(height: AppSpacing.md),
                        TextFormField(
                          controller: _tinController,
                          enabled: !isLoading,
                          decoration: _decoration('TIN (optional)'),
                        ),
                        const SizedBox(height: AppSpacing.lg),
                        Text(
                          'Contacts',
                          style: Theme.of(context).textTheme.titleMedium?.copyWith(
                            fontWeight: FontWeight.w700,
                            color: AppColors.textPrimary,
                          ),
                        ),
                        const SizedBox(height: AppSpacing.sm),
                        for (var i = 0; i < _contacts.length; i++)
                          _ContactEditor(
                            key: ObjectKey(_contacts[i]),
                            draft: _contacts[i],
                            enabled: !isLoading,
                            onChanged: () => setState(() {}),
                            onRemove: _contacts.length > 1
                                ? () => setState(() => _contacts.removeAt(i).dispose())
                                : null,
                          ),
                        Align(
                          alignment: Alignment.centerLeft,
                          child: TextButton.icon(
                            onPressed: isLoading ? null : () => setState(() => _contacts.add(_ContactDraft())),
                            icon: const Icon(Icons.add, size: 18),
                            label: const Text('Add another contact'),
                          ),
                        ),
                        const SizedBox(height: AppSpacing.md),
                        TextFormField(
                          controller: _remarksController,
                          enabled: !isLoading,
                          maxLines: 3,
                          decoration: _decoration('Remarks (optional)'),
                        ),
                        if (failure != null) ...[
                          const SizedBox(height: AppSpacing.md),
                          Container(
                            padding: const EdgeInsets.all(AppSpacing.sm),
                            decoration: BoxDecoration(
                              color: AppColors.cardHover,
                              borderRadius: AppRadius.mdBorder,
                              border: Border.all(color: AppColors.error),
                            ),
                            child: Text(
                              failure.message,
                              style: const TextStyle(color: AppColors.error, fontWeight: FontWeight.w500),
                              textAlign: TextAlign.center,
                            ),
                          ),
                        ],
                        const SizedBox(height: AppSpacing.lg),
                        SizedBox(
                          height: 52,
                          child: FilledButton(
                            style: FilledButton.styleFrom(
                              backgroundColor: AppColors.brandPrimary,
                              foregroundColor: AppColors.onBrandPrimary,
                              shape: const RoundedRectangleBorder(borderRadius: AppRadius.mdBorder),
                            ),
                            onPressed: isLoading ? null : _submit,
                            child: isLoading
                                ? const SizedBox(
                                    height: 24,
                                    width: 24,
                                    child: CircularProgressIndicator(strokeWidth: 2.5, color: AppColors.onBrandPrimary),
                                  )
                                : Text(
                                    _editing ? 'Save Changes' : 'Add Supplier',
                                    style: const TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
                                  ),
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
      ),
    );
  }
}

class _ContactEditor extends StatelessWidget {
  const _ContactEditor({
    super.key,
    required this.draft,
    required this.enabled,
    required this.onChanged,
    required this.onRemove,
  });

  final _ContactDraft draft;
  final bool enabled;
  final VoidCallback onChanged;
  final VoidCallback? onRemove;

  @override
  Widget build(BuildContext context) {
    return Container(
      margin: const EdgeInsets.only(bottom: AppSpacing.sm),
      padding: const EdgeInsets.all(AppSpacing.sm),
      decoration: BoxDecoration(
        borderRadius: AppRadius.mdBorder,
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextFormField(
            controller: draft.person,
            enabled: enabled,
            decoration: _decoration('Contact person'),
          ),
          const SizedBox(height: AppSpacing.sm),
          const Text('Mode of contact', style: TextStyle(fontWeight: FontWeight.w600, color: AppColors.textSecondary)),
          Wrap(
            spacing: AppSpacing.xs,
            children: [
              for (final mode in supplierContactModes)
                FilterChip(
                  label: Text(mode),
                  selected: draft.modes.contains(mode),
                  onSelected: enabled
                      ? (selected) {
                          selected ? draft.modes.add(mode) : draft.modes.remove(mode);
                          onChanged();
                        }
                      : null,
                ),
            ],
          ),
          const SizedBox(height: AppSpacing.sm),
          for (var i = 0; i < draft.numbers.length; i++) ...[
            Row(
              children: [
                Expanded(
                  child: TextFormField(
                    controller: draft.numbers[i],
                    enabled: enabled,
                    keyboardType: TextInputType.phone,
                    decoration: _decoration(i == 0 ? 'Contact number' : 'Contact number ${i + 1}'),
                  ),
                ),
                if (draft.numbers.length > 1)
                  IconButton(
                    tooltip: 'Remove number',
                    icon: const Icon(Icons.close),
                    onPressed: enabled
                        ? () {
                            draft.numbers.removeAt(i).dispose();
                            onChanged();
                          }
                        : null,
                  ),
              ],
            ),
            const SizedBox(height: AppSpacing.xs),
          ],
          Align(
            alignment: Alignment.centerLeft,
            child: TextButton.icon(
              onPressed: enabled
                  ? () {
                      draft.numbers.add(TextEditingController());
                      onChanged();
                    }
                  : null,
              icon: const Icon(Icons.add, size: 18),
              label: const Text('Add another number'),
            ),
          ),
          for (var i = 0; i < draft.emails.length; i++) ...[
            Row(
              children: [
                Expanded(
                  child: TextFormField(
                    controller: draft.emails[i],
                    enabled: enabled,
                    keyboardType: TextInputType.emailAddress,
                    decoration: _decoration(i == 0 ? 'Email address' : 'Email address ${i + 1}'),
                    validator: (value) {
                      final text = value?.trim() ?? '';
                      return text.isEmpty || RegExp(r'^\S+@\S+\.\S+$').hasMatch(text) ? null : 'Enter a valid email';
                    },
                  ),
                ),
                if (draft.emails.length > 1)
                  IconButton(
                    tooltip: 'Remove email',
                    icon: const Icon(Icons.close),
                    onPressed: enabled
                        ? () {
                            draft.emails.removeAt(i).dispose();
                            onChanged();
                          }
                        : null,
                  ),
              ],
            ),
            const SizedBox(height: AppSpacing.xs),
          ],
          Align(
            alignment: Alignment.centerLeft,
            child: TextButton.icon(
              onPressed: enabled
                  ? () {
                      draft.emails.add(TextEditingController());
                      onChanged();
                    }
                  : null,
              icon: const Icon(Icons.add, size: 18),
              label: const Text('Add another email'),
            ),
          ),
          if (onRemove != null)
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton(
                style: TextButton.styleFrom(foregroundColor: AppColors.error),
                onPressed: enabled ? onRemove : null,
                child: const Text('Remove contact'),
              ),
            ),
        ],
      ),
    );
  }
}
