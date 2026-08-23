import 'package:flutter/material.dart';

import '../core/theme/trucknav_theme.dart';

class PanelCard extends StatelessWidget {
  const PanelCard({
    super.key,
    this.child,
    this.padding = const EdgeInsets.all(18),
    this.onTap,
  });

  final Widget? child;
  final EdgeInsetsGeometry padding;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    if (onTap == null) {
      return Card(
        margin: EdgeInsets.zero,
        clipBehavior: Clip.antiAlias,
        child: Padding(padding: padding, child: child),
      );
    }

    return Material(
      color: TruckNavPalette.surface,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(28),
        side: const BorderSide(color: TruckNavPalette.border),
      ),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(28),
        child: Padding(padding: padding, child: child),
      ),
    );
  }
}
