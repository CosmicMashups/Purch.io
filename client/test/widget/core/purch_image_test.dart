import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/widgets/purch_image.dart';

void main() {
  test('PurchImage.resolveUrl correctly prepends apiBaseUrl for relative paths', () {
    final resolved = PurchImage.resolveUrl('/uploads/tenant-1/pic.jpg');
    expect(resolved, contains('/uploads/tenant-1/pic.jpg'));
    expect(resolved.startsWith('http'), isTrue);

    // Absolute URLs and assets remain untouched
    expect(
      PurchImage.resolveUrl('https://example.com/image.png'),
      'https://example.com/image.png',
    );
    expect(
      PurchImage.resolveUrl('assets/images/sample_qr_ph.jpg'),
      'assets/images/sample_qr_ph.jpg',
    );
  });

  testWidgets('PurchImage renders local asset without error', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: PurchImage(
            imageUrlOrPath: 'assets/images/kiosk_poster_default.jpg',
            width: 200,
            height: 100,
          ),
        ),
      ),
    );

    expect(find.byType(PurchImage), findsOneWidget);
    expect(find.byType(Image), findsOneWidget);
  });

  testWidgets('PurchImage renders fallback asset when imageUrlOrPath is null', (
    tester,
  ) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: PurchImage(
            imageUrlOrPath: null,
            fallbackAsset: 'assets/images/kiosk_poster_default.jpg',
            width: 200,
            height: 100,
          ),
        ),
      ),
    );

    expect(find.byType(PurchImage), findsOneWidget);
    expect(find.byType(Image), findsOneWidget);
  });

  testWidgets('PurchImage renders network image safely without hanging under test mode', (
    tester,
  ) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: PurchImage(
            imageUrlOrPath: 'https://example.com/item.png',
            width: 100,
            height: 100,
          ),
        ),
      ),
    );

    expect(find.byType(PurchImage), findsOneWidget);
    expect(find.byType(Image), findsOneWidget);
  });
}
