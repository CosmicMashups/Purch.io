import 'package:purch_client/features/onboarding/domain/bootstrap_models.dart';
import 'package:purch_client/features/onboarding/domain/onboarding_enums.dart';
import 'package:purch_client/features/onboarding/domain/onboarding_repository.dart';
import 'package:purch_client/features/onboarding/domain/staff_models.dart';

class FakeOnboardingRepository implements OnboardingRepository {
  FakeOnboardingRepository({
    this.bootstrapFailure,
    this.createStaffFailure,
    List<StaffMember>? initialStaff,
  }) : staff = initialStaff ?? [];

  final Object? bootstrapFailure;
  final Object? createStaffFailure;
  final List<StaffMember> staff;

  BootstrapRequest? lastBootstrapRequest;
  CreateStaffRequest? lastCreateStaffRequest;

  @override
  Future<BootstrapResult> bootstrap(BootstrapRequest request) async {
    lastBootstrapRequest = request;
    if (bootstrapFailure != null) {
      throw bootstrapFailure!;
    }
    return const BootstrapResult(
      tenantId: 'tenant-1',
      branchId: 'branch-1',
      deviceId: 'device-1',
      devicePairingCode: 'ABCD1234',
      adminUserId: 'admin-1',
    );
  }

  @override
  Future<List<StaffMember>> listStaff() async => staff;

  @override
  Future<StaffMember> createStaff(CreateStaffRequest request) async {
    lastCreateStaffRequest = request;
    if (createStaffFailure != null) {
      throw createStaffFailure!;
    }
    final created = StaffMember(
      id: 'staff-${staff.length + 1}',
      name: request.name,
      role: request.role,
      scopeType: ScopeType.tenant,
      scopeId: null,
      branchId: null,
      isActive: true,
    );
    staff.add(created);
    return created;
  }

  @override
  Future<StaffMember> updateStaff(
    String staffId,
    UpdateStaffRequest request,
  ) async {
    throw UnimplementedError();
  }
}
