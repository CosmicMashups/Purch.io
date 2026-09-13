import 'package:flutter/material.dart';

/// First-launch brand moment shown by `_StartupGate` in `main.dart` while the
/// stored-session check resolves. Background is sampled from
/// `assets/logo.jpg`'s own ground so the mark reads as native to the frame
/// instead of pasted onto an unrelated color.
class SplashScreen extends StatefulWidget {
  const SplashScreen({super.key});

  static const Color background = Color(0xFF0E6B5A);

  @override
  State<SplashScreen> createState() => _SplashScreenState();
}

class _SplashScreenState extends State<SplashScreen>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller;
  late final Animation<double> _fade;
  late final Animation<double> _scale;

  @override
  void initState() {
    super.initState();
    _controller = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 700),
    );
    _fade = CurvedAnimation(parent: _controller, curve: Curves.easeOut);
    _scale = Tween<double>(begin: 0.92, end: 1.0).animate(
      CurvedAnimation(parent: _controller, curve: Curves.easeOutCubic),
    );
    _controller.forward();
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: SplashScreen.background,
      body: Center(
        child: FadeTransition(
          opacity: _fade,
          child: ScaleTransition(
            scale: _scale,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                ClipRRect(
                  borderRadius: BorderRadius.circular(28),
                  child: Image.asset(
                    'assets/logo.jpg',
                    width: 112,
                    height: 112,
                    semanticLabel: 'Purch.io',
                  ),
                ),
                const SizedBox(height: 24),
                Image.asset(
                  'assets/wordmark.png',
                  height: 40,
                  color: Colors.white,
                  colorBlendMode: BlendMode.srcIn,
                  semanticLabel: 'Purch.io',
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
