import 'dart:convert';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'hardware_config.dart';

class HardwareConfigStorage {
  HardwareConfigStorage({FlutterSecureStorage? storage})
      : _storage = storage ?? const FlutterSecureStorage();

  static const _configKey = 'purch_hardware_configuration';
  final FlutterSecureStorage _storage;

  Future<void> saveConfig(HardwareConfig config) async {
    final jsonStr = jsonEncode(config.toJson());
    await _storage.write(key: _configKey, value: jsonStr);
  }

  Future<HardwareConfig> loadConfig() async {
    try {
      final jsonStr = await _storage.read(key: _configKey);
      if (jsonStr == null || jsonStr.isEmpty) {
        return const HardwareConfig();
      }
      final map = jsonDecode(jsonStr) as Map<String, dynamic>;
      return HardwareConfig.fromJson(map);
    } catch (_) {
      return const HardwareConfig();
    }
  }

  Future<void> clear() => _storage.delete(key: _configKey);
}
