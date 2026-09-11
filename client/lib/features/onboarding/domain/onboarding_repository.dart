import 'bootstrap_models.dart';
import 'staff_models.dart';

/// Covers the onboarding screens built so far (A1–A3 bootstrap, A4 staff).
/// Branch/device management, tenant settings, and the audit log viewer have
/// working backend endpoints already (see backend/src/Purch.Api/Endpoints/
/// OnboardingEndpoints.cs) but no client screens yet — their repository
/// methods land alongside those screens rather than being added speculatively
/// ahead of any UI that would call them.
abstract class OnboardingRepository {
  Future<BootstrapResult> bootstrap(BootstrapRequest request);

  Future<List<StaffMember>> listStaff();

  Future<StaffMember> createStaff(CreateStaffRequest request);

  Future<StaffMember> updateStaff(String staffId, UpdateStaffRequest request);
}
