import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../../../core/theming/app_tokens.dart';
import '../providers/catalog_providers.dart';

/// Shown while the items and categories on screen are the saved copy because the server could not
/// be reached, so a cashier knows prices and stock may be out of date. Renders nothing when the
/// catalog is confirmed current.
class StaleCatalogBanner extends ConsumerWidget {
  const StaleCatalogBanner({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final staleSince = ref.watch(catalogStaleSinceProvider);
    if (staleSince == null) {
      return const SizedBox.shrink();
    }

    final message =
        'Offline — showing the catalog as of ${describeAsOf(staleSince, DateTime.now())}. '
        'Prices and stock may be out of date.';

    return Semantics(
      liveRegion: true,
      label: message,
      child: Material(
        color: AppColors.accentWarm.withValues(alpha: 0.12),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
          child: Row(
            children: [
              const Icon(
                Icons.history_rounded,
                size: 18,
                color: AppColors.accentWarm,
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Text(
                  message,
                  style: const TextStyle(
                    fontSize: 13,
                    fontWeight: FontWeight.w600,
                    color: AppColors.accentWarm,
                  ),
                ),
              ),
              TextButton(
                onPressed: () {
                  ref.invalidate(itemListProvider);
                  ref.invalidate(categoryListProvider);
                },
                child: const Text('Refresh'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

/// "10:42" for today, otherwise "21 Sep, 10:42". Local time, 24-hour, matching the rest of the app
/// (there is no intl dependency yet).
String describeAsOf(DateTime moment, DateTime now) {
  final local = moment.toLocal();
  final today = now.toLocal();
  String two(int n) => n.toString().padLeft(2, '0');
  final time = '${two(local.hour)}:${two(local.minute)}';
  final sameDay =
      local.year == today.year &&
      local.month == today.month &&
      local.day == today.day;
  if (sameDay) return time;
  const months = [
    'Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun',
    'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec',
  ];
  return '${local.day} ${months[local.month - 1]}, $time';
}
