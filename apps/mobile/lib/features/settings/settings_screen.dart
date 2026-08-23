import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/theme/trucknav_theme.dart';
import '../../widgets/panel_card.dart';
import '../session/session_controller.dart';

class SettingsScreen extends ConsumerWidget {
  const SettingsScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final session = ref.watch(sessionControllerProvider);
    final controller = ref.read(sessionControllerProvider.notifier);
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        leading: IconButton(
          onPressed: () => context.go('/connector'),
          icon: const Icon(Icons.arrow_back_ios_new),
        ),
        title: const Text('Settings'),
      ),
      body: Container(
        decoration: const BoxDecoration(
          gradient: LinearGradient(
            begin: Alignment.topCenter,
            end: Alignment.bottomCenter,
            colors: <Color>[Color(0xFF11161C), TruckNavPalette.background],
          ),
        ),
        child: SafeArea(
          top: false,
          child: ListView(
            padding: const EdgeInsets.fromLTRB(24, 8, 24, 32),
            children: <Widget>[
              Text(
                'Connection behavior',
                style: theme.textTheme.headlineMedium?.copyWith(
                  fontWeight: FontWeight.w800,
                ),
              ),
              const SizedBox(height: 8),
              Text(
                'These settings now persist locally and shape how TruckNav handles reconnect plus game-selection flow.',
                style: theme.textTheme.bodyMedium?.copyWith(
                  color: TruckNavPalette.textSecondary,
                  height: 1.5,
                ),
              ),
              const SizedBox(height: 24),
              PanelCard(
                child: Column(
                  children: <Widget>[
                    SwitchListTile(
                      value: session.autoReconnect,
                      title: const Text('Auto reconnect'),
                      subtitle: const Text(
                        'Recover from temporary Wi-Fi loss without restarting the apps.',
                      ),
                      onChanged: controller.toggleAutoReconnect,
                    ),
                    const Divider(height: 1),
                    SwitchListTile(
                      value: session.autoOpenRunningGame,
                      title: const Text('Automatically open running game'),
                      subtitle: const Text(
                        'Skip game selection when exactly one supported game is running.',
                      ),
                      onChanged: controller.toggleAutoOpenRunningGame,
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
                      'Current device',
                      style: theme.textTheme.titleMedium?.copyWith(
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                    const SizedBox(height: 12),
                    Text(
                      '${session.deviceName} keeps a persistent TruckNav UUID. Manual IP entry is still intentionally absent from the product flow.',
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: TruckNavPalette.textSecondary,
                        height: 1.5,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
