import 'package:flutter/material.dart';

import '../../domain/legal_content.dart';
import 'legal_document_screen.dart';

class TermsOfServiceScreen extends StatelessWidget {
  const TermsOfServiceScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return const LegalDocumentScreen(
      title: 'Terms of Service',
      lastUpdatedNote: LegalContent.lastUpdated,
      sections: LegalContent.termsOfService,
    );
  }
}
