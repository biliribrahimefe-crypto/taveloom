import 'package:flutter/material.dart';

import 'state/app_state.dart';
import 'theme.dart';
import 'ui/controls_screen.dart';
import 'ui/onboarding.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  runApp(const TaveloomApp());
}

class TaveloomApp extends StatefulWidget {
  const TaveloomApp({super.key, this.state});
  final AppState? state;

  @override
  State<TaveloomApp> createState() => _TaveloomAppState();
}

class _TaveloomAppState extends State<TaveloomApp> {
  late final AppState _state = widget.state ?? AppState();

  @override
  void initState() {
    super.initState();
    _state.init();
  }

  @override
  void dispose() {
    _state.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Taveloom',
      debugShowCheckedModeBanner: false,
      theme: buildTheme(Brightness.light),
      darkTheme: buildTheme(Brightness.dark),
      themeMode: ThemeMode.system,
      home: ListenableBuilder(
        listenable: _state,
        builder:
            (context, _) => switch (_state.phase) {
              Phase.loading => const Scaffold(
                body: Center(child: CircularProgressIndicator()),
              ),
              Phase.unpaired => OnboardingScreen(state: _state),
              Phase.paired => ControlsScreen(state: _state),
            },
      ),
    );
  }
}
