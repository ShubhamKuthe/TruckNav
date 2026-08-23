import 'package:flutter/material.dart';

import '../../core/theme/trucknav_theme.dart';

class GpsVideoSurfacePlaceholder extends StatelessWidget {
  const GpsVideoSurfacePlaceholder({super.key});

  @override
  Widget build(BuildContext context) {
    final mediaQuery = MediaQuery.of(context);
    final isTablet = mediaQuery.size.shortestSide >= 700;
    final isLandscape = mediaQuery.orientation == Orientation.landscape;

    final modeLabel = isTablet
        ? 'Tablet GPS placeholder'
        : isLandscape
        ? 'Landscape GPS placeholder'
        : 'Portrait GPS placeholder';

    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(32),
        border: Border.all(color: TruckNavPalette.border),
        gradient: const LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: <Color>[
            Color(0xFF11161C),
            Color(0xFF1A222C),
          ],
        ),
      ),
      child: Center(
        child: Padding(
          padding: const EdgeInsets.all(28),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: <Widget>[
              Container(
                width: 82,
                height: 82,
                decoration: BoxDecoration(
                  color: TruckNavPalette.accent.withValues(alpha: 0.16),
                  borderRadius: BorderRadius.circular(28),
                ),
                child: const Icon(
                  Icons.videocam_outlined,
                  size: 42,
                  color: TruckNavPalette.accent,
                ),
              ),
              const SizedBox(height: 18),
              Text(
                modeLabel,
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                      fontWeight: FontWeight.w800,
                    ),
              ),
              const SizedBox(height: 10),
              Text(
                'GpsVideoSurfacePlaceholder reserves this area for the future ETS2/ATS capture feed from the Windows Connector.',
                textAlign: TextAlign.center,
                style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                      color: TruckNavPalette.textSecondary,
                      height: 1.5,
                    ),
              ),
              const SizedBox(height: 16),
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 14,
                  vertical: 10,
                ),
                decoration: BoxDecoration(
                  color: TruckNavPalette.surface.withValues(alpha: 0.92),
                  borderRadius: BorderRadius.circular(18),
                  border: Border.all(color: TruckNavPalette.border),
                ),
                child: Text(
                  'Development placeholder only. No fake navigation map is rendered.',
                  textAlign: TextAlign.center,
                  style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: TruckNavPalette.textSecondary,
                        height: 1.4,
                      ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
