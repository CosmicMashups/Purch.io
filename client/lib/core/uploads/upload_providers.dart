import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:riverpod_annotation/riverpod_annotation.dart';

import '../../features/auth/presentation/providers/auth_providers.dart';
import 'upload_repository.dart';

part 'upload_providers.g.dart';

@Riverpod(keepAlive: true)
UploadRepository uploadRepository(Ref ref) {
  return UploadRepository(apiClient: ref.watch(apiClientProvider));
}
