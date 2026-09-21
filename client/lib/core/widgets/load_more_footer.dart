import 'package:flutter/material.dart';

import '../theming/app_tokens.dart';

/// The last row of a paged list: a "Load older" button that shows a spinner while [onLoadMore] runs.
class LoadMoreFooter extends StatefulWidget {
  const LoadMoreFooter({super.key, required this.onLoadMore});

  final Future<void> Function() onLoadMore;

  @override
  State<LoadMoreFooter> createState() => _LoadMoreFooterState();
}

class _LoadMoreFooterState extends State<LoadMoreFooter> {
  bool _loading = false;

  Future<void> _load() async {
    setState(() => _loading = true);
    try {
      await widget.onLoadMore();
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: AppSpacing.sm),
      child: Center(
        child: _loading
            ? const SizedBox(
                width: 24,
                height: 24,
                child: CircularProgressIndicator(strokeWidth: 2),
              )
            : OutlinedButton(onPressed: _load, child: const Text('Load older')),
      ),
    );
  }
}
