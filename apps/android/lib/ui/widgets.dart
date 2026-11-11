import 'package:flutter/material.dart';

import '../protocol.dart';
import '../services/host_client.dart';
import '../state/app_state.dart';
import '../theme.dart';
import 'icons.dart';

/// One grid cell for any widget kind. Layout is decided by the host (column/row/span); this only draws.
class ControlTile extends StatelessWidget {
  const ControlTile({super.key, required this.control, required this.state});

  final ClientControl control;
  final AppState state;

  @override
  Widget build(BuildContext context) {
    final t = TavColors.of(context);
    final scheme = Theme.of(context).colorScheme;
    final status = state.statusOf(control.id);
    final online = state.connected;
    final usable = control.available && online;
    final tel = state.telemetry;

    final bool on = control.widget == WidgetKind.toggle && tel?.muted == true;
    Color border = t.border;
    Color fill = t.surface;
    if (on) {
      fill = scheme.primary.withAlpha(46);
      border = scheme.primary;
    }
    if (status.phase == ControlPhase.success) border = t.success;
    if (status.phase == ControlPhase.error) border = t.danger;

    final Widget body = switch (control.widget) {
      WidgetKind.value => _valueBody(context, t, tel),
      WidgetKind.volume => _VolumeBody(
        control: control,
        state: state,
        enabled: usable,
      ),
      _ => _buttonBody(context, t, scheme, status, on),
    };

    return Semantics(
      button:
          control.widget != WidgetKind.value &&
          control.widget != WidgetKind.volume,
      enabled: usable,
      label: control.label,
      child: AnimatedContainer(
        duration: const Duration(milliseconds: 160),
        decoration: BoxDecoration(
          color: fill,
          borderRadius: BorderRadius.circular(18),
          border: Border.all(
            color: border,
            width: on || status.phase != ControlPhase.idle ? 1.6 : 1,
          ),
        ),
        child: Opacity(
          opacity: usable || control.widget == WidgetKind.value ? 1 : 0.45,
          child: Material(
            color: Colors.transparent,
            child: InkWell(
              borderRadius: BorderRadius.circular(18),
              onTap:
                  control.widget == WidgetKind.value ||
                          control.widget == WidgetKind.volume
                      ? null
                      : () => state.invoke(control),
              child: Padding(
                padding: const EdgeInsets.all(8),
                child: Center(child: body),
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buttonBody(
    BuildContext context,
    TavColors t,
    ColorScheme scheme,
    ControlStatus status,
    bool on,
  ) {
    final Widget icon = switch (status.phase) {
      ControlPhase.loading => const SizedBox(
        width: 22,
        height: 22,
        child: CircularProgressIndicator(strokeWidth: 2.4),
      ),
      ControlPhase.success => Icon(
        Icons.check_rounded,
        size: 28,
        color: t.success,
      ),
      ControlPhase.error => Icon(
        Icons.error_outline,
        size: 28,
        color: t.danger,
      ),
      ControlPhase.idle => Icon(
        iconFor(control.icon),
        size: 28,
        color: on ? scheme.primary : scheme.onSurface,
      ),
    };
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        SizedBox(height: 30, child: Center(child: icon)),
        const SizedBox(height: 6),
        Text(
          control.label,
          maxLines: 2,
          textAlign: TextAlign.center,
          overflow: TextOverflow.ellipsis,
          style: const TextStyle(fontSize: 12, fontWeight: FontWeight.w500),
        ),
        if (!control.available)
          Padding(
            padding: const EdgeInsets.only(top: 2),
            child: Text(
              'Not set up',
              style: TextStyle(fontSize: 10, color: t.warning),
            ),
          ),
      ],
    );
  }

  Widget _valueBody(BuildContext context, TavColors t, Telemetry? tel) {
    String text = '—';
    final v = switch (control.source) {
      'cpu' => tel?.cpu,
      'ram' => tel?.ram,
      'volume' => tel?.volume,
      _ => null,
    };
    if (v != null) {
      text = '${v.round()}%';
    } else if (control.source == 'mic' && tel?.micMuted != null) {
      text = tel!.micMuted! ? 'Muted' : 'Live';
    }
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        Text(
          text,
          style: const TextStyle(fontSize: 22, fontWeight: FontWeight.w600),
        ),
        const SizedBox(height: 4),
        Text(
          control.label,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(fontSize: 12, color: t.muted),
        ),
        if (text == '—')
          Text(
            state.connected ? 'unavailable' : 'offline',
            style: TextStyle(fontSize: 10, color: t.muted),
          ),
      ],
    );
  }
}

class _VolumeBody extends StatefulWidget {
  const _VolumeBody({
    required this.control,
    required this.state,
    required this.enabled,
  });
  final ClientControl control;
  final AppState state;
  final bool enabled;

  @override
  State<_VolumeBody> createState() => _VolumeBodyState();
}

class _VolumeBodyState extends State<_VolumeBody> {
  double? _dragging;

  @override
  Widget build(BuildContext context) {
    final t = TavColors.of(context);
    final host = widget.state.telemetry?.volume;
    final value = (_dragging ?? host ?? 0).clamp(0, 100).toDouble();
    return Row(
      children: [
        Icon(
          iconFor(widget.control.icon),
          color: Theme.of(context).colorScheme.onSurface,
        ),
        Expanded(
          child: Slider(
            value: value,
            min: 0,
            max: 100,
            // A slider with no host value yet is disabled instead of showing an invented position.
            onChanged:
                widget.enabled && host != null
                    ? (v) => setState(() => _dragging = v)
                    : null,
            onChangeEnd: (v) {
              widget.state.invoke(widget.control, value: v.roundToDouble());
              setState(() => _dragging = null);
            },
          ),
        ),
        SizedBox(
          width: 42,
          child: Text(
            host == null ? '—' : '${value.round()}%',
            textAlign: TextAlign.end,
            style: TextStyle(color: t.muted, fontWeight: FontWeight.w600),
          ),
        ),
      ],
    );
  }
}

class ConnectionBanner extends StatelessWidget {
  const ConnectionBanner({
    super.key,
    required this.state,
    required this.onPairAgain,
  });
  final AppState state;
  final VoidCallback onPairAgain;

  @override
  Widget build(BuildContext context) {
    final t = TavColors.of(context);
    final (
      String title,
      String? detail,
      Color color,
      bool action,
    ) = switch (state.link) {
      LinkState.connected => ('', null, t.success, false),
      LinkState.connecting => ('Connecting…', null, t.warning, false),
      LinkState.reconnecting => (
        'Reconnecting to ${state.host?.hostName ?? 'your PC'}…',
        state.issue,
        t.warning,
        false,
      ),
      LinkState.revoked => ('Access removed', state.issue, t.danger, true),
      LinkState.authFailed => (
        'Not recognised by the PC',
        state.issue,
        t.danger,
        true,
      ),
      LinkState.untrusted => (
        'Security check failed',
        state.issue,
        t.danger,
        true,
      ),
      LinkState.disconnected => ('Disconnected', null, t.muted, false),
    };
    if (state.link == LinkState.connected) return const SizedBox.shrink();
    return Container(
      margin: const EdgeInsets.fromLTRB(16, 4, 16, 8),
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: color.withAlpha(30),
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: color.withAlpha(120)),
      ),
      child: Row(
        children: [
          if (!action)
            SizedBox(
              width: 16,
              height: 16,
              child: CircularProgressIndicator(strokeWidth: 2, color: color),
            )
          else
            Icon(Icons.link_off, color: color, size: 20),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: const TextStyle(fontWeight: FontWeight.w600),
                ),
                if (detail != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 2),
                    child: Text(
                      detail,
                      style: TextStyle(fontSize: 12, color: t.muted),
                    ),
                  ),
              ],
            ),
          ),
          if (action)
            TextButton(onPressed: onPairAgain, child: const Text('Pair again')),
        ],
      ),
    );
  }
}

class ConnectionDot extends StatelessWidget {
  const ConnectionDot({super.key, required this.link});
  final LinkState link;

  @override
  Widget build(BuildContext context) {
    final t = TavColors.of(context);
    final (Color c, String label) = switch (link) {
      LinkState.connected => (t.success, 'Connected'),
      LinkState.connecting ||
      LinkState.reconnecting => (t.warning, 'Connecting'),
      _ => (t.danger, 'Offline'),
    };
    return Semantics(
      label: 'Connection: $label',
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            width: 9,
            height: 9,
            decoration: BoxDecoration(color: c, shape: BoxShape.circle),
          ),
          const SizedBox(width: 6),
          Text(label, style: TextStyle(fontSize: 12, color: t.muted)),
        ],
      ),
    );
  }
}
