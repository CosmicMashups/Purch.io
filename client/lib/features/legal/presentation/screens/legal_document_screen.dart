import 'package:flutter/material.dart';

import '../../../../core/theming/app_tokens.dart';
import '../../domain/legal_document.dart';

/// Shared scrollable viewer for a legal document (Privacy Policy or Terms
/// of Service): a title, a draft/last-updated notice, and a list of
/// heading + body sections. Both PrivacyPolicyScreen and
/// TermsOfServiceScreen are thin wrappers around this with their own
/// content, so the two documents stay visually identical.
class LegalDocumentScreen extends StatelessWidget {
  const LegalDocumentScreen({
    super.key,
    required this.title,
    required this.lastUpdatedNote,
    required this.sections,
  });

  final String title;
  final String lastUpdatedNote;
  final List<LegalSection> sections;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(title: Text(title), backgroundColor: AppColors.surface),
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 640),
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(AppSpacing.lg),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Container(
                    padding: const EdgeInsets.all(AppSpacing.md),
                    decoration: BoxDecoration(
                      color: AppColors.accentWarmContainer,
                      borderRadius: AppRadius.smBorder,
                      border: Border.all(
                        color: AppColors.accentWarm.withAlpha(80),
                      ),
                    ),
                    child: Row(
                      children: [
                        const Icon(
                          Icons.info_outline_rounded,
                          size: 18,
                          color: AppColors.onAccentWarmContainer,
                        ),
                        const SizedBox(width: AppSpacing.sm),
                        Expanded(
                          child: Text(
                            lastUpdatedNote,
                            style: const TextStyle(
                              fontSize: 12,
                              color: AppColors.onAccentWarmContainer,
                              fontWeight: FontWeight.w500,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: AppSpacing.lg),
                  for (final section in sections) ...[
                    Text(
                      section.heading,
                      style: const TextStyle(
                        fontSize: 16,
                        fontWeight: FontWeight.w700,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.sm),
                    Text(
                      section.body,
                      style: const TextStyle(
                        fontSize: 14,
                        height: 1.5,
                        color: AppColors.textSecondary,
                      ),
                    ),
                    const SizedBox(height: AppSpacing.lg),
                  ],
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
