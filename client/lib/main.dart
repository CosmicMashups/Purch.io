import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/config/app_config.dart';
import 'core/routing/app_router.dart';
import 'core/storage/server_connection_storage.dart';
import 'core/theming/app_theme.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Restore a manually-entered Local/on-prem server address, if this device
  // has one saved — see AppConfig's doc comment and ServerConnectionScreen.
  final savedBaseUrl = await ServerConnectionStorage().readBaseUrl();
  AppConfig.setApiBaseUrlOverride(savedBaseUrl);

  runApp(const ProviderScope(child: PurchApp()));
}

class PurchApp extends ConsumerWidget {
  const PurchApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    return MaterialApp.router(
      title: 'Purch.io',
      theme: AppTheme.staffTheme(),
      routerConfig: ref.watch(appRouterProvider),
    );
  }
}
