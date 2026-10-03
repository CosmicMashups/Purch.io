/// A business a person belongs to, offered when their email is a member of several.
class BusinessChoice {
  const BusinessChoice({required this.tenantId, required this.name});

  factory BusinessChoice.fromJson(Map<String, dynamic> json) {
    return BusinessChoice(
      tenantId: json['tenantId'] as String,
      name: json['name'] as String,
    );
  }

  final String tenantId;
  final String name;
}

/// What an email and password sign-in came back with: either a session (already
/// stored), or a list of businesses to choose from before signing in again.
sealed class SignInOutcome {
  const SignInOutcome();
}

final class SignedIn extends SignInOutcome {
  const SignedIn();
}

final class ChooseBusiness extends SignInOutcome {
  const ChooseBusiness(this.businesses);

  final List<BusinessChoice> businesses;
}

/// Mirrors Purch.Domain.Enums.DeviceType in declared order.
enum PairedDeviceType {
  register,
  kiosk,
  orderBoard,
  kitchenDisplay,
  warehouseOfficer,
  customerDisplay,
}

/// What the server says about this device when it opens a session.
class DeviceSession {
  const DeviceSession({
    required this.requiresStaff,
    required this.deviceType,
    this.name,
  });

  /// A Register or Warehouse device has no access of its own; a person unlocks it with their PIN.
  final bool requiresStaff;
  final PairedDeviceType deviceType;
  final String? name;
}

/// A person who can unlock this device. Mirrors Purch.Application.Devices.RosterEntryDto.
class RosterPerson {
  const RosterPerson({
    required this.membershipId,
    required this.name,
    required this.role,
    required this.hasPin,
  });

  factory RosterPerson.fromJson(Map<String, dynamic> json) {
    return RosterPerson(
      membershipId: json['membershipId'] as String,
      name: json['name'] as String,
      role: json['role'] as int? ?? 2,
      hasPin: json['hasPin'] as bool? ?? false,
    );
  }

  final String membershipId;
  final String name;

  /// 0 Admin, 1 Manager, 2 Staff.
  final int role;
  final bool hasPin;
}

class DeviceRoster {
  const DeviceRoster({required this.deviceName, required this.people});

  final String? deviceName;
  final List<RosterPerson> people;
}
