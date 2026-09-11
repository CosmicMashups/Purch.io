import 'package:purch_client/features/inventory/domain/supplier_models.dart';
import 'package:purch_client/features/inventory/domain/supplier_repository.dart';

class FakeSupplierRepository implements SupplierRepository {
  FakeSupplierRepository({
    this.createSupplierFailure,
    List<Supplier>? initialSuppliers,
  }) : suppliers = initialSuppliers ?? [];

  final Object? createSupplierFailure;
  final List<Supplier> suppliers;

  CreateSupplierRequest? lastCreateRequest;

  @override
  Future<List<Supplier>> listSuppliers() async => suppliers;

  @override
  Future<Supplier> createSupplier(CreateSupplierRequest request) async {
    lastCreateRequest = request;
    if (createSupplierFailure != null) {
      throw createSupplierFailure!;
    }
    final created = Supplier(
      id: 'supplier-${suppliers.length + 1}',
      name: request.name,
      contactInfo: request.contactInfo,
      isActive: true,
    );
    suppliers.add(created);
    return created;
  }
}
