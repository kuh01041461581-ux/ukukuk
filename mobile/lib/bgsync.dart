// 백그라운드 동기화: 앱이 꺼져 있어도 약 15분마다 PC 의 변경을 받아 알림·위젯을 갱신한다 (안드로이드)
import 'dart:io';

import 'package:flutter/foundation.dart' show kIsWeb;
import 'package:flutter/widgets.dart';
import 'package:workmanager/workmanager.dart';

import 'package:home_widget/home_widget.dart';

import 'core.dart';
import 'notify.dart';
import 'store.dart';
import 'widget_data.dart';
import 'widget_sync.dart';

const _taskName = 'dc-background-sync';

@pragma('vm:entry-point')
void callbackDispatcher() {
  Workmanager().executeTask((task, input) async {
    try {
      WidgetsFlutterBinding.ensureInitialized();
      await Notifier.init();
      final store = AppStore();
      await store.load();
      if (store.syncCfg != null) await store.syncNow();
      await Notifier.reschedule(store);
      await updateHomeWidget(store);
      return true;
    } catch (_) {
      return false;
    }
  });
}

/// 홈 화면 '할 일' 위젯에서 항목을 눌렀을 때(앱을 열지 않고) 체크/체크 해제한다
@pragma('vm:entry-point')
Future<void> widgetBackgroundCallback(Uri? uri) async {
  if (uri == null || uri.host != 'toggle' || uri.pathSegments.isEmpty) return;
  try {
    WidgetsFlutterBinding.ensureInitialized();
    final store = AppStore();
    await store.load();
    final t = findForToggle(store.data, DateTime.now(), uri.pathSegments.first);
    if (t != null) {
      store.toggle(t);
      await store.persistNow();
    }
    await updateHomeWidget(store);
    if (store.syncCfg != null) await store.syncNow();
  } catch (_) {}
}

Future<void> registerBackgroundSync() async {
  if (kIsWeb || !Platform.isAndroid) return;
  try {
    await HomeWidget.registerInteractivityCallback(widgetBackgroundCallback);
    await Workmanager().initialize(callbackDispatcher, isInDebugMode: false);
    await Workmanager().registerPeriodicTask(_taskName, _taskName,
        frequency: const Duration(minutes: 15),
        existingWorkPolicy: ExistingWorkPolicy.keep,
        constraints: Constraints(networkType: NetworkType.connected));
  } catch (_) {}
}
