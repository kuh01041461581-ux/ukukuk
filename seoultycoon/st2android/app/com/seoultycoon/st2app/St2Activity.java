package com.seoultycoon.st2app;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.DialogInterface;
import android.graphics.Bitmap;
import android.graphics.Typeface;
import android.os.Bundle;
import android.view.KeyEvent;
import android.view.View;
import android.view.WindowManager;
import android.widget.EditText;
import com.seoultycoon.st2.Host;
import com.seoultycoon.st2.Surf;
import java.io.InputStream;

public class St2Activity extends Activity implements St2View.Opener {
    St2View view; Host host; AndroidPlatform pf; volatile boolean running; Thread loop; Bitmap lcd;

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        Typeface tf = Typeface.DEFAULT;
        try { tf = Typeface.createFromAsset(getAssets(), "galmuri11.ttf"); } catch (Throwable t) { }
        pf = new AndroidPlatform(this, tf);
        lcd = Bitmap.createBitmap(960, 1184, Bitmap.Config.ARGB_8888);      // 화질 보정: 도트 확대(EPX 4배) 결과를 담는다
        view = new St2View(this); view.host = this; view.lcd = lcd;
        view.mode = getSharedPreferences("st2", MODE_PRIVATE).getInt("mode", St2View.MODE_PHONE);
        setContentView(view);
        view.setSystemUiVisibility(View.SYSTEM_UI_FLAG_HIDE_NAVIGATION | View.SYSTEM_UI_FLAG_FULLSCREEN | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
        final String name = getSharedPreferences("st2", MODE_PRIVATE).getString("name", null);
        if (name == null) askName(); else start(name);
    }

    void askName() {
        final EditText et = new EditText(this); et.setSingleLine(true); et.setText("시장");
        final android.widget.CheckBox cb = new android.widget.CheckBox(this); cb.setText("튜토리얼 자동 넘김 (처음 한 번)"); cb.setChecked(true);
        final android.widget.LinearLayout box = new android.widget.LinearLayout(this); box.setOrientation(android.widget.LinearLayout.VERTICAL); box.setPadding(40, 10, 40, 0); box.addView(et); box.addView(cb);
        new AlertDialog.Builder(this).setTitle("시장님 존함 (게임 시작 시 사용)").setView(box).setCancelable(false)
            .setPositiveButton("확인", new DialogInterface.OnClickListener() { public void onClick(DialogInterface d, int w) {
                String s = et.getText().toString().trim(); if (s.length() == 0) s = "시장"; if (s.length() > 5) s = s.substring(0, 5);
                getSharedPreferences("st2", MODE_PRIVATE).edit().putString("name", s).putBoolean("skiptut", cb.isChecked()).apply(); start(s);
            } }).show();
    }

    void start(final String name) {
        running = true;
        loop = new Thread(null, new Runnable() { public void run() {
            try {
                byte[] img = pf.resource("client.bin1116");
                host = new Host(pf, img); host.defaultName = name; if (getSharedPreferences("st2", MODE_PRIVATE).getBoolean("skiptut", false)) { host.startAutoTutorial(); getSharedPreferences("st2", MODE_PRIVATE).edit().putBoolean("skiptut", false).apply(); } view.game_host = host; host.timeScale = speed();
                host.boot(); host.startApp();
                long last = System.nanoTime(); int[] px = new int[240 * 296];
                while (running) {
                    long t0 = System.nanoTime(); int dt = (int) Math.min(100, (t0 - last) / 1000000); last = t0;
                    host.timeScale = speed();
                    if (host.tick(Math.max(8, dt))) {
                        Surf s = host.screen; for (int i = 0; i < px.length; i++) px[i] = s.px[i] | 0xFF000000; int[] big = com.seoultycoon.st2.PixelScale.nn4(px, 240, 296); synchronized (lcd) { lcd.setPixels(big, 0, 960, 0, 0, 960, 1184); }
                        view.frames++;
                    }
                    long el = (System.nanoTime() - t0) / 1000000; if (el < 16) Thread.sleep(16 - el);
                }
            } catch (Throwable t) {
                android.util.Log.e("st2", "fatal", t);
                java.io.StringWriter sw = new java.io.StringWriter(); t.printStackTrace(new java.io.PrintWriter(sw));
                String m = sw.toString(); if (m.length() > 900) m = m.substring(0, 900);
                view.err = m; view.postInvalidate();
                try { pf.writeFile("error.txt", m.getBytes("UTF-8")); } catch (Exception e) { }
            }
        } }, "st2-game", 256L * 1024 * 1024);
        loop.start();
    }

    static final double[] SPEEDS = {1.0, 1.5, 2.0, 3.0};
    double speed() { return SPEEDS[Math.max(0, Math.min(SPEEDS.length - 1, getSharedPreferences("st2", MODE_PRIVATE).getInt("speed", 2)))]; }
    public void openModeDialog() {
        new AlertDialog.Builder(this).setTitle("설정").setItems(new String[]{"조작 방식", "게임 속도 (부드러움)", "다음 실행 때 튜토리얼 자동 넘김"}, new DialogInterface.OnClickListener() {
            public void onClick(DialogInterface d, int w) { if (w == 0) openControlDialog(); else if (w == 1) openSpeedDialog(); else { getSharedPreferences("st2", MODE_PRIVATE).edit().putBoolean("skiptut", true).apply(); android.widget.Toast.makeText(St2Activity.this, "다음에 게임을 켤 때 튜토리얼을 자동으로 넘깁니다", android.widget.Toast.LENGTH_LONG).show(); } } }).show();
    }
    void openSpeedDialog() {
        final String[] n = {"1배 (원본 속도, 끊겨 보임)", "1.5배", "2배 (기본)", "3배 (가장 부드럽고 빠름)"};
        new AlertDialog.Builder(this).setTitle("게임 속도").setSingleChoiceItems(n, getSharedPreferences("st2", MODE_PRIVATE).getInt("speed", 2), new DialogInterface.OnClickListener() {
            public void onClick(DialogInterface d, int w) { getSharedPreferences("st2", MODE_PRIVATE).edit().putInt("speed", w).apply(); d.dismiss(); } }).show();
    }
    void openControlDialog() {
        final String[] names = {"피처폰 버튼 (키패드)", "방향키 버튼 + 확인/취소", "터치 전용 (전체 화면)\n  탭=확인 · 쓸기=방향 · 길게/두 손가락=취소"};
        new AlertDialog.Builder(this).setTitle("조작 방식").setSingleChoiceItems(names, view.mode, new DialogInterface.OnClickListener() {
            public void onClick(DialogInterface d, int w) { view.setMode(w); getSharedPreferences("st2", MODE_PRIVATE).edit().putInt("mode", w).apply(); d.dismiss(); } }).show();
    }

    @Override public boolean onKeyDown(int kc, KeyEvent e) { int k = map(kc); if (k != 0 && host != null) { host.postKey(1, k); return true; } return super.onKeyDown(kc, e); }
    @Override public boolean onKeyUp(int kc, KeyEvent e) { int k = map(kc); if (k != 0 && host != null) { host.postKey(2, k); return true; } return super.onKeyUp(kc, e); }
    static int map(int kc) {
        switch (kc) {
            case KeyEvent.KEYCODE_DPAD_UP: return St2View.UP; case KeyEvent.KEYCODE_DPAD_DOWN: return St2View.DOWN;
            case KeyEvent.KEYCODE_DPAD_LEFT: return St2View.LEFT; case KeyEvent.KEYCODE_DPAD_RIGHT: return St2View.RIGHT;
            case KeyEvent.KEYCODE_DPAD_CENTER: case KeyEvent.KEYCODE_ENTER: case KeyEvent.KEYCODE_SPACE: return St2View.OK;
            case KeyEvent.KEYCODE_BACK: case KeyEvent.KEYCODE_DEL: return St2View.CLR;
        }
        if (kc >= KeyEvent.KEYCODE_0 && kc <= KeyEvent.KEYCODE_9) return '0' + (kc - KeyEvent.KEYCODE_0);
        return 0;
    }
    @Override protected void onPause() { super.onPause(); pf.pause(); }
    @Override protected void onResume() { super.onResume(); pf.resume(); }
    @Override protected void onDestroy() { super.onDestroy(); running = false; if (isFinishing()) System.exit(0); }
}
