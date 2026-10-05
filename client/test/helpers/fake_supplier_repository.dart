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
      contactInfo: null,
      isActive: true,
      specialization: request.specialization,
      contacts: request.contacts,
    );
    suppliers.add(created);
    return created;
  }

  UpdateSupplierRequest? lastUpdateRequest;

  @override
  Future<Supplier> updateSupplier(
    String supplierId,
    UpdateSupplierRequest request,
  ) async {
    lastUpdateRequest = request;
    final index = suppliers.indexWhere((s) => s.id == supplierId);
    final updated = Supplier(
      id: supplierId,
      name: request.name,
      contactInfo: null,
      isActive: request.isActive,
      specialization: request.specialization,
      address: request.address,
      tin: request.tin,
      remarks: request.remarks,
      contacts: request.contacts,
    );
    suppliers[index] = updated;
    return updated;
  }
}
