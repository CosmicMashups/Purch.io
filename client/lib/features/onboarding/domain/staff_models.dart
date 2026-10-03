/// A person's place in the business. Mirrors Purch.Domain.Enums.MembershipRole.
enum MemberRole { admin, manager, staff }

/// Staff duties are flags on a Staff member. Mirrors Purch.Domain.Enums.StaffDuty.
class StaffDuties {
  static const int none = 0;
  static const int cashier = 1;
  static const int warehouse = 2;

  static bool has(int duties, int duty) => duties & duty != 0;

  static String describe(int duties) {
    final names = [
      if (has(duties, cashier)) 'Cashier',
      if (has(duties, warehouse)) 'Warehouse',
    ];
    return names.isEmpty ? 'No duties' : names.join(' and ');
  }
}

/// Mirrors Purch.Application.Onboarding.MemberDto.
class StaffMember {
  const StaffMember({
    required this.id,
    required this.name,
    required this.email,
    required this.role,
    required this.duties,
    required this.branchIds,
    required this.isActive,
    required this.hasPin,
  });

  factory StaffMember.fromJson(Map<String, dynamic> json) {
    return StaffMember(
      id: json['id'] as String,
      name: json['name'] as String,
      email: json['email'] as String? ?? '',
      role: MemberRole.values[json['role'] as int? ?? 2],
      duties: json['duties'] as int? ?? 0,
      branchIds: (json['branchIds'] as List<dynamic>? ?? const []).cast<String>(),
      isActive: json['isActive'] as bool? ?? true,
      hasPin: json['hasPin'] as bool? ?? false,
    );
  }

  final String id;
  final String name;
  final String email;
  final MemberRole role;
  final int duties;
  final List<String> branchIds;
  final bool isActive;
  final bool hasPin;
}

/// Mirrors Purch.Application.Onboarding.CreateInviteRequest. An Admin or Manager
/// works every branch, so [duties] and [branchIds] only matter for [MemberRole.staff].
class InviteStaffRequest {
  const InviteStaffRequest({
    required this.name,
    required this.email,
    required this.role,
    this.duties = StaffDuties.none,
    this.branchIds = const [],
  });

  final String name;
  final String email;
  final MemberRole role;
  final int duties;
  final List<String> branchIds;

  Map<String, dynamic> toJson() => {
    'name': name,
    'email': email,
    'role': role.index,
    'duties': role == MemberRole.staff ? duties : StaffDuties.none,
    'branchIds': role == MemberRole.staff ? branchIds : const <String>[],
    'legacyUserId': null,
  };
}

/// A single-use link for the invited person. No email is sent: the admin hands
/// it over. The token is shown once; the server keeps only its hash.
class StaffInviteLink {
  const StaffInviteLink({
    required this.name,
    required this.email,
    required this.token,
    required this.expiresAt,
  });

  factory StaffInviteLink.fromJson(Map<String, dynamic> json) {
    final invite = json['invite'] as Map<String, dynamic>;
    return StaffInviteLink(
      name: invite['name'] as String,
      email: invite['email'] as String,
      token: json['token'] as String,
      expiresAt: DateTime.parse(invite['expiresAt'] as String),
    );
  }

  final String name;
  final String email;
  final String token;
  final DateTime expiresAt;

  /// The path the person opens on the web app; put the app's address in front of it.
  String get path => '/enrol/$token';
}
