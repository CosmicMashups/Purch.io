/// A single named block of a legal document (Privacy Policy / Terms of
/// Service), rendered as a heading followed by body text.
class LegalSection {
  const LegalSection({required this.heading, required this.body});

  final String heading;
  final String body;
}
