// 백그라운드 동기화: 앱이 꺼져 있어도 약 15분마다 PC 의 변경을 받아 알림·위젯을 갱신한다 (안드로이드)
import 'dart:io';

import 'package:flutter/widgets.dart';
import 'package:workmanager/workmanager.dart';

import 'notify.dart';
import 'store.dart';
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

Future<void> registerBackgroundSync() async {
  if (!Platform.isAndroid) return;
  try {
    await Workmanager().initialize(callbackDispatcher, isInDebugMode: false);
    await Workmanager().registerPeriodicTask(_taskName, _taskName,
        frequency: const Duration(minutes: 15),
        existingWorkPolicy: ExistingWorkPolicy.keep,
        constraints: Constraints(networkType: NetworkType.connected));
  } catch (_) {}
}
