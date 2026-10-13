import 'dart:async';
import 'dart:convert';

import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'core.dart';
import 'notify.dart';
import 'sync.dart';

/// 앱 전체 상태: 일정 데이터, 동기화, 알림 설정
class AppStore extends ChangeNotifier {
  Json data = <String, dynamic>{'_deleted': <String, dynamic>{}};
  Map<String, dynamic>? syncCfg; // {repo, tok, key, p}
  String syncStatus = '';
  bool syncing = false;
  bool notify = true;
  int lead = 10;
  String brief = '09:00';
  late SharedPreferences _prefs;
  double textScale = 1.0; // 앱 전체 글자 배율
  double calScale = 1.0; // 달력 칸 안 글자 배율

  Future<void> setScales({double? text, double? cal}) async {
    if (text != null) textScale = text;
    if (cal != null) calScale = cal;
    await _prefs.setDouble('textScale', textScale);
    await _prefs.setDouble('calScale', calScale);
    notifyListeners();
  }
  Timer? _saveTimer, _syncTimer, _notifyTimer;

  Future<void> load() async {
    _prefs = await SharedPreferences.getInstance();
    final raw = _prefs.getString('data');
    if (raw != null) {
      try {
        data = jsonDecode(raw) as Json;
      } catch (_) {}
    }
    ensureIds(data);
    final c = _prefs.getString('sync');
    if (c != null) syncCfg = jsonDecode(c) as Map<String, dynamic>;
    textScale = _prefs.getDouble('textScale') ?? 1.0;
    calScale = _prefs.getDouble('calScale') ?? 1.0;
    notify = _prefs.getBool('notify') ?? true;
    lead = _prefs.getInt('lead') ?? 10;
    brief = _prefs.getString('brief') ?? '09:00';
  }

  Future<void> _persist() async => _prefs.setString('data', jsonEncode(data));

  /// 일정이 바뀌었을 때 호출: 화면 갱신 + 저장 + (연결돼 있으면) 동기화 + 알림 다시 예약
  void changed({bool sync = true}) {
    notifyListeners();
    _saveTimer?.cancel();
    _saveTimer = Timer(const Duration(milliseconds: 300), _persist);
    if (sync) scheduleSync();
    _notifyTimer?.cancel();
    _notifyTimer = Timer(const Duration(seconds: 2), () => Notifier.reschedule(this));
  }

  void scheduleSync() {
    if (syncCfg == null) return;
    _syncTimer?.cancel();
    _syncTimer = Timer(const Duration(seconds: 4), syncNow);
  }

  Future<void> saveSettings() async {
    await _prefs.setBool('notify', notify);
    await _prefs.setInt('lead', lead);
    await _prefs.setString('brief', brief);
    notifyListeners();
    Notifier.reschedule(this);
  }

  Future<void> connect(Map<String, dynamic> cfg) async {
    syncCfg = cfg;
    await _prefs.setString('sync', jsonEncode(cfg));
    notifyListeners();
    await syncNow();
  }

  Future<void> disconnect() async {
    syncCfg = null;
    await _prefs.remove('sync');
    syncStatus = '';
    notifyListeners();
  }

  Future<void> syncNow() async {
    final cfg = syncCfg;
    if (cfg == null || syncing) return;
    syncing = true;
    syncStatus = '동기화 중…';
    notifyListeners();
    try {
      final store = GitHubStore(cfg['repo'] as String, cfg['tok'] as String, (cfg['p'] as String?) ?? 'deskcalendar.enc');
      final key = cfg['key'] as String;
      var done = false;
      for (var i = 0; i < 4 && !done; i++) {
        final (blob, sha) = await store.get();
        final remote = blob == null ? null : await decryptState(key, blob);
        if (remote != null && mergeState(data, remote)) {
          await _persist();
          notifyListeners();
          _notifyTimer?.cancel();
          _notifyTimer = Timer(const Duration(seconds: 2), () => Notifier.reschedule(this));
        }
        final local = exportState(data);
        if (remote != null && canon(local) == canon(remote)) {
          done = true;
        } else {
          try {
            await store.put(await encryptState(key, local), sha);
            done = true;
          } on SyncConflict {
            continue;
          }
        }
      }
      final n = DateTime.now();
      syncStatus = done ? '동기화 완료 · ${two(n.hour)}:${two(n.minute)}' : '충돌이 잦아 잠시 후 다시 시도해요';
    } on SyncError catch (e) {
      syncStatus = e.msg;
    } catch (_) {
      syncStatus = '동기화에 실패했어요';
    } finally {
      syncing = false;
      notifyListeners();
    }
  }

  // ── 일정 조작 ──
  void add(String key, Json t) {
    addItem(data, key, t);
    changed();
  }

  void remove(Json t) {
    deleteItem(data, t);
    changed();
  }

  void toggle(Json t) {
    final src = t['_src'] as Json?;
    if (src != null) {
      final dd = (src['done_dates'] as List?) ?? <dynamic>[];
      src['done_dates'] = dd;
      final s = iso(t['_occ'] as DateTime);
      dd.contains(s) ? dd.remove(s) : dd.add(s);
      touch(src);
    } else {
      t['done'] = !(t['done'] == true);
      touch(t);
    }
    changed();
  }

  void cyclePri(Json t) {
    final b = baseOf(t);
    b['pri'] = (((b['pri'] as num?)?.toInt() ?? 2) - 1) % 4;
    touch(b);
    changed();
  }

  void toInbox(Json t) {
    final b = baseOf(t);
    removeItem(data, b);
    for (final k in ['time', 'end', 'repeat', 'done_dates']) {
      b.remove(k);
    }
    b['done'] = false;
    touch(b);
    addItem(data, '_inbox', b);
    changed();
  }

  void moveTo(Json t, DateTime d) {
    final b = baseOf(t);
    removeItem(data, b);
    touch(b);
    addItem(data, iso(d), b);
    changed();
  }

  void setMemo(DateTime d, String memo) {
    final e = data.putIfAbsent(iso(d), () => <String, dynamic>{'memo': '', 'todos': <dynamic>[]}) as Map;
    e['memo'] = memo;
    e['memo_ts'] = nowMs();
    changed();
  }

  String memoOf(DateTime d) => ((data[iso(d)] as Map?)?['memo'] as String?) ?? '';

  List<Json> inbox() => List<Json>.from(((data['_inbox'] as Map?)?['todos'] as List?) ?? const [])
    ..sort(sortCompare);
}
