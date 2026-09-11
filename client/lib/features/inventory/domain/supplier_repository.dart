import 'supplier_models.dart';

/// C5 — the supplier list a purchase order is created against.
abstract class SupplierRepository {
  Future<List<Supplier>> listSuppliers();

  Future<Supplier> createSupplier(CreateSupplierRequest request);
}
