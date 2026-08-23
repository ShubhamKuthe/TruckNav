import 'dart:io';

List<String> rankConnectorAddresses(
  Iterable<InternetAddress> addresses, {
  String? fallbackHost,
}) {
  final usable = addresses.where(_isUsableAddress).toList(growable: false)
    ..sort(_compareAddresses);

  final ranked = <String>[];
  final seen = <String>{};

  for (final address in usable) {
    if (seen.add(address.address)) {
      ranked.add(address.address);
    }
  }

  if (fallbackHost != null) {
    final trimmedFallback = fallbackHost.trim();
    if (trimmedFallback.isNotEmpty && seen.add(trimmedFallback)) {
      ranked.add(trimmedFallback);
    }
  }

  return ranked;
}

bool _isUsableAddress(InternetAddress address) {
  if (address.isLoopback || address.isMulticast) {
    return false;
  }

  if (address.address == InternetAddress.anyIPv4.address ||
      address.address == InternetAddress.anyIPv6.address) {
    return false;
  }

  if (address.type == InternetAddressType.IPv6 && address.isLinkLocal) {
    return false;
  }

  return true;
}

int _compareAddresses(InternetAddress left, InternetAddress right) {
  final leftPriority = _addressPriority(left);
  final rightPriority = _addressPriority(right);
  if (leftPriority != rightPriority) {
    return leftPriority.compareTo(rightPriority);
  }

  return left.address.compareTo(right.address);
}

int _addressPriority(InternetAddress address) {
  if (address.type == InternetAddressType.IPv4) {
    return _isPrivateIpv4(address) ? 0 : 1;
  }

  return _isUniqueLocalIpv6(address) ? 2 : 3;
}

bool _isPrivateIpv4(InternetAddress address) {
  final bytes = address.rawAddress;
  if (bytes.length != 4) {
    return false;
  }

  final first = bytes[0];
  final second = bytes[1];
  return first == 10 ||
      (first == 172 && second >= 16 && second <= 31) ||
      (first == 192 && second == 168);
}

bool _isUniqueLocalIpv6(InternetAddress address) {
  final bytes = address.rawAddress;
  if (bytes.length != 16) {
    return false;
  }

  return (bytes[0] & 0xFE) == 0xFC;
}
