// 연결 코드: QR 에 들어 있는 내용과 같은 정보를 글자로 주고받는다 (QR 스캔이 어려운 기기·웹앱용)
import 'dart:convert';

/// 'DC1:...'(PC 의 [연결 코드 복사]) 또는 QR 내용(JSON)을 해석한다. 올바르지 않으면 null
Map<String, dynamic>? parsePairing(String raw) {
  var s = raw.trim();
  try {
    if (s.startsWith('DC1:')) s = utf8.decode(base64.decode(s.substring(4).trim()));
    final j = jsonDecode(s);
    if (j is! Map<String, dynamic>) return null;
    final ok = j['key'] != null && ((j['relay'] != null && j['id'] != null) || (j['repo'] != null && j['tok'] != null));
    return ok ? j : null;
  } catch (_) {
    return null;
  }
}
