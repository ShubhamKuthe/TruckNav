import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/trucknav_theme.dart';
import 'gps_video_surface_placeholder.dart';
import '../session/session_controller.dart';
import '../session/session_state.dart';

class GpsScreen extends ConsumerStatefulWidget {
  const GpsScreen({super.key});

  @override
  ConsumerState<GpsScreen> createState() => _GpsScreenState();
}

class _GpsScreenState extends ConsumerState<GpsScreen> {
  static const Duration _controlsVisibleDuration = Duration(seconds: 3);

  Timer? _hideTimer;
  bool _controlsVisible = true;

  @override
  void initState() {
    super.initState();
    _restartTimer();
  }

  @override
  void dispose() {
    _hideTimer?.cancel();
    super.dispose();
  }

  void _restartTimer() {
    _hideTimer?.cancel();
    _hideTimer = Timer(_controlsVisibleDuration, () {
      if (mounted) {
        setState(() => _controlsVisible = false);
      }
    });
  }

  void _showControls() {
    setState(() => _controlsVisible = true);
    _restartTimer();
  }

  Future<void> _openQuickMenu(BuildContext context) async {
    await showModalBottomSheet<void>(
      context: context,
      backgroundColor: TruckNavPalette.surface,
      builder: (context) {
        return SafeArea(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              ListTile(
                leading: const Icon(Icons.grid_view_rounded),
                title: const Text('Game selection'),
                onTap: () {
                  ref.read(sessionControllerProvider.notifier).returnToGameSelection();
                  Navigator.of(context).pop();
                  this.context.go('/connector');
                },
              ),
              ListTile(
                leading: const Icon(Icons.tune_rounded),
                title: const Text('Settings'),
                onTap: () {
                  Navigator.of(context).pop();
                  this.context.go('/settings');
                },
              ),
              ListTile(
                leading: const Icon(Icons.link_off_rounded),
                title: const Text('Disconnect'),
                onTap: () async {
                  Navigator.of(context).pop();
                  await ref.read(sessionControllerProvider.notifier).disconnect();
                  if (!mounted) {
                    return;
                  }
                  this.context.go('/');
                },
              ),
            ],
          ),
        );
      },
    );
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
    final selectedGameMissing =
        session.selectedGame != TruckGame.none &&
        !session.supportedGames.isRunning(session.selectedGame);

    return Scaffold(
      body: GestureDetector(
        behavior: HitTestBehavior.opaque,
        onTap: _showControls,
        child: Stack(
          fit: StackFit.expand,
          children: <Widget>[
            Container(
              decoration: const BoxDecoration(
                gradient: RadialGradient(
                  center: Alignment.topCenter,
                  radius: 1.2,
                  colors: <Color>[
                    Color(0xFF1B2632),
                    TruckNavPalette.background,
                  ],
                ),
              ),
            ),
            SafeArea(
              child: Padding(
                padding: const EdgeInsets.all(18),
                child: Column(
                  children: <Widget>[
                    if (selectedGameMissing) ...<Widget>[
                      _GameNotDetectedBanner(
                        game: session.selectedGame,
                        connectorName: session.selectedConnector?.name ?? 'ROCK',
                        onCheckAgain: controller.requestGameStateRefresh,
                        onChooseAnotherGame: () {
                          controller.returnToGameSelection();
                          context.go('/connector');
                        },
                      ),
                      const SizedBox(height: 14),
                    ],
                    const Expanded(child: GpsVideoSurfacePlaceholder()),
                    const SizedBox(height: 14),
                    if (session.lastCommandLabel case final command?)
                      Text(
                        'Last command: $command',
                        style: theme.textTheme.bodyMedium?.copyWith(
                          color: TruckNavPalette.textSecondary,
                        ),
                      ),
                  ],
                ),
              ),
            ),
            SafeArea(
              child: AnimatedSwitcher(
                duration: const Duration(milliseconds: 220),
                child: _controlsVisible
                    ? Column(
                        key: const ValueKey<String>('gps-controls'),
                        children: <Widget>[
                          Padding(
                            padding: const EdgeInsets.fromLTRB(18, 8, 18, 0),
                            child: Row(
                              children: <Widget>[
                                _OverlayPill(
                                  label: session.selectedGame == TruckGame.none
                                      ? gameLabel(session.runningGame)
                                      : gameLabel(session.selectedGame),
                                ),
                                const Spacer(),
                                IconButton.filledTonal(
                                  onPressed: () => _openQuickMenu(context),
                                  icon: const Icon(Icons.more_horiz_rounded),
                                ),
                              ],
                            ),
                          ),
                          const Spacer(),
                          Padding(
                            padding: const EdgeInsets.fromLTRB(18, 0, 18, 24),
                            child: Row(
                              children: <Widget>[
                                Expanded(
                                  child: _CommandButton(
                                    label: 'Zoom out',
                                    icon: Icons.remove,
                                    onPressed: () {
                                      controller.sendCommand(
                                        NavigationCommandKind.zoomOut,
                                      );
                                      _showControls();
                                    },
                                  ),
                                ),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: _CommandButton(
                                    label: 'Change navigation view',
                                    icon: Icons.sync_alt_rounded,
                                    onPressed: () {
                                      controller.sendCommand(
                                        NavigationCommandKind
                                            .cycleNavigationView,
                                      );
                                      _showControls();
                                    },
                                  ),
                                ),
                                const SizedBox(width: 12),
                                Expanded(
                                  child: _CommandButton(
                                    label: 'Zoom in',
                                    icon: Icons.add,
                                    onPressed: () {
                                      controller.sendCommand(
                                        NavigationCommandKind.zoomIn,
                                      );
                                      _showControls();
                                    },
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ],
                      )
                    : const SizedBox.shrink(),
              ),
            ),
            if (session.weakConnection)
              const Positioned(
                left: 18,
                right: 18,
                bottom: 112,
                child: _WeakConnectionBanner(),
              ),
            if (session.connectionStage == ConnectionStage.reconnecting)
              const _GpsReconnectOverlay(),
          ],
        ),
      ),
    );
  }
}

class _OverlayPill extends StatelessWidget {
  const _OverlayPill({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
      decoration: BoxDecoration(
        color: TruckNavPalette.surface.withValues(alpha: 0.9),
        borderRadius: BorderRadius.circular(999),
        border: Border.all(color: TruckNavPalette.border),
      ),
      child: Text(
        label,
        style: Theme.of(
          context,
        ).textTheme.bodyMedium?.copyWith(fontWeight: FontWeight.w700),
      ),
    );
  }
}

class _CommandButton extends StatelessWidget {
  const _CommandButton({
    required this.label,
    required this.icon,
    required this.onPressed,
  });

  final String label;
  final IconData icon;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return FilledButton.icon(
      onPressed: onPressed,
      style: FilledButton.styleFrom(
        minimumSize: const Size.fromHeight(58),
        backgroundColor: TruckNavPalette.surface.withValues(alpha: 0.92),
        foregroundColor: TruckNavPalette.textPrimary,
        side: const BorderSide(color: TruckNavPalette.border),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(22)),
      ),
      icon: Icon(icon),
      label: Text(label, textAlign: TextAlign.center),
    );
  }
}

class _WeakConnectionBanner extends StatelessWidget {
  const _WeakConnectionBanner();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: TruckNavPalette.surface.withValues(alpha: 0.95),
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: TruckNavPalette.border),
      ),
      child: Text(
        'Weak connection. TruckNav is still receiving pong responses, but the LAN session is degraded.',
        textAlign: TextAlign.center,
        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
              color: TruckNavPalette.textPrimary,
            ),
      ),
    );
  }
}

class _GameNotDetectedBanner extends StatelessWidget {
  const _GameNotDetectedBanner({
    required this.game,
    required this.connectorName,
    required this.onCheckAgain,
    required this.onChooseAnotherGame,
  });

  final TruckGame game;
  final String connectorName;
  final Future<void> Function() onCheckAgain;
  final VoidCallback onChooseAnotherGame;

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: TruckNavPalette.surface.withValues(alpha: 0.95),
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: TruckNavPalette.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Text(
            'Game not detected',
            style: Theme.of(context).textTheme.titleMedium?.copyWith(
              color: TruckNavPalette.accent,
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 8),
          Text(
            'Start ${gameLabel(game)} on $connectorName to continue.',
            style: Theme.of(context).textTheme.bodyMedium?.copyWith(
              color: TruckNavPalette.textSecondary,
              height: 1.45,
            ),
          ),
          const SizedBox(height: 14),
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

class _GpsReconnectOverlay extends StatelessWidget {
  const _GpsReconnectOverlay();

  @override
  Widget build(BuildContext context) {
    return ColoredBox(
      color: Colors.black54,
      child: Center(
        child: Container(
          margin: const EdgeInsets.symmetric(horizontal: 30),
          padding: const EdgeInsets.all(24),
          decoration: BoxDecoration(
            color: TruckNavPalette.surface,
            borderRadius: BorderRadius.circular(28),
            border: Border.all(color: TruckNavPalette.border),
          ),
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
                'TruckNav is waiting for the previous Connector to reappear on the LAN before restoring the control session.',
                textAlign: TextAlign.center,
              ),
            ],
          ),
        ),
      ),
    );
  }
}
