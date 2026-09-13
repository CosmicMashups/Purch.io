import 'package:flutter/material.dart';

import '../../../../core/theming/app_tokens.dart';

/// Horizontal step tracker used by multi-step onboarding wizards (tenant
/// bootstrap, device setup). Each step is a filled dot connected by a line;
/// the current step also carries its label so the wizard's stage is legible
/// at a glance without adding a second heading.
class OnboardingStepProgress extends StatelessWidget {
  const OnboardingStepProgress({
    super.key,
    required this.stepLabels,
    required this.currentStep,
  });

  final List<String> stepLabels;

  /// Zero-based index of the active step.
  final int currentStep;

  @override
  Widget build(BuildContext context) {
    return Column(
      children: [
        Row(
          children: [
            for (var i = 0; i < stepLabels.length; i++) ...[
              _StepDot(
                index: i,
                isActive: i == currentStep,
                isComplete: i < currentStep,
              ),
              if (i != stepLabels.length - 1)
                Expanded(
                  child: Container(
                    height: 2,
                    margin: const EdgeInsets.symmetric(horizontal: 4),
                    color:
                        i < currentStep
                            ? AppColors.brandPrimary
                            : AppColors.border,
                  ),
                ),
            ],
          ],
        ),
        const SizedBox(height: 10),
        Text(
          'Step ${currentStep + 1} of ${stepLabels.length} · '
          '${stepLabels[currentStep]}',
          style: const TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            color: AppColors.textSecondary,
          ),
        ),
      ],
    );
  }
}

class _StepDot extends StatelessWidget {
  const _StepDot({
    required this.index,
    required this.isActive,
    required this.isComplete,
  });

  final int index;
  final bool isActive;
  final bool isComplete;

  @override
  Widget build(BuildContext context) {
    final filled = isActive || isComplete;
    return Container(
      width: 28,
      height: 28,
      alignment: Alignment.center,
      decoration: BoxDecoration(
        color: filled ? AppColors.brandPrimary : AppColors.card,
        shape: BoxShape.circle,
        border: Border.all(
          color: filled ? AppColors.brandPrimary : AppColors.border,
          width: 2,
        ),
      ),
      child:
          isComplete
              ? const Icon(Icons.check_rounded, size: 16, color: Colors.white)
              : Text(
                '${index + 1}',
                style: TextStyle(
                  fontSize: 13,
                  fontWeight: FontWeight.w700,
                  color: isActive ? Colors.white : AppColors.textMuted,
                ),
              ),
    );
  }
}
