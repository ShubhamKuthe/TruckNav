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
    ref.listen<ConnectionStage>(
      sessionControllerProvider.select((state) => state.connectionStage),
      (previous, next) {
        if (!mounted) {
          return;
        }

        if (next == ConnectionStage.waitingForStream &&
            previous != ConnectionStage.waitingForStream) {
          context.go('/gps');
        }
      },
    );

    final session = ref.watch(sessionControllerProvider);
    final controller = ref.read(sessionControllerProvider.notifier);
    final connector = session.selectedConnector;
    final theme = Theme.of(context);
    final runningGames = session.supportedGames.runningGames;
    final otherGames = TruckGame.values
        .where((game) => game != TruckGame.none && !session.supportedGames.isRunning(game))
        .toList(growable: false);
    final selectedGameMissing = session.selectedGame != TruckGame.none &&
        !session.supportedGames.isRunning(session.selectedGame);

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
                      : 'Devices are paired',
                  style: theme.textTheme.headlineMedium?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 8),
                Text(
                  connector == null
                      ? 'Choose a nearby computer first.'
                      : '${session.deviceName} is paired with ${connector.name}. TruckNav will keep this session alive even if the game opens or closes.',
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: TruckNavPalette.textSecondary,
                    height: 1.5,
                  ),
                ),
                if (selectedGameMissing) ...<Widget>[
                  const SizedBox(height: 20),
                  _GameMissingBanner(
                    connectorName: connector?.name ?? 'ROCK',
                    game: session.selectedGame,
                    onCheckAgain: controller.requestGameStateRefresh,
                    onChooseAnotherGame: () {
                      controller.returnToGameSelection();
                    },
                  ),
                ],
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
                Text(
                  runningGames.isEmpty
                      ? 'No supported game detected'
                      : 'Detected on ${connector?.name ?? 'ROCK'}',
                  style: theme.textTheme.titleLarge?.copyWith(
                    fontWeight: FontWeight.w800,
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  runningGames.isEmpty
                      ? 'Start Euro Truck Simulator 2 or American Truck Simulator on the PC.'
                      : 'Open GPS only after choosing the game you want to follow.',
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: TruckNavPalette.textSecondary,
                    height: 1.5,
                  ),
                ),
                const SizedBox(height: 14),
                if (runningGames.isEmpty)
                  ..._buildGameCards(
                    session: session,
                    games: const <TruckGame>[
                      TruckGame.euroTruckSimulator2,
                      TruckGame.americanTruckSimulator,
                    ],
                    isRunning: false,
                    controller: controller,
                  )
                else
                  ..._buildGameCards(
                    session: session,
                    games: runningGames,
                    isRunning: true,
                    controller: controller,
                  ),
                if (otherGames.isNotEmpty) ...<Widget>[
                  const SizedBox(height: 10),
                  Text(
                    'Other Games',
                    style: theme.textTheme.titleLarge?.copyWith(
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                  const SizedBox(height: 12),
                  ..._buildGameCards(
                    session: session,
                    games: otherGames,
                    isRunning: false,
                    controller: controller,
                  ),
                ],
                const SizedBox(height: 10),
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
                        label: 'Automatically open running game',
                        value:
                            session.autoOpenRunningGame ? 'Enabled' : 'Disabled',
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 22),
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

  List<Widget> _buildGameCards({
    required SessionState session,
    required List<TruckGame> games,
    required bool isRunning,
    required SessionController controller,
  }) {
    return games
        .map(
          (game) => Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: _GameStatusCard(
              game: game,
              isRunning: isRunning,
              isSelected: session.selectedGame == game,
              onOpenGps: isRunning
                  ? () {
                      controller.chooseGame(game);
                      controller.openGps();
                    }
                  : null,
            ),
          ),
        )
        .toList(growable: false);
  }
}

class _GameStatusCard extends StatelessWidget {
  const _GameStatusCard({
    required this.game,
    required this.isRunning,
    required this.isSelected,
    required this.onOpenGps,
  });

  final TruckGame game;
  final bool isRunning;
  final bool isSelected;
  final VoidCallback? onOpenGps;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final accentColor = isRunning
        ? TruckNavPalette.success
        : TruckNavPalette.textSecondary;

    return PanelCard(
      padding: const EdgeInsets.all(22),
      child: Row(
        children: <Widget>[
          Container(
            width: 52,
            height: 52,
            decoration: BoxDecoration(
              color: accentColor.withValues(alpha: 0.14),
              borderRadius: BorderRadius.circular(16),
            ),
            child: Icon(
              Icons.sports_esports_rounded,
              color: accentColor,
            ),
          ),
          const SizedBox(width: 16),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  gameLabel(game).toUpperCase(),
                  style: theme.textTheme.labelLarge?.copyWith(
                    color: TruckNavPalette.textSecondary,
                    letterSpacing: 0.6,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 6),
                Text(
                  isRunning ? 'Running' : 'Not running',
                  style: theme.textTheme.titleLarge?.copyWith(
                    fontWeight: FontWeight.w800,
                    color: isRunning
                        ? TruckNavPalette.success
                        : TruckNavPalette.textPrimary,
                  ),
                ),
                if (isSelected) ...<Widget>[
                  const SizedBox(height: 6),
                  Text(
                    'Selected for GPS',
                    style: theme.textTheme.bodyMedium?.copyWith(
                      color: TruckNavPalette.textSecondary,
                    ),
                  ),
                ],
              ],
            ),
          ),
          if (onOpenGps != null)
            FilledButton(
              onPressed: onOpenGps,
              style: FilledButton.styleFrom(
                backgroundColor: TruckNavPalette.accent,
                foregroundColor: TruckNavPalette.background,
              ),
              child: const Text('Open GPS'),
            ),
        ],
      ),
    );
  }
}

class _GameMissingBanner extends StatelessWidget {
  const _GameMissingBanner({
    required this.connectorName,
    required this.game,
    required this.onCheckAgain,
    required this.onChooseAnotherGame,
  });

  final String connectorName;
  final TruckGame game;
  final Future<void> Function() onCheckAgain;
  final VoidCallback onChooseAnotherGame;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return PanelCard(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            'Game not detected',
            style: theme.textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w800,
              color: TruckNavPalette.accent,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            'Start ${gameLabel(game)} on $connectorName to continue.',
            style: theme.textTheme.bodyMedium?.copyWith(
              color: TruckNavPalette.textSecondary,
              height: 1.45,
            ),
          ),
          const SizedBox(height: 16),
          Wrap(
            spacing: 10,
            runSpacing: 10,
            children: <Widget>[
              FilledButton(
                onPressed: () {
                  onCheckAgain();
                },
                style: FilledButton.styleFrom(
                  backgroundColor: TruckNavPalette.accent,
                  foregroundColor: TruckNavPalette.background,
                ),
                child: const Text('Check Again'),
              ),
              OutlinedButton(
                onPressed: onChooseAnotherGame,
                style: OutlinedButton.styleFrom(
                  side: const BorderSide(color: TruckNavPalette.border),
                  foregroundColor: TruckNavPalette.textPrimary,
                ),
                child: const Text('Choose Another Game'),
              ),
            ],
          ),
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
  ConnectionStage.waitingForStream => 'Waiting for game state',
  ConnectionStage.streaming => 'Streaming',
  ConnectionStage.reconnecting => 'Reconnecting',
  ConnectionStage.versionMismatch => 'Update required',
  ConnectionStage.connectionFailed => 'Connection failed',
  ConnectionStage.localNetworkAccessRequired => 'LAN access required',
  ConnectionStage.connecting => 'Connecting',
  _ => 'Discovery',
};
