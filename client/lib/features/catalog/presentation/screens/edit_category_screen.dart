import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/errors/failure.dart';
import '../../../../core/theming/app_tokens.dart';
import '../../../../core/widgets/image_upload_field.dart';
import '../../../../core/widgets/status_badge.dart';
import '../../domain/category_models.dart';
import '../providers/catalog_providers.dart';

class EditCategoryScreen extends ConsumerStatefulWidget {
  const EditCategoryScreen({super.key, required this.category});

  final Category category;

  @override
  ConsumerState<EditCategoryScreen> createState() => _EditCategoryScreenState();
}

class _EditCategoryScreenState extends ConsumerState<EditCategoryScreen> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _nameController;
  late final TextEditingController _sortOrderController;
  late final TextEditingController _imageUrlController;

  @override
  void initState() {
    super.initState();
    _nameController = TextEditingController(text: widget.category.name);
    _sortOrderController =
        TextEditingController(text: widget.category.sortOrder.toString());
    _imageUrlController =
        TextEditingController(text: widget.category.imageUrl ?? '');
  }

  @override
  void dispose() {
    _nameController.dispose();
    _sortOrderController.dispose();
    _imageUrlController.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) {
      return;
    }

    final sortOrder = int.tryParse(_sortOrderController.text.trim()) ??
        widget.category.sortOrder;

    final controller = ref.read(updateCategoryControllerProvider.notifier);
    final succeeded = await controller.updateCategory(
      widget.category.id,
      UpdateCategoryRequest(
        name: _nameController.text.trim(),
        sortOrder: sortOrder,
        imageUrl: _imageUrlController.text.trim().isEmpty
            ? null
            : _imageUrlController.text.trim(),
      ),
    );

    if (!mounted) return;

    if (succeeded) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          backgroundColor: AppColors.success,
          content: Text('Category "${_nameController.text.trim()}" updated successfully'),
        ),
      );
      Navigator.of(context).pop();
    }
  }

  @override
  Widget build(BuildContext context) {
    final updateState = ref.watch(updateCategoryControllerProvider);
    final isLoading = updateState.isLoading;
    final failure =
        ref.read(updateCategoryControllerProvider.notifier).currentFailure;

    // Harmonized color pair for this category
    final colorPair = AppColors.categoryPalette[
        widget.category.name.hashCode.abs() % AppColors.categoryPalette.length];

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Edit Category'),
        backgroundColor: AppColors.surface,
        elevation: 0,
        centerTitle: false,
        actions: [
          Padding(
            padding: const EdgeInsets.only(right: AppSpacing.lg),
            child: Center(
              child: StatusBadge(
                label: 'Order #${widget.category.sortOrder}',
                type: StatusBadgeType.info,
                isSmall: true,
              ),
            ),
          ),
        ],
      ),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 520),
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.lg,
                vertical: AppSpacing.xl,
              ),
              child: Container(
                decoration: BoxDecoration(
                  color: AppColors.surface,
                  borderRadius: AppRadius.lgBorder,
                  border: Border.all(color: AppColors.border),
                  boxShadow: AppShadows.card,
                ),
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.xl),
                  child: Form(
                    key: _formKey,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        // Category identity header with colored preview avatar
                        Row(
                          children: [
                            Container(
                              width: 52,
                              height: 52,
                              decoration: BoxDecoration(
                                color: colorPair.background,
                                borderRadius: AppRadius.mdBorder,
                                border: Border.all(color: colorPair.border),
                              ),
                              child: Icon(
                                Icons.category_rounded,
                                color: colorPair.foreground,
                                size: 28,
                              ),
                            ),
                            const SizedBox(width: AppSpacing.md),
                            Expanded(
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    widget.category.name,
                                    style: const TextStyle(
                                      fontSize: 18,
                                      fontWeight: FontWeight.w700,
                                      color: AppColors.textPrimary,
                                      letterSpacing: -0.2,
                                    ),
                                  ),
                                  const SizedBox(height: 2),
                                  const Text(
                                    'Update category details and visual appearance.',
                                    style: TextStyle(
                                      fontSize: 13,
                                      color: AppColors.textSecondary,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ),
                        const SizedBox(height: AppSpacing.xl),

                        if (failure != null) ...[
                          Container(
                            padding: const EdgeInsets.all(AppSpacing.md),
                            decoration: BoxDecoration(
                              color: AppColors.errorContainer,
                              borderRadius: AppRadius.mdBorder,
                              border: Border.all(color: AppColors.errorBorder),
                            ),
                            child: Row(
                              children: [
                                const Icon(
                                  Icons.error_outline_rounded,
                                  color: AppColors.error,
                                  size: 20,
                                ),
                                const SizedBox(width: AppSpacing.sm),
                                Expanded(
                                  child: Text(
                                    describeError(failure),
                                    style: const TextStyle(
                                      color: AppColors.onErrorContainer,
                                      fontSize: 13,
                                      fontWeight: FontWeight.w500,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                          const SizedBox(height: AppSpacing.lg),
                        ],

                        TextFormField(
                          controller: _nameController,
                          enabled: !isLoading,
                          textInputAction: TextInputAction.next,
                          decoration: const InputDecoration(
                            labelText: 'Category name',
                            hintText: 'e.g. Hot Drinks, Bakery, Retail',
                            prefixIcon: Icon(Icons.label_outline_rounded),
                          ),
                          validator: (value) {
                            final trimmed = value?.trim() ?? '';
                            if (trimmed.isEmpty) {
                              return 'Category name is required';
                            }
                            return null;
                          },
                        ),
                        const SizedBox(height: AppSpacing.lg),

                        TextFormField(
                          controller: _sortOrderController,
                          enabled: !isLoading,
                          keyboardType: TextInputType.number,
                          textInputAction: TextInputAction.next,
                          decoration: const InputDecoration(
                            labelText: 'Display sort order',
                            hintText: '0, 1, 2...',
                            helperText: 'Controls placement on the POS and Kiosk menus',
                            prefixIcon: Icon(Icons.sort_rounded),
                          ),
                          validator: (value) {
                            final trimmed = value?.trim() ?? '';
                            if (trimmed.isEmpty) {
                              return 'Sort order is required';
                            }
                            final parsed = int.tryParse(trimmed);
                            if (parsed == null || parsed < 0) {
                              return 'Enter a non-negative integer';
                            }
                            return null;
                          },
                        ),
                        const SizedBox(height: AppSpacing.lg),

                        ImageUploadField(
                          controller: _imageUrlController,
                          label: 'Category image',
                          hintText: 'Upload or enter an image URL',
                          enabled: !isLoading,
                        ),
                        const SizedBox(height: AppSpacing.xl),

                        FilledButton(
                          onPressed: isLoading ? null : _submit,
                          style: FilledButton.styleFrom(
                            backgroundColor: AppColors.brandPrimary,
                            foregroundColor: AppColors.onBrandPrimary,
                            minimumSize: const Size.fromHeight(48),
                            shape: const RoundedRectangleBorder(
                              borderRadius: AppRadius.mdBorder,
                            ),
                          ),
                          child: isLoading
                              ? const SizedBox(
                                  width: 20,
                                  height: 20,
                                  child: CircularProgressIndicator(
                                    strokeWidth: 2,
                                    color: Colors.white,
                                  ),
                                )
                              : const Row(
                                  mainAxisAlignment: MainAxisAlignment.center,
                                  children: [
                                    Icon(Icons.check_rounded, size: 18),
                                    SizedBox(width: 8),
                                    Text(
                                      'Save Changes',
                                      style: TextStyle(
                                        fontSize: 16,
                                        fontWeight: FontWeight.w600,
                                      ),
                                    ),
                                  ],
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
