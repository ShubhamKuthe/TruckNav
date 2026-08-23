import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/trucknav_theme.dart';
import '../../widgets/panel_card.dart';
import '../session/session_controller.dart';
import '../session/session_state.dart';

class ConnectorScreen extends ConsumerStatefulWidget {
  const ConnectorScreen({super.key});

  @override
  ConsumerState<ConnectorScreen> createState() => _ConnectorScreenState();
}

class _ConnectorScreenState extends ConsumerState<ConnectorScreen> {
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
    final connector = session.selectedConnector;
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        leading: IconButton(
          onPressed: () => context.go('/'),
          icon: const Icon(Icons.arrow_back_ios_new),
        ),
        title: Text(connector?.name ?? 'Connector'),
        actions: <Widget>[
          IconButton(
            onPressed: () => context.go('/settings'),
            icon: const Icon(Icons.tune_rounded),
          ),
        ],
      ),
      body: Stack(
        children: <Widget>[
          Container(
            decoration: const BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
                colors: <Color>[Color(0xFF131920), TruckNavPalette.background],
              ),
            ),
          ),
          SafeArea(
            top: false,
            child: ListView(
              padding: const EdgeInsets.fromLTRB(24, 8, 24, 32),
              children: <Widget>[
                Text(
                  session.connectionStage == ConnectionStage.reconnecting
                      ? 'Reconnecting to ${connector?.name ?? 'Connector'}'
                      : connector == null
                      ? 'No connector selected'
                      : 'Connected to ${connector.name}',
                  style: theme.textTheme.headlineMedium?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  'Phase 2 stops at discovery plus control signaling, so this screen stays honest about connection and game-selection state.',
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: TruckNavPalette.textSecondary,
                    height: 1.5,
                  ),
                ),
                if (session.weakConnection) ...<Widget>[
                  const SizedBox(height: 20),
                  const _ConnectorBanner(
                    title: 'Weak connection',
                    message:
                        'Ping/pong health checks are still alive, but response timing is degraded.',
                    tone: TruckNavPalette.accent,
                  ),
                ],
                const SizedBox(height: 24),
                PanelCard(
                  padding: const EdgeInsets.all(22),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        'Running game',
                        style: theme.textTheme.titleMedium?.copyWith(
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      const SizedBox(height: 12),
                      Text(
                        gameLabel(session.runningGame),
                        style: theme.textTheme.headlineSmall?.copyWith(
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      const SizedBox(height: 10),
                      Text(
                        'You can preview the game-selection shell here, but production code does not fake ETS2 or ATS as detected yet.',
                        style: theme.textTheme.bodyMedium?.copyWith(
                          color: TruckNavPalette.textSecondary,
                          height: 1.5,
                        ),
                      ),
                      const SizedBox(height: 18),
                      Wrap(
                        spacing: 10,
                        runSpacing: 10,
                        children: <Widget>[
                          ChoiceChip(
                            label: const Text('ETS2'),
                            selected: session.selectedGame ==
                                TruckGame.euroTruckSimulator2,
                            onSelected: (_) => controller.chooseGame(
                              TruckGame.euroTruckSimulator2,
                            ),
                          ),
                          ChoiceChip(
                            label: const Text('ATS'),
                            selected: session.selectedGame ==
                                TruckGame.americanTruckSimulator,
                            onSelected: (_) => controller.chooseGame(
                              TruckGame.americanTruckSimulator,
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 16),
                PanelCard(
                  padding: const EdgeInsets.all(22),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        'Connection health',
                        style: theme.textTheme.titleMedium?.copyWith(
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      const SizedBox(height: 12),
                      _StatusLine(
                        label: 'Stage',
                        value: _stageLabel(session.connectionStage),
                      ),
                      const SizedBox(height: 8),
                      _StatusLine(
                        label: 'Protocol',
                        value: 'v${connector?.protocolVersion ?? 1}',
                      ),
                      const SizedBox(height: 8),
                      _StatusLine(
                        label: 'Auto reconnect',
                        value: session.autoReconnect ? 'Enabled' : 'Disabled',
                      ),
                      const SizedBox(height: 8),
                      _StatusLine(
                        label: 'Weak connection',
                        value: session.weakConnection ? 'Yes' : 'No',
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 22),
                FilledButton(
                  onPressed: () {
                    controller.openGps();
                    context.go('/gps');
                  },
                  style: FilledButton.styleFrom(
                    minimumSize: const Size.fromHeight(58),
                    backgroundColor: TruckNavPalette.accent,
                    foregroundColor: TruckNavPalette.background,
                  ),
                  child: const Text('Open GPS Placeholder'),
                ),
                const SizedBox(height: 12),
                OutlinedButton(
                  onPressed: () async {
                    await controller.disconnect();
                    if (!mounted) {
                      return;
                    }
                    this.context.go('/');
                  },
                  style: OutlinedButton.styleFrom(
                    minimumSize: const Size.fromHeight(56),
                    side: const BorderSide(color: TruckNavPalette.border),
                    foregroundColor: TruckNavPalette.textPrimary,
                  ),
                  child: const Text('Disconnect'),
                ),
              ],
            ),
          ),
          if (session.connectionStage == ConnectionStage.reconnecting)
            const _ReconnectOverlay(),
        ],
      ),
    );
  }
}

class _StatusLine extends StatelessWidget {
  const _StatusLine({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Row(
      children: <Widget>[
        Expanded(
          child: Text(
            label,
            style: theme.textTheme.bodyMedium?.copyWith(
              color: TruckNavPalette.textSecondary,
            ),
          ),
        ),
        Text(
          value,
          style: theme.textTheme.bodyMedium?.copyWith(
            fontWeight: FontWeight.w700,
          ),
        ),
      ],
    );
  }
}

class _ConnectorBanner extends StatelessWidget {
  const _ConnectorBanner({
    required this.title,
    required this.message,
    required this.tone,
  });

  final String title;
  final String message;
  final Color tone;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return PanelCard(
      padding: const EdgeInsets.all(18),
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
    );
  }
}

class _ReconnectOverlay extends StatelessWidget {
  const _ReconnectOverlay();

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: Colors.black54,
      child: Center(
        child: PanelCard(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: const <Widget>[
              CircularProgressIndicator(),
              SizedBox(height: 18),
              Text(
                'Reconnecting',
                style: TextStyle(fontSize: 22, fontWeight: FontWeight.w800),
              ),
              SizedBox(height: 8),
              Text(
                'TruckNav is waiting for the previous Connector to reappear on the LAN before opening a new WebSocket session.',
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

String _stageLabel(ConnectionStage stage) => switch (stage) {
      ConnectionStage.gameSelection => 'Connected',
      ConnectionStage.waitingForStream => 'Waiting for stream',
      ConnectionStage.streaming => 'Streaming',
      ConnectionStage.reconnecting => 'Reconnecting',
      ConnectionStage.versionMismatch => 'Update required',
      ConnectionStage.connectionFailed => 'Connection failed',
      ConnectionStage.localNetworkAccessRequired => 'LAN access required',
      ConnectionStage.connecting => 'Connecting',
      _ => 'Discovery',
    };
