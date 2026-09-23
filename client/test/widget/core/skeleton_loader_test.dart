import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:purch_client/core/widgets/skeleton_loader.dart';

void main() {
  testWidgets('SkeletonBox renders and pulses without error', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: SkeletonBox(width: 100, height: 20),
        ),
      ),
    );

    expect(find.byType(SkeletonBox), findsOneWidget);
    await tester.pump(const Duration(milliseconds: 500));
    expect(find.byType(SkeletonBox), findsOneWidget);
  });

  testWidgets('CatalogGridSkeleton renders card placeholders', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: CatalogGridSkeleton(itemCount: 4),
        ),
      ),
    );

    expect(find.byType(CatalogGridSkeleton), findsOneWidget);
    expect(find.byType(GridView), findsOneWidget);
  });

  testWidgets('ItemListSkeleton renders row placeholders', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: ItemListSkeleton(itemCount: 3),
        ),
      ),
    );

    expect(find.byType(ItemListSkeleton), findsOneWidget);
    expect(find.byType(ListView), findsOneWidget);
  });

  testWidgets('ReportTableSkeleton renders table placeholders', (tester) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(
          body: ReportTableSkeleton(rowCount: 3, columnCount: 4),
        ),
      ),
    );

    expect(find.byType(ReportTableSkeleton), findsOneWidget);
  });
}
