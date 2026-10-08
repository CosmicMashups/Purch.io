import 'package:purch_client/features/pos/data/sale_queue_integrity.dart';

/// A [SaleQueueIntegrity] with its key and "already sealed" flag held in memory, so tests never reach the platform keystore.
/// Two instances made with the same [key] agree on every seal, like two runs of the app on one install.
class InMemorySealStore {
  InMemorySealStore({this.key});

  String? key;
  bool sealed = false;

  SaleQueueIntegrity build() => SaleQueueIntegrity(
    readKey: () async => key,
    saveKey: (value) async => key = value,
    readSealed: () async => sealed,
    markSealed: () async => sealed = true,
  );
}
