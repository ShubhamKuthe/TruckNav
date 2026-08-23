import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:uuid/uuid.dart';

import 'session_state.dart';

final sessionPreferencesStoreProvider = Provider<SessionPreferencesStore>(
  (ref) => SharedPreferencesSessionPreferencesStore(),
);

class SessionPreferencesSnapshot {
  const SessionPreferencesSnapshot({
    required this.deviceId,
    required this.deviceName,
    required this.isFirstLaunch,
    required this.autoReconnect,
    required this.autoOpenRunningGame,
    required this.lastConnectedDeviceId,
    required this.lastConnectedDeviceName,
  });

  final String deviceId;
  final String deviceName;
  final bool isFirstLaunch;
  final bool autoReconnect;
  final bool autoOpenRunningGame;
  final String? lastConnectedDeviceId;
  final String? lastConnectedDeviceName;
}

abstract interface class SessionPreferencesStore {
  Future<SessionPreferencesSnapshot> loadOrCreateProfile();

  Future<void> saveDeviceName(String deviceName);

  Future<void> saveAutoReconnect(bool value);

  Future<void> saveAutoOpenRunningGame(bool value);

  Future<void> saveLastConnectedConnector(ConnectorSummary connector);
}

class SharedPreferencesSessionPreferencesStore
    implements SessionPreferencesStore {
  static const String _deviceIdKey = 'trucknav.deviceId';
  static const String _deviceNameKey = 'trucknav.deviceName';
  static const String _autoReconnectKey = 'trucknav.autoReconnect';
  static const String _autoOpenRunningGameKey = 'trucknav.autoOpenRunningGame';
  static const String _lastConnectedDeviceIdKey =
      'trucknav.lastConnectedDeviceId';
  static const String _lastConnectedDeviceNameKey =
      'trucknav.lastConnectedDeviceName';

  static const String _defaultDeviceName = 'HEN';
  static const Uuid _uuid = Uuid();

  @override
  Future<SessionPreferencesSnapshot> loadOrCreateProfile() async {
    final preferences = await SharedPreferences.getInstance();
    var deviceId = preferences.getString(_deviceIdKey);
    if (deviceId == null || deviceId.isEmpty) {
      deviceId = _uuid.v4();
      await preferences.setString(_deviceIdKey, deviceId);
    }

    final storedDeviceName = preferences.getString(_deviceNameKey);

    return SessionPreferencesSnapshot(
      deviceId: deviceId,
      deviceName: storedDeviceName?.trim().isNotEmpty == true
          ? storedDeviceName!.trim()
          : _defaultDeviceName,
      isFirstLaunch: storedDeviceName == null || storedDeviceName.trim().isEmpty,
      autoReconnect: preferences.getBool(_autoReconnectKey) ?? true,
      autoOpenRunningGame:
          preferences.getBool(_autoOpenRunningGameKey) ?? false,
      lastConnectedDeviceId: preferences.getString(_lastConnectedDeviceIdKey),
      lastConnectedDeviceName: preferences.getString(
        _lastConnectedDeviceNameKey,
      ),
    );
  }

  @override
  Future<void> saveDeviceName(String deviceName) async {
    final preferences = await SharedPreferences.getInstance();
    await preferences.setString(_deviceNameKey, deviceName.trim());
  }

  @override
  Future<void> saveAutoReconnect(bool value) async {
    final preferences = await SharedPreferences.getInstance();
    await preferences.setBool(_autoReconnectKey, value);
  }

  @override
  Future<void> saveAutoOpenRunningGame(bool value) async {
    final preferences = await SharedPreferences.getInstance();
    await preferences.setBool(_autoOpenRunningGameKey, value);
  }

  @override
  Future<void> saveLastConnectedConnector(ConnectorSummary connector) async {
    final preferences = await SharedPreferences.getInstance();
    await preferences.setString(_lastConnectedDeviceIdKey, connector.id);
    await preferences.setString(_lastConnectedDeviceNameKey, connector.name);
  }
}
