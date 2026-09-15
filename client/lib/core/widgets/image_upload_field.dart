import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../errors/failure.dart';
import '../theming/app_tokens.dart';
import '../uploads/upload_providers.dart';
import 'purch_image.dart';

/// A labeled image field used for branding logo, category image, and item
/// image alike: a live preview, an "Upload Image" button that opens the
/// native file picker and uploads straight to `POST /uploads/image`, and a
/// text field underneath so a pasted URL still works (kept editable rather
/// than replaced, since existing records already hold plain URLs/paths).
class ImageUploadField extends ConsumerStatefulWidget {
  const ImageUploadField({
    super.key,
    required this.controller,
    required this.label,
    this.enabled = true,
    this.hintText,
    this.onChanged,
  });

  final TextEditingController controller;
  final String label;
  final bool enabled;
  final String? hintText;
  final VoidCallback? onChanged;

  @override
  ConsumerState<ImageUploadField> createState() => _ImageUploadFieldState();
}

class _ImageUploadFieldState extends ConsumerState<ImageUploadField> {
  bool _isUploading = false;
  String? _uploadError;

  Future<void> _pickAndUpload() async {
    final result = await FilePicker.platform.pickFiles(
      type: FileType.custom,
      allowedExtensions: ['jpg', 'jpeg', 'png', 'webp', 'gif', 'svg'],
      withData: false,
    );

    final path = result?.files.single.path;
    if (path == null || !mounted) {
      return;
    }

    setState(() {
      _isUploading = true;
      _uploadError = null;
    });

    try {
      final uploadResult = await ref
          .read(uploadRepositoryProvider)
          .uploadImage(path);
      if (!mounted) {
        return;
      }
      widget.controller.text = uploadResult.relativePath;
      widget.onChanged?.call();
    } catch (error) {
      if (!mounted) {
        return;
      }
      setState(() => _uploadError = describeError(error));
    } finally {
      if (mounted) {
        setState(() => _isUploading = false);
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final isBusy = _isUploading || !widget.enabled;
    final currentValue = widget.controller.text.trim();

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            ClipRRect(
              borderRadius: AppRadius.smBorder,
              child: Container(
                width: 56,
                height: 56,
                color: AppColors.cardHover,
                child:
                    currentValue.isEmpty
                        ? const Icon(
                          Icons.image_outlined,
                          color: AppColors.textMuted,
                        )
                        : PurchImage(
                          imageUrlOrPath: currentValue,
                          width: 56,
                          height: 56,
                          fit: BoxFit.cover,
                        ),
              ),
            ),
            const SizedBox(width: AppSpacing.md),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    widget.label,
                    style: const TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      color: AppColors.textSecondary,
                    ),
                  ),
                  const SizedBox(height: 6),
                  OutlinedButton.icon(
                    onPressed: isBusy ? null : _pickAndUpload,
                    icon:
                        _isUploading
                            ? const SizedBox(
                              width: 14,
                              height: 14,
                              child: CircularProgressIndicator(strokeWidth: 2),
                            )
                            : const Icon(Icons.upload_rounded, size: 16),
                    label: Text(
                      _isUploading ? 'Uploading…' : 'Upload Image',
                    ),
                    style: OutlinedButton.styleFrom(
                      foregroundColor: AppColors.brandPrimary,
                      side: const BorderSide(color: AppColors.border),
                      shape: const RoundedRectangleBorder(
                        borderRadius: AppRadius.smBorder,
                      ),
                      visualDensity: VisualDensity.compact,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
        if (_uploadError != null) ...[
          const SizedBox(height: 6),
          Text(
            _uploadError!,
            style: const TextStyle(fontSize: 12, color: AppColors.error),
          ),
        ],
        const SizedBox(height: AppSpacing.sm),
        TextFormField(
          controller: widget.controller,
          enabled: widget.enabled && !_isUploading,
          decoration: InputDecoration(
            labelText: '${widget.label} URL',
            hintText: widget.hintText ?? 'e.g. /uploads/... or https://...',
            isDense: true,
            border: OutlineInputBorder(borderRadius: AppRadius.smBorder),
          ),
          onChanged: (_) => widget.onChanged?.call(),
        ),
      ],
    );
  }
}
