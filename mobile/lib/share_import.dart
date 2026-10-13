// 폰에서 공문 PDF 를 '공유'하거나 '열기'로 보내면 일정으로 자동 추가한다
import 'dart:async';

import 'package:flutter/material.dart';
import 'package:pdfrx/pdfrx.dart';
import 'package:receive_sharing_intent/receive_sharing_intent.dart';

import 'core.dart';
import 'gongmun.dart';
import 'store.dart';

Future<String> pdfText(String path) async {
  final doc = await PdfDocument.openFile(path);
  final sb = StringBuffer();
  try {
    for (final page in doc.pages) {
      final t = await page.loadText();
      sb.writeln(t.fullText);
    }
  } finally {
    await doc.dispose();
  }
  return sb.toString();
}

/// 공문 텍스트에서 핵심 일정만 달력에 넣고, 추가한 항목들을 돌려준다 (같은 날 같은 내용은 건너뜀)
List<Json> importGongmunText(AppStore store, String text, {String? srcName}) {
  final today = dayOnly(DateTime.now());
  final (title, items) = parseGongmun(text, today);
  final added = <Json>[];
  for (final it in items) {
    var d = it.date;
    DateTime? end = it.end;
    var note = title;
    if (end != null && end.isAfter(d) && !it.period) {
      note = '$title · 기간 ${d.month}/${d.day} ~ ${end.month}/${end.day}';
      d = end;
      end = null;
    }
    if (dayItems(store.data, d).any((r) => r.t['text'] == it.text)) continue;
    final extra = <String, dynamic>{'pri': it.pri, 'note': note};
    if (it.time.isNotEmpty) extra['time'] = it.time;
    if (end != null && end.isAfter(d)) extra['end'] = iso(end);
    final t = newItem(it.text, extra: extra);
    store.add(iso(d), t);
    added.add(t);
  }
  return added;
}

/// 공유로 들어온 파일 처리 (앱이 열려 있을 때/처음 열릴 때 모두)
class ShareImporter {
  final AppStore store;
  final BuildContext Function() ctx;
  StreamSubscription? _sub;
  ShareImporter(this.store, this.ctx);

  void start() {
    try {
      ReceiveSharingIntent.instance.getInitialMedia().then((m) {
        _handle(m);
        ReceiveSharingIntent.instance.reset();
      }).catchError((_) {});
      _sub = ReceiveSharingIntent.instance.getMediaStream().listen(_handle, onError: (_) {});
    } catch (_) {}
  }

  void dispose() => _sub?.cancel();

  Future<void> _handle(List<SharedMediaFile> files) async {
    final pdfs = files.where((f) => f.path.toLowerCase().endsWith('.pdf')).toList();
    if (pdfs.isEmpty) return;
    final messenger = ScaffoldMessenger.of(ctx());
    final all = <Json>[];
    var bad = 0;
    for (final f in pdfs) {
      try {
        final text = await pdfText(f.path);
        if (text.trim().length < 20) {
          bad++;
          continue;
        }
        all.addAll(importGongmunText(store, text));
      } catch (_) {
        bad++;
      }
    }
    messenger.showSnackBar(SnackBar(
      duration: const Duration(seconds: 8),
      content: Text(all.isNotEmpty
          ? '공문에서 일정 ${all.length}개를 추가했어요'
          : bad > 0
              ? '글자를 읽을 수 없는 PDF예요 (스캔 이미지는 지원하지 않아요)'
              : '추가할 새 일정을 찾지 못했어요'),
      action: all.isEmpty
          ? null
          : SnackBarAction(
              label: '실행 취소',
              onPressed: () {
                for (final t in all) {
                  store.remove(t);
                }
              }),
    ));
  }
}
