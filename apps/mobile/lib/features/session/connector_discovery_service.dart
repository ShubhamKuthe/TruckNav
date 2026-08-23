import 'session_state.dart';

abstract interface class ConnectorDiscoveryService {
  Future<void> start();

  Future<List<ConnectorSummary>> scanOnce();

  Future<void> stop();
}
