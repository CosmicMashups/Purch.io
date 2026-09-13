import 'package:flutter/material.dart';

import '../../domain/legal_content.dart';
import 'legal_document_screen.dart';

class PrivacyPolicyScreen extends StatelessWidget {
  const PrivacyPolicyScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const LegalDocumentScreen(
      title: 'Privacy Policy',
      lastUpdatedNote: LegalContent.lastUpdated,
      sections: LegalContent.privacyPolicy,
    );
  }
}
