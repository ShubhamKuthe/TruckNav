import 'package:go_router/go_router.dart';

import '../features/connector/connector_screen.dart';
import '../features/discovery/discovery_screen.dart';
import '../features/gps/gps_screen.dart';
import '../features/settings/settings_screen.dart';

final GoRouter appRouter = GoRouter(
  routes: <RouteBase>[
    GoRoute(path: '/', builder: (context, state) => const DiscoveryScreen()),
    GoRoute(
      path: '/connector',
      builder: (context, state) => const ConnectorScreen(),
    ),
    GoRoute(path: '/gps', builder: (context, state) => const GpsScreen()),
    GoRoute(
      path: '/settings',
      builder: (context, state) => const SettingsScreen(),
    ),
  ],
);
