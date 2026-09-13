import 'package:flutter/material.dart';
import 'package:flutter/gestures.dart';

import '../../../../core/theming/app_tokens.dart';
import '../screens/privacy_policy_screen.dart';
import '../screens/terms_of_service_screen.dart';

/// "I agree to the Terms of Service and Privacy Policy" checkbox with
/// inline tappable links, used to gate tenant bootstrap. `errorText` is
/// shown (in the same red as other form validation) when the caller has
/// tried to proceed without the box checked.
class LegalAgreementCheckbox extends StatelessWidget {
  const LegalAgreementCheckbox({
    super.key,
    required this.value,
    required this.onChanged,
    this.enabled = true,
    this.errorText,
  });

  final bool value;
  final ValueChanged<bool> onChanged;
  final bool enabled;
  final String? errorText;

  @override
  Widget build(BuildContext context) {
    final bodyStyle = const TextStyle(
      fontSize: 13,
      color: AppColors.textSecondary,
      height: 1.4,
    );
    final linkStyle = const TextStyle(
      fontSize: 13,
      color: AppColors.brandPrimary,
      fontWeight: FontWeight.w600,
      height: 1.4,
    );

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        InkWell(
          borderRadius: AppRadius.smBorder,
          onTap: enabled ? () => onChanged(!value) : null,
          child: Padding(
            padding: const EdgeInsets.symmetric(vertical: 4),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Checkbox(
                  value: value,
                  activeColor: AppColors.brandPrimary,
                  onChanged:
                      enabled ? (checked) => onChanged(checked ?? false) : null,
                ),
                const SizedBox(width: 4),
                Expanded(
                  child: Padding(
                    padding: const EdgeInsets.only(top: 12),
                    child: Text.rich(
                      TextSpan(
                        style: bodyStyle,
                        children: [
                          const TextSpan(text: 'I agree to the '),
                          TextSpan(
                            text: 'Terms of Service',
                            style: linkStyle,
                            recognizer:
                                TapGestureRecognizer()
                                  ..onTap =
                                      () => Navigator.of(context).push<void>(
                                        MaterialPageRoute(
                                          builder:
                                              (_) =>
                                                  const TermsOfServiceScreen(),
                                        ),
                                      ),
                          ),
                          const TextSpan(text: ' and '),
                          TextSpan(
                            text: 'Privacy Policy',
                            style: linkStyle,
                            recognizer:
                                TapGestureRecognizer()
                                  ..onTap =
                                      () => Navigator.of(context).push<void>(
                                        MaterialPageRoute(
                                          builder:
                                              (_) =>
                                                  const PrivacyPolicyScreen(),
                                        ),
                                      ),
                          ),
                          const TextSpan(text: '.'),
                        ],
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ),
        if (errorText != null)
          Padding(
            padding: const EdgeInsets.only(left: 48, top: 2),
            child: Text(
              errorText!,
              style: const TextStyle(fontSize: 12, color: AppColors.error),
            ),
          ),
      ],
    );
  }
}
