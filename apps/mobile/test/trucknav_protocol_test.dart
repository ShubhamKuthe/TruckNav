import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';

import 'package:trucknav_mobile/features/session/trucknav_protocol.dart';

void main() {
  test('parseProtocolInboundMessage parses game_state', () {
    const payload = '''
    {
      "type": "game_state",
      "games": [
        { "game": "ets2", "running": true },
        { "game": "ats", "running": false }
      ]
    }
    ''';

    final message = parseProtocolInboundMessage(payload);

    expect(message, isA<GameStateProtocolMessage>());
    final gameState = message as GameStateProtocolMessage;
    expect(gameState.games, hasLength(2));
    expect(gameState.games.first.game, 'ets2');
    expect(gameState.games.first.running, isTrue);
    expect(gameState.games.last.game, 'ats');
    expect(gameState.games.last.running, isFalse);
  });

  test('encodeGameStateRequestMessage writes a typed request', () {
    final payload = jsonDecode(encodeGameStateRequestMessage()) as Map<String, dynamic>;

    expect(payload['type'], 'game_state_request');
  });
}
