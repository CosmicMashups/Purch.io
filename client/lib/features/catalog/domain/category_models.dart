/// Mirrors Purch.Application.Catalog.CategoryDto.
class Category {
  const Category({
    required this.id,
    required this.name,
    required this.sortOrder,
    this.imageUrl,
  });

  factory Category.fromJson(Map<String, dynamic> json) {
    return Category(
      id: json['id'] as String,
      name: json['name'] as String,
      sortOrder: json['sortOrder'] as int,
      imageUrl: json['imageUrl'] as String?,
    );
  }

  final String id;
  final String name;
  final int sortOrder;
  final String? imageUrl;
}

/// Mirrors Purch.Application.Catalog.CreateCategoryRequest.
class CreateCategoryRequest {
  const CreateCategoryRequest({
    required this.name,
    required this.sortOrder,
    this.imageUrl,
  });

  final String name;
  final int sortOrder;
  final String? imageUrl;

  Map<String, dynamic> toJson() => {
    'name': name,
    'sortOrder': sortOrder,
    'imageUrl': imageUrl,
  };
}
