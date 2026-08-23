import 'dart:async';
import 'dart:io';

import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

final localNetworkPermissionServiceProvider =
    Provider<LocalNetworkPermissionService>((ref) {
      final service = MethodChannelLocalNetworkPermissionService();
      ref.onDispose(() => unawaited(service.dispose()));
      return service;
    });

enum LocalNetworkPermissionStatus { granted, denied }

class LocalNetworkPermissionState {
  const LocalNetworkPermissionState({
    required this.status,
    required this.multicastLockHeld,
    this.message,
    this.targetSdkVersion,
    this.requiresRuntimePermission = false,
  });

  final LocalNetworkPermissionStatus status;
  final bool multicastLockHeld;
  final String? message;
  final int? targetSdkVersion;
  final bool requiresRuntimePermission;

  bool get isGranted => status == LocalNetworkPermissionStatus.granted;
}

class LocalNetworkPermissionException implements Exception {
  const LocalNetworkPermissionException(
    this.message, {
    this.targetSdkVersion,
    this.requiresRuntimePermission = false,
  });

  final String message;
  final int? targetSdkVersion;
  final bool requiresRuntimePermission;

  @override
  String toString() => message;
}

abstract interface class LocalNetworkPermissionService {
  Future<LocalNetworkPermissionState> ensureDiscoveryAccess();

  Future<void> releaseDiscoveryAccess();

  Future<void> dispose();
}

class MethodChannelLocalNetworkPermissionService
    implements LocalNetworkPermissionService {
  static const MethodChannel _channel = MethodChannel(
    'trucknav/local_network',
  );

  @override
  Future<LocalNetworkPermissionState> ensureDiscoveryAccess() async {
    final state = await _invokeState('prepareDiscoveryAccess');
    if (!state.isGranted) {
      throw LocalNetworkPermissionException(
        state.message ??
            'Local network access is required before TruckNav can discover a Connector on this LAN.',
        targetSdkVersion: state.targetSdkVersion,
        requiresRuntimePermission: state.requiresRuntimePermission,
      );
    }

    return state;
  }

  @override
  Future<void> releaseDiscoveryAccess() async {
    if (!Platform.isAndroid) {
      return;
    }

    try {
      await _channel.invokeMethod<void>('releaseDiscoveryAccess');
    } on MissingPluginException {
      // The platform channel is unavailable in tests and non-Android runs.
    }
  }

  @override
  Future<void> dispose() => releaseDiscoveryAccess();

  Future<LocalNetworkPermissionState> _invokeState(String method) async {
    if (!Platform.isAndroid) {
      return const LocalNetworkPermissionState(
        status: LocalNetworkPermissionStatus.granted,
        multicastLockHeld: false,
      );
    }

    try {
      final payload =
          await _channel.invokeMapMethod<String, Object?>(method) ??
          const <String, Object?>{};
      return LocalNetworkPermissionState(
        status: payload['status'] == 'denied'
            ? LocalNetworkPermissionStatus.denied
            : LocalNetworkPermissionStatus.granted,
        multicastLockHeld: payload['multicastLockHeld'] as bool? ?? false,
        message: payload['message'] as String?,
        targetSdkVersion: payload['targetSdkVersion'] as int?,
        requiresRuntimePermission:
            payload['requiresRuntimePermission'] as bool? ?? false,
      );
    } on MissingPluginException {
      return const LocalNetworkPermissionState(
        status: LocalNetworkPermissionStatus.granted,
        multicastLockHeld: false,
      );
    }
  }
}
