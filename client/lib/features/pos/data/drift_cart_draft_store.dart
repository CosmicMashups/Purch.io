import '../../../core/db/daos/device_identity_dao.dart';
import '../../../core/db/daos/local_cart_draft_dao.dart';
import 'local_first_pos_repository.dart';

/// Persists the draft cart in the app's local SQLite database, keyed by
/// tenant + device so a draft can never show up under another tenant's session.
class DriftCartDraftStore implements CartDraftStore {
  DriftCartDraftStore({
    required LocalCartDraftDao dao,
    required DeviceIdentityDao identityDao,
  }) : _dao = dao,
       _identityDao = identityDao;

  final LocalCartDraftDao _dao;
  final DeviceIdentityDao _identityDao;
  String? _key;

  Future<String> _draftKey() async {
    final cached = _key;
    if (cached != null) {
      return cached;
    }
    final identity = await _identityDao.getIdentity();
    // Without an identity there is no tenant to scope to; don't cache the
    // fallback, so the real key is used as soon as the identity exists.
    if (identity == null) {
      return 'unpaired';
    }
    return _key = '${identity.tenantId}:${identity.deviceId}';
  }

  @override
  Future<String?> read() async => _dao.read(await _draftKey());

  @override
  Future<void> write(String json) async => _dao.write(await _draftKey(), json);

  @override
  Future<void> clear() async => _dao.clear(await _draftKey());
}
