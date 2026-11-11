import 'dart:math';

import 'package:flutter/material.dart';

import '../protocol.dart';
import '../state/app_state.dart';
import '../theme.dart';
import 'icons.dart';
import 'widgets.dart';

class ControlsScreen extends StatelessWidget {
  const ControlsScreen({super.key, required this.state});
  final AppState state;

  Future<void> _forget(BuildContext context) async {
    final ok = await showDialog<bool>(
      context: context,
      builder:
          (c) => AlertDialog(
            title: const Text('Forget this PC?'),
            content: const Text(
              'This phone will stop controlling it. To fully remove access, also revoke the phone in Taveloom on your PC (Devices).',
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(c, false),
                child: const Text('Cancel'),
              ),
              FilledButton(
                style: FilledButton.styleFrom(minimumSize: const Size(96, 44)),
                onPressed: () => Navigator.pop(c, true),
                child: const Text('Forget'),
              ),
            ],
          ),
    );
    if (ok == true) await state.unpair();
  }

  @override
  Widget build(BuildContext context) {
    final t = TavColors.of(context);
    final ws = state.workspace;
    final active = ws?.active;

    if (state.toast case final msg?) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!context.mounted) return;
        ScaffoldMessenger.of(context)
          ..clearSnackBars()
          ..showSnackBar(
            SnackBar(content: Text(msg), duration: const Duration(seconds: 3)),
          );
      });
    }

    return Scaffold(
      appBar: AppBar(
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              state.host?.hostName ?? 'Taveloom',
              style: const TextStyle(fontSize: 18, fontWeight: FontWeight.w600),
            ),
            ConnectionDot(link: state.link),
          ],
        ),
        actions: [
          PopupMenuButton<String>(
            onSelected: (v) {
              if (v == 'forget') _forget(context);
              if (v == 'retry') state.retryNow();
            },
            itemBuilder:
                (_) => const [
                  PopupMenuItem(value: 'retry', child: Text('Reconnect now')),
                  PopupMenuItem(value: 'forget', child: Text('Forget this PC')),
                ],
          ),
        ],
      ),
      body: SafeArea(
        child: Column(
          children: [
            ConnectionBanner(state: state, onPairAgain: () => state.unpair()),
            if (ws != null) _WorkspaceBar(state: state, ws: ws),
            Expanded(
              child:
                  active == null
                      ? Center(
                        child: Padding(
                          padding: const EdgeInsets.all(32),
                          child: Text(
                            state.connected
                                ? 'No workspace is selected on your PC yet.'
                                : 'Waiting for your PC…',
                            style: TextStyle(color: t.muted),
                          ),
                        ),
                      )
                      : _Grid(state: state, ws: active),
            ),
            if (ws != null && ws.reason.isNotEmpty)
              Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: Text(
                  '${ws.mode == 'auto' ? 'Automatic' : 'Manual'} · ${ws.reason}',
                  style: TextStyle(fontSize: 12, color: t.muted),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _WorkspaceBar extends StatelessWidget {
  const _WorkspaceBar({required this.state, required this.ws});
  final AppState state;
  final WorkspaceState ws;

  @override
  Widget build(BuildContext context) {
    final auto = ws.mode == 'auto';
    return SizedBox(
      height: 52,
      child: Row(
        children: [
          Expanded(
            child: ListView.separated(
              padding: const EdgeInsets.symmetric(horizontal: 16),
              scrollDirection: Axis.horizontal,
              itemCount: ws.workspaces.length,
              separatorBuilder: (_, _) => const SizedBox(width: 8),
              itemBuilder: (_, i) {
                final w = ws.workspaces[i];
                return ChoiceChip(
                  avatar: Icon(iconFor(w.icon), size: 18),
                  label: Text(w.name),
                  selected: w.id == ws.active?.id,
                  onSelected:
                      state.connected
                          ? (_) => state.selectWorkspace(w.id)
                          : null,
                );
              },
            ),
          ),
          Tooltip(
            message:
                auto
                    ? 'Automatic switching is on'
                    : 'Tap to switch workspaces automatically again',
            child: IconButton(
              onPressed: state.connected ? () => state.setAuto(!auto) : null,
              icon: Icon(
                auto ? Icons.auto_awesome : Icons.auto_awesome_outlined,
              ),
              color: auto ? Theme.of(context).colorScheme.primary : null,
            ),
          ),
        ],
      ),
    );
  }
}

/// Lays tiles out from the host's column/row/span values using the space available, so it adapts to any phone or tablet.
class _Grid extends StatelessWidget {
  const _Grid({required this.state, required this.ws});
  final AppState state;
  final ClientWorkspace ws;

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, box) {
        const gap = 10.0, pad = 16.0;
        final cols = max(1, ws.columns), rows = max(1, ws.rows);
        final byWidth = (box.maxWidth - pad * 2 - gap * (cols - 1)) / cols;
        final cell = min(
          byWidth,
          150.0,
        ); // cap so tablets get a tidy centred grid, not giant buttons
        final width = cell * cols + gap * (cols - 1);
        final height = cell * rows + gap * (rows - 1);
        return SingleChildScrollView(
          padding: const EdgeInsets.all(pad),
          child: Center(
            child: AnimatedSwitcher(
              duration: const Duration(milliseconds: 220),
              child: SizedBox(
                key: ValueKey(ws.id),
                width: width,
                height: height,
                child: Stack(
                  children: [
                    for (final c in ws.controls)
                      Positioned(
                        left: c.col * (cell + gap),
                        top: c.row * (cell + gap),
                        width: cell * c.colSpan + gap * (c.colSpan - 1),
                        height: cell * c.rowSpan + gap * (c.rowSpan - 1),
                        child: ControlTile(control: c, state: state),
                      ),
                  ],
                ),
              ),
            ),
          ),
        );
      },
    );
  }
}
