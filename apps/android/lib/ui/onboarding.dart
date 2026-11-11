import 'dart:async';

import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import '../services/host_client.dart';
import '../state/app_state.dart';
import '../theme.dart';

class OnboardingScreen extends StatefulWidget {
  const OnboardingScreen({super.key, required this.state});
  final AppState state;

  @override
  State<OnboardingScreen> createState() => _OnboardingScreenState();
}

class _OnboardingScreenState extends State<OnboardingScreen> {
  final _controller = PageController();
  final _name = TextEditingController(text: 'My Android');
  int _page = 0;

  static const _pages = [
    (
      Icons.dashboard_customize_outlined,
      'Welcome to Taveloom',
      'Turn this phone into a control surface for your Windows PC. Your workspace, at your fingertips.',
    ),
    (
      Icons.auto_awesome_outlined,
      'It adapts to what you do',
      'When you switch to your editor, your browser or a media player, your controls change with it. Or pick a workspace yourself.',
    ),
    (
      Icons.qr_code_scanner,
      'Pair in a few seconds',
      'On your PC open Taveloom, choose Devices > Pair a phone, and scan the code. Your PC asks you to approve this phone. Nothing leaves your network.',
    ),
  ];

  Future<void> _scan() async {
    final code = await Navigator.of(
      context,
    ).push<String>(MaterialPageRoute(builder: (_) => const ScanScreen()));
    if (code == null || !mounted) return;
    await Navigator.of(context).push(
      MaterialPageRoute(
        builder:
            (_) => PairingScreen(
              state: widget.state,
              code: code,
              deviceName: _name.text,
            ),
      ),
    );
  }

  @override
  void dispose() {
    _controller.dispose();
    _name.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = TavColors.of(context);
    final last = _page == _pages.length - 1;
    return Scaffold(
      body: SafeArea(
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 520),
            child: Column(
              children: [
                Expanded(
                  child: PageView.builder(
                    controller: _controller,
                    itemCount: _pages.length,
                    onPageChanged: (i) => setState(() => _page = i),
                    itemBuilder: (_, i) {
                      final (icon, title, body) = _pages[i];
                      return Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 32),
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Container(
                              width: 96,
                              height: 96,
                              decoration: BoxDecoration(
                                color: Theme.of(
                                  context,
                                ).colorScheme.primary.withAlpha(36),
                                borderRadius: BorderRadius.circular(28),
                              ),
                              child: Icon(
                                icon,
                                size: 44,
                                color: Theme.of(context).colorScheme.primary,
                              ),
                            ),
                            const SizedBox(height: 32),
                            Text(
                              title,
                              textAlign: TextAlign.center,
                              style: Theme.of(context).textTheme.headlineSmall
                                  ?.copyWith(fontWeight: FontWeight.w600),
                            ),
                            const SizedBox(height: 12),
                            Text(
                              body,
                              textAlign: TextAlign.center,
                              style: TextStyle(
                                fontSize: 16,
                                height: 1.45,
                                color: t.muted,
                              ),
                            ),
                          ],
                        ),
                      );
                    },
                  ),
                ),
                Row(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    for (var i = 0; i < _pages.length; i++)
                      AnimatedContainer(
                        duration: const Duration(milliseconds: 200),
                        margin: const EdgeInsets.all(4),
                        width: i == _page ? 22 : 8,
                        height: 8,
                        decoration: BoxDecoration(
                          color:
                              i == _page
                                  ? Theme.of(context).colorScheme.primary
                                  : t.border,
                          borderRadius: BorderRadius.circular(4),
                        ),
                      ),
                  ],
                ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(24, 20, 24, 24),
                  child:
                      last
                          ? Column(
                            children: [
                              TextField(
                                controller: _name,
                                maxLength: 40,
                                decoration: const InputDecoration(
                                  labelText: 'Name this device',
                                  counterText: '',
                                ),
                              ),
                              const SizedBox(height: 12),
                              FilledButton.icon(
                                onPressed: _scan,
                                icon: const Icon(Icons.qr_code_scanner),
                                label: const Text('Scan pairing code'),
                              ),
                            ],
                          )
                          : FilledButton(
                            onPressed:
                                () => _controller.nextPage(
                                  duration: const Duration(milliseconds: 260),
                                  curve: Curves.easeOut,
                                ),
                            child: const Text('Continue'),
                          ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class ScanScreen extends StatefulWidget {
  const ScanScreen({super.key});

  @override
  State<ScanScreen> createState() => _ScanScreenState();
}

class _ScanScreenState extends State<ScanScreen> {
  final _controller = MobileScannerController(
    formats: const [BarcodeFormat.qrCode],
    detectionSpeed: DetectionSpeed.noDuplicates,
  );
  bool _done = false;

  @override
  void dispose() {
    unawaited(_controller.dispose());
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = TavColors.of(context);
    return Scaffold(
      appBar: AppBar(title: const Text('Scan pairing code')),
      body: Column(
        children: [
          Expanded(
            child: ClipRRect(
              borderRadius: BorderRadius.circular(20),
              child: MobileScanner(
                controller: _controller,
                onDetect: (capture) {
                  if (_done) return;
                  for (final b in capture.barcodes) {
                    final v = b.rawValue;
                    if (v != null && v.startsWith('taveloom://')) {
                      _done = true;
                      Navigator.of(context).pop(v);
                      return;
                    }
                  }
                },
                errorBuilder:
                    (context, error, child) => Center(
                      child: Padding(
                        padding: const EdgeInsets.all(24),
                        child: Text(
                          "The camera could not start (${error.errorCode.name}). ${error.errorDetails?.message ?? ''}\n\nIf this says permissionDenied, allow Camera for Taveloom in Android settings.",
                          textAlign: TextAlign.center,
                          style: TextStyle(color: t.muted),
                        ),
                      ),
                    ),
              ),
            ),
          ),
          Padding(
            padding: const EdgeInsets.all(24),
            child: Text(
              'On your PC: Taveloom > Devices > Pair a phone',
              textAlign: TextAlign.center,
              style: TextStyle(color: t.muted),
            ),
          ),
        ],
      ),
    );
  }
}

class PairingScreen extends StatefulWidget {
  const PairingScreen({
    super.key,
    required this.state,
    required this.code,
    required this.deviceName,
  });
  final AppState state;
  final String code, deviceName;

  @override
  State<PairingScreen> createState() => _PairingScreenState();
}

class _PairingScreenState extends State<PairingScreen> {
  bool _success = false;

  @override
  void initState() {
    super.initState();
    widget.state.addListener(_rebuild);
    unawaited(_run());
  }

  Future<void> _run() async {
    final ok = await widget.state.pair(widget.code, widget.deviceName);
    if (!mounted || !ok) return;
    setState(() => _success = true);
    await Future<void>.delayed(const Duration(milliseconds: 1200));
    if (mounted) Navigator.of(context).pop();
  }

  void _rebuild() => setState(() {});

  @override
  void dispose() {
    widget.state.removeListener(_rebuild);
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = TavColors.of(context);
    final p = widget.state.pairProgress;
    final scheme = Theme.of(context).colorScheme;
    final (IconData? icon, String title, String detail) =
        _success
            ? (
              Icons.check_circle_outline,
              'Connected',
              'Your PC is paired. Loading your controls…',
            )
            : p.error != null
            ? (Icons.error_outline, 'Pairing did not finish', p.error!.message)
            : p.stage == PairStage.awaitingApproval
            ? (
              null,
              'Approve on your PC',
              'A window on your Windows PC is asking to allow this phone. Click Allow to continue.',
            )
            : (
              null,
              'Connecting securely…',
              'Verifying your PC and exchanging keys.',
            );
    return PopScope(
      canPop: p.error != null,
      child: Scaffold(
        body: SafeArea(
          child: Center(
            child: Padding(
              padding: const EdgeInsets.all(32),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  if (icon == null)
                    const SizedBox(
                      width: 56,
                      height: 56,
                      child: CircularProgressIndicator(strokeWidth: 3.5),
                    )
                  else
                    Icon(
                      icon,
                      size: 64,
                      color: _success ? t.success : t.danger,
                    ),
                  const SizedBox(height: 28),
                  Text(
                    title,
                    textAlign: TextAlign.center,
                    style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                  const SizedBox(height: 12),
                  Text(
                    detail,
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      fontSize: 15,
                      height: 1.45,
                      color: t.muted,
                    ),
                  ),
                  if (p.error != null) ...[
                    const SizedBox(height: 32),
                    FilledButton(
                      onPressed: () {
                        widget.state.resetPairProgress();
                        Navigator.of(context).pop();
                      },
                      child: const Text('Try again'),
                    ),
                  ],
                  if (p.error == null && !_success) ...[
                    const SizedBox(height: 24),
                    Text(
                      'Pairing as "${widget.deviceName.trim().isEmpty ? 'My Android' : widget.deviceName.trim()}"',
                      style: TextStyle(fontSize: 12, color: scheme.outline),
                    ),
                  ],
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
