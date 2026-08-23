import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/trucknav_theme.dart';
import '../../widgets/panel_card.dart';
import '../session/session_controller.dart';
import '../session/session_state.dart';

class DiscoveryScreen extends ConsumerStatefulWidget {
  const DiscoveryScreen({super.key});

  @override
  ConsumerState<DiscoveryScreen> createState() => _DiscoveryScreenState();
}

class _DiscoveryScreenState extends ConsumerState<DiscoveryScreen> {
  late final TextEditingController _nameController;

  @override
  void initState() {
    super.initState();
    _nameController = TextEditingController(text: 'HEN');
    Future<void>.microtask(
      () => ref.read(sessionControllerProvider.notifier).activateDiscovery(),
    );
  }

  @override
  void dispose() {
    _nameController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    ref.listen<SessionNotice?>(
      sessionControllerProvider.select((state) => state.notice),
      (previous, next) {
        if (next == null || !mounted) {
          return;
        }

        ScaffoldMessenger.of(context)
          ..hideCurrentSnackBar()
          ..showSnackBar(SnackBar(content: Text(next.message)));
        ref.read(sessionControllerProvider.notifier).clearNotice();
      },
    );

    final session = ref.watch(sessionControllerProvider);
    final controller = ref.read(sessionControllerProvider.notifier);
    final theme = Theme.of(context);
    final statusCard = _buildStatusCard(session);

    if (session.deviceName.isNotEmpty &&
        _nameController.text != session.deviceName &&
        session.connectionStage != ConnectionStage.namingDevice) {
      _nameController.text = session.deviceName;
    }

    return Scaffold(
      body: Container(
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: <Color>[Color(0xFF10151B), TruckNavPalette.background],
          ),
        ),
        child: SafeArea(
          child: session.connectionStage == ConnectionStage.initializing
              ? const Center(child: CircularProgressIndicator())
              : ListView(
                  padding: const EdgeInsets.fromLTRB(24, 16, 24, 24),
                  children: <Widget>[
                    Text(
                      'TruckNav',
                      style: theme.textTheme.displaySmall?.copyWith(
                        fontWeight: FontWeight.w700,
                        letterSpacing: -1.4,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'Discover your Connector automatically on the same local network and keep the control path free of manual IP entry.',
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: TruckNavPalette.textSecondary,
                        height: 1.45,
                      ),
                    ),
                    const SizedBox(height: 24),
                    if (session.connectionStage == ConnectionStage.namingDevice)
                      _FirstLaunchCard(
                        controller: _nameController,
                        onContinue: () => controller.completeFirstLaunch(
                          _nameController.text,
                        ),
                      )
                    else ...<Widget>[
                      _DeviceCard(deviceName: session.deviceName),
                      const SizedBox(height: 16),
                      const _LocalNetworkCard(),
                      const SizedBox(height: 16),
                      if (statusCard != null) ...<Widget>[
                        statusCard,
                        const SizedBox(height: 16),
                      ],
                      Row(
                        children: <Widget>[
                          Expanded(
                            child: Text(
                              'Available Connectors',
                              style: theme.textTheme.titleLarge?.copyWith(
                                fontWeight: FontWeight.w700,
                              ),
                            ),
                          ),
                          TextButton(
                            onPressed: controller.scanAgain,
                            child: const Text('Scan Again'),
                          ),
                        ],
                      ),
                      const SizedBox(height: 12),
                      if (session.availableConnectors.isEmpty)
                        _EmptyDiscoveryState(
                          stage: session.connectionStage,
                          message: session.connectionError,
                          isScanning:
                              session.connectionStage == ConnectionStage.discovering,
                          onScanAgain: controller.scanAgain,
                        )
                      else
                        ...session.availableConnectors.map(
                          (connector) => Padding(
                            padding: const EdgeInsets.only(bottom: 14),
                            child: _ConnectorCard(
                              connector: connector,
                              isConnecting:
                                  session.connectionStage ==
                                          ConnectionStage.connecting &&
                                      session.selectedConnector?.id ==
                                          connector.id,
                              onConnect: () async {
                                await controller.connectToConnector(connector);
                                if (!mounted) {
                                  return;
                                }

                                if (ref
                                        .read(sessionControllerProvider)
                                        .connectionStage ==
                                    ConnectionStage.gameSelection) {
                                  this.context.go('/connector');
                                }
                              },
                            ),
                          ),
                        ),
                    ],
                  ],
                ),
        ),
      ),
    );
  }

  Widget? _buildStatusCard(SessionState session) {
    switch (session.connectionStage) {
      case ConnectionStage.connecting:
        return _StateBanner(
          title: 'Connecting',
          message:
              'Sending hello to ${session.selectedConnector?.name ?? 'Connector'} and waiting for hello_ack.',
          tone: TruckNavPalette.accent,
          trailing: const SizedBox(
            width: 22,
            height: 22,
            child: CircularProgressIndicator(strokeWidth: 2),
          ),
        );
      case ConnectionStage.versionMismatch:
        return _StateBanner(
          title: 'Update required',
          message: session.connectionError ??
              'The Connector is running an incompatible protocol version.',
          tone: TruckNavPalette.error,
        );
      case ConnectionStage.connectionFailed:
        return _StateBanner(
          title: 'Connection failed',
          message: session.connectionError ??
              'The Connector did not accept the current session.',
          tone: TruckNavPalette.error,
        );
      case ConnectionStage.localNetworkAccessRequired:
        return _StateBanner(
          title: 'Local network access required',
          message: session.connectionError ??
              'TruckNav needs local network access before it can scan for a Connector on this LAN.',
          tone: TruckNavPalette.error,
        );
      case ConnectionStage.reconnecting:
        return _StateBanner(
          title: 'Reconnecting',
          message:
              'Waiting for ${session.selectedConnector?.name ?? 'the previous Connector'} to reappear before reconnecting.',
          tone: TruckNavPalette.accent,
        );
      default:
        return null;
    }
  }
}

class _DeviceCard extends StatelessWidget {
  const _DeviceCard({required this.deviceName});

  final String deviceName;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return PanelCard(
      padding: const EdgeInsets.all(22),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            'This device',
            style: theme.textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            deviceName,
            style: theme.textTheme.headlineSmall?.copyWith(
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            'Android mobile identity is persistent and no longer tied to IP, MAC address, or hostname.',
            style: theme.textTheme.bodyMedium?.copyWith(
              color: TruckNavPalette.textSecondary,
              height: 1.5,
            ),
          ),
        ],
      ),
    );
  }
}

class _LocalNetworkCard extends StatelessWidget {
  const _LocalNetworkCard();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return PanelCard(
      padding: const EdgeInsets.all(22),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            'Local network only',
            style: theme.textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            'TruckNav discovery uses mDNS on the same LAN. The Windows PC can stay on Ethernet while this Android device stays on Wi-Fi.',
            style: theme.textTheme.bodyMedium?.copyWith(
              color: TruckNavPalette.textSecondary,
              height: 1.5,
            ),
          ),
        ],
      ),
    );
  }
}

class _FirstLaunchCard extends StatelessWidget {
  const _FirstLaunchCard({
    required this.controller,
    required this.onContinue,
  });

  final TextEditingController controller;
  final Future<void> Function() onContinue;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return PanelCard(
      padding: const EdgeInsets.all(24),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            'Name this device',
            style: theme.textTheme.headlineSmall?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 10),
          Text(
            'Your Android device keeps a persistent TruckNav identity. You can rename it now before local discovery starts.',
            style: theme.textTheme.bodyMedium?.copyWith(
              color: TruckNavPalette.textSecondary,
              height: 1.5,
            ),
          ),
          const SizedBox(height: 18),
          TextField(
            controller: controller,
            maxLength: 32,
            decoration: const InputDecoration(
              labelText: 'Device name',
              hintText: 'HEN',
            ),
          ),
          const SizedBox(height: 8),
          FilledButton(
            onPressed: onContinue,
            style: FilledButton.styleFrom(
              minimumSize: const Size.fromHeight(56),
              backgroundColor: TruckNavPalette.accent,
              foregroundColor: TruckNavPalette.background,
            ),
            child: const Text('Continue to Discovery'),
          ),
        ],
      ),
    );
  }
}

class _ConnectorCard extends StatelessWidget {
  const _ConnectorCard({
    required this.connector,
    required this.isConnecting,
    required this.onConnect,
  });

  final ConnectorSummary connector;
  final bool isConnecting;
  final Future<void> Function() onConnect;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return PanelCard(
      child: Row(
        children: <Widget>[
          Container(
            width: 54,
            height: 54,
            decoration: BoxDecoration(
              color: TruckNavPalette.accent.withValues(alpha: 0.18),
              borderRadius: BorderRadius.circular(18),
            ),
            child: const Icon(
              Icons.computer_rounded,
              color: TruckNavPalette.accent,
            ),
          ),
          const SizedBox(width: 18),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: <Widget>[
                    Text(
                      connector.name,
                      style: theme.textTheme.titleLarge?.copyWith(
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    if (connector.isPrevious)
                      Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 10,
                          vertical: 6,
                        ),
                        decoration: BoxDecoration(
                          color: TruckNavPalette.surfaceRaised,
                          borderRadius: BorderRadius.circular(999),
                        ),
                        child: Text(
                          'PREVIOUS',
                          style: theme.textTheme.labelSmall?.copyWith(
                            fontWeight: FontWeight.w800,
                            letterSpacing: 1.0,
                          ),
                        ),
                      ),
                  ],
                ),
                const SizedBox(height: 4),
                Text(
                  '${connector.platformLabel}  •  Ready',
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: TruckNavPalette.textSecondary,
                  ),
                ),
                const SizedBox(height: 2),
                Text(
                  '${connector.networkLabel}  •  Protocol v${connector.protocolVersion}',
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: TruckNavPalette.textSecondary,
                  ),
                ),
              ],
            ),
          ),
          FilledButton(
            onPressed: isConnecting ? null : onConnect,
            child: isConnecting
                ? const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  )
                : const Text('Connect'),
          ),
        ],
      ),
    );
  }
}

class _EmptyDiscoveryState extends StatelessWidget {
  const _EmptyDiscoveryState({
    required this.stage,
    required this.message,
    required this.isScanning,
    required this.onScanAgain,
  });

  final ConnectionStage stage;
  final String? message;
  final bool isScanning;
  final VoidCallback onScanAgain;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return PanelCard(
      padding: const EdgeInsets.all(24),
      child: Column(
        children: <Widget>[
          Icon(
            isScanning
                ? Icons.radar_rounded
                : stage == ConnectionStage.localNetworkAccessRequired
                ? Icons.shield_outlined
                : Icons.wifi_find_rounded,
            size: 42,
            color: TruckNavPalette.accent,
          ),
          const SizedBox(height: 14),
          Text(
            isScanning
                ? 'Searching for Connectors'
                : stage == ConnectionStage.localNetworkAccessRequired
                ? 'Local network access required'
                : 'No PC found',
            textAlign: TextAlign.center,
            style: theme.textTheme.titleLarge?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            isScanning
                ? 'TruckNav is scanning for _trucknav._tcp.local announcements on the current network.'
                : stage == ConnectionStage.localNetworkAccessRequired
                ? (message ??
                      'TruckNav needs local network access before it can discover a Windows Connector on this LAN.')
                : 'No Windows Connector is advertising on this network yet. Keep both devices on the same LAN and try again.',
            textAlign: TextAlign.center,
            style: theme.textTheme.bodyMedium?.copyWith(
              color: TruckNavPalette.textSecondary,
              height: 1.5,
            ),
          ),
          if (!isScanning) ...<Widget>[
            const SizedBox(height: 18),
            OutlinedButton(
              onPressed: onScanAgain,
              style: OutlinedButton.styleFrom(
                side: const BorderSide(color: TruckNavPalette.border),
                foregroundColor: TruckNavPalette.textPrimary,
              ),
              child: const Text('Scan Again'),
            ),
          ],
        ],
      ),
    );
  }
}

class _StateBanner extends StatelessWidget {
  const _StateBanner({
    required this.title,
    required this.message,
    required this.tone,
    this.trailing,
  });

  final String title;
  final String message;
  final Color tone;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return PanelCard(
      padding: const EdgeInsets.all(20),
      child: Row(
        children: <Widget>[
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  title,
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w800,
                    color: tone,
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  message,
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: TruckNavPalette.textSecondary,
                    height: 1.45,
                  ),
                ),
              ],
            ),
          ),
          if (trailing != null) ...<Widget>[
            const SizedBox(width: 16),
            trailing!,
          ],
        ],
      ),
    );
  }
}
