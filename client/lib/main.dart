import 'dart:io';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:path/path.dart' as p;
import 'package:path_provider/path_provider.dart';

import 'core/config/app_config.dart';
import 'core/data/data_refresh.dart';
import 'core/diagnostics/crash_reporter.dart';
import 'core/routing/app_router.dart';
import 'core/session/session_scope.dart';
import 'core/storage/server_connection_storage.dart';
import 'core/theming/theme_builder.dart';

void main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Installed before anything else can fail, so a startup error is recorded too.
  await _installCrashReporting();

  // Restore a manually-entered Local/on-prem server address, if this device
  // has one saved — see AppConfig's doc comment and ServerConnectionScreen.
  final savedBaseUrl = await ServerConnectionStorage().readBaseUrl();
  AppConfig.setApiBaseUrlOverride(savedBaseUrl);

  runApp(const SessionScope(child: PurchApp()));
}

Future<void> _installCrashReporting() async {
  try {
    final directory = await getApplicationDocumentsDirectory();
    installGlobalErrorHandlers(
      FileCrashReporter(File(p.join(directory.path, 'crash.log'))),
    );
  } on Object catch (error) {
    // No writable location: keep Flutter's default handlers rather than block startup.
    debugPrint('Crash reporting unavailable: $error');
  }
}

class PurchApp extends ConsumerStatefulWidget {
  const PurchApp({super.key});

  @override
  ConsumerState<PurchApp> createState() => _PurchAppState();
}

class _PurchAppState extends ConsumerState<PurchApp>
    with WidgetsBindingObserver {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addObserver(this);
  }

  @override
  void dispose() {
    WidgetsBinding.instance.removeObserver(this);
    super.dispose();
  }

  /// Coming back to the app (after minimising, a screen lock, or a long idle)
  /// is when cached dashboard/inventory data is most likely to be stale, so
  /// drop it and let whatever is on screen refetch.
  @override
  void didChangeAppLifecycleState(AppLifecycleState state) {
    if (state == AppLifecycleState.resumed) {
      refreshStockAndSalesDataFromWidget(ref);
    }
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp.router(
      title: 'Purch.io',
      // Built at runtime from the tenant's cached branding colours, so an
      // admin's Business Settings change re-themes the app immediately.
      theme: ref.watch(staffThemeProvider),
      routerConfig: ref.watch(appRouterProvider),
    );
  }
}
