/// Mirrors Purch.Application.Catalog.CategoryDto.
class Category {
  const Category({
    required this.id,
    required this.name,
    required this.sortOrder,
  });

  factory Category.fromJson(Map<String, dynamic> json) {
    return Category(
      id: json['id'] as String,
      name: json['name'] as String,
      sortOrder: json['sortOrder'] as int,
    );
  }

  final String id;
  final String name;
  final int sortOrder;
}

/// Mirrors Purch.Application.Catalog.CreateCategoryRequest.
class CreateCategoryRequest {
  const CreateCategoryRequest({required this.name, required this.sortOrder});

  final String name;
  final int sortOrder;

  Map<String, dynamic> toJson() => {'name': name, 'sortOrder': sortOrder};
}
