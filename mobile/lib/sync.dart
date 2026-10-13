// 인터넷 동기화 (cloudsync.py 와 같은 방식): 암호화한 일정을 GitHub 비공개 저장소의 파일 하나로 주고받는다.
import 'dart:convert';
import 'dart:typed_data';

import 'package:cryptography/cryptography.dart';
import 'package:http/http.dart' as http;

import 'core.dart';

const _magic = [0x44, 0x43, 0x31]; // "DC1"
final _algo = AesGcm.with256bits();

class SyncConflict implements Exception {}

class SyncError implements Exception {
  final String msg;
  SyncError(this.msg);
  @override
  String toString() => msg;
}

Future<Uint8List> encryptState(String keyB64, Json state) async {
  final nonce = _algo.newNonce();
  final box = await _algo.encrypt(utf8.encode(jsonEncode(state)),
      secretKey: SecretKey(base64.decode(keyB64)), nonce: nonce);
  return Uint8List.fromList([..._magic, ...nonce, ...box.cipherText, ...box.mac.bytes]);
}

Future<Json> decryptState(String keyB64, Uint8List blob) async {
  if (blob.length < 3 + 12 + 16 || blob[0] != _magic[0] || blob[1] != _magic[1] || blob[2] != _magic[2]) {
    throw SyncError('알 수 없는 데이터 형식');
  }
  try {
    final nonce = blob.sublist(3, 15);
    final rest = blob.sublist(15);
    final box = SecretBox(rest.sublist(0, rest.length - 16), nonce: nonce, mac: Mac(rest.sublist(rest.length - 16)));
    final clear = await _algo.decrypt(box, secretKey: SecretKey(base64.decode(keyB64)));
    return jsonDecode(utf8.decode(clear)) as Json;
  } catch (_) {
    throw SyncError('복호화 실패: 암호 키가 다릅니다 (PC의 QR로 다시 연결하세요)');
  }
}

dynamic _sortKeys(dynamic v) {
  if (v is Map) {
    final k = v.keys.map((e) => e.toString()).toList()..sort();
    return {for (final x in k) x: _sortKeys(v[x])};
  }
  if (v is List) return v.map(_sortKeys).toList();
  return v;
}

/// 두 상태가 같은지 비교하기 위한 문자열
String canon(Json s) {
  final items = (s['items'] as List? ?? const []).map((e) => Map<String, dynamic>.from(e as Map)).toList()
    ..sort((a, b) => (a['id'] ?? '').toString().compareTo((b['id'] ?? '').toString()));
  return jsonEncode(_sortKeys({'items': items, 'memos': s['memos'] ?? {}, 'deleted': s['deleted'] ?? {}}));
}

class GitHubStore {
  final String repo, token, path;
  GitHubStore(this.repo, this.token, [this.path = 'deskcalendar.enc']);

  Uri get _url => Uri.parse('https://api.github.com/repos/$repo/contents/$path');
  Map<String, String> get _headers => {
        'Authorization': 'Bearer $token',
        'Accept': 'application/vnd.github+json',
        'X-GitHub-Api-Version': '2022-11-28',
        'User-Agent': 'DeskCalendar',
      };

  SyncError _err(int code) => SyncError({
        401: '토큰이 올바르지 않거나 만료됐어요',
        403: '토큰 권한이 부족해요 (Contents: Read and write 필요)',
        404: '저장소를 찾을 수 없어요 (이름·토큰 권한 확인)',
      }[code] ?? 'GitHub 오류 $code');

  /// (암호화된 바이트 또는 null, sha 또는 null)
  Future<(Uint8List?, String?)> get() async {
    try {
      final r = await http.get(_url, headers: _headers).timeout(const Duration(seconds: 20));
      if (r.statusCode == 404) {
        // 저장소 자체가 없는 경우와 파일이 없는 경우를 구분: 저장소 확인
        final rr = await http
            .get(Uri.parse('https://api.github.com/repos/$repo'), headers: _headers)
            .timeout(const Duration(seconds: 20));
        if (rr.statusCode == 200) return (null, null);
        throw _err(rr.statusCode == 404 ? 404 : rr.statusCode);
      }
      if (r.statusCode != 200) throw _err(r.statusCode);
      final j = jsonDecode(r.body) as Map;
      return (Uint8List.fromList(base64.decode((j['content'] as String).replaceAll('\n', ''))), j['sha'] as String?);
    } on SyncError {
      rethrow;
    } catch (_) {
      throw SyncError('인터넷에 연결할 수 없어요');
    }
  }

  Future<String> put(Uint8List blob, String? sha) async {
    try {
      final body = {'message': 'sync', 'content': base64.encode(blob), if (sha != null) 'sha': sha};
      final r = await http
          .put(_url, headers: {..._headers, 'Content-Type': 'application/json'}, body: jsonEncode(body))
          .timeout(const Duration(seconds: 30));
      if (r.statusCode == 409 || r.statusCode == 422) throw SyncConflict();
      if (r.statusCode != 200 && r.statusCode != 201) throw _err(r.statusCode);
      return (jsonDecode(r.body) as Map)['content']['sha'] as String;
    } on SyncError {
      rethrow;
    } on SyncConflict {
      rethrow;
    } catch (_) {
      throw SyncError('인터넷에 연결할 수 없어요');
    }
  }
}
