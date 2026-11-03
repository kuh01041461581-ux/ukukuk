package com.seoultycoon;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.DialogInterface;
import android.os.Bundle;
import android.view.KeyEvent;
import android.view.View;
import android.view.WindowManager;
import android.widget.EditText;
import com.xce.lcdui.TextComponent;
import com.xce.lcdui.TextComponentHandler;
import javax.microedition.lcdui.Display;

public class GameActivity extends Activity {
    static boolean started;
    GameView view;

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        com.xce.io.XFile.dir = getFilesDir();
        view = new GameView(this);
        setContentView(view);
        view.setSystemUiVisibility(View.SYSTEM_UI_FLAG_HIDE_NAVIGATION | View.SYSTEM_UI_FLAG_FULLSCREEN | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
        TextComponentHandler.prompter = new TextComponentHandler.Prompter() {
            public void prompt(final TextComponent tc) { runOnUiThread(new Runnable() { public void run() { askName(tc); } }); }
        };
        if (!started) {
            started = true;
            new Thread(new Runnable() { public void run() {
                try {
                    Object m = Class.forName("Seoul").getDeclaredConstructor().newInstance();
                    m.getClass().getMethod("startApp").invoke(m);
                } catch (Throwable t) { t.printStackTrace(); }
            } }, "game-start").start();
        }
    }

    void askName(final TextComponent tc) {
        final EditText et = new EditText(this);
        et.setSingleLine(true);
        new AlertDialog.Builder(this).setTitle("시장님 존함 (최대 " + tc.getMaxSize() + "자)").setView(et).setCancelable(false)
            .setPositiveButton("확인", new DialogInterface.OnClickListener() {
                public void onClick(DialogInterface d, int w) {
                    final String s = et.getText().toString();
                    new Thread(new Runnable() { public void run() {
                        for (int i = 0; i < s.length(); i++) tc.insert(s.charAt(i));
                        Display.current.keyPressed(0); Display.current.keyReleased(0);   // 글자수 갱신
                        Display.current.keyPressed(GameView.OK); Display.current.keyReleased(GameView.OK);
                    } }).start();
                }
            }).show();
    }

    @Override public boolean onKeyDown(int kc, KeyEvent e) {
        int k = map(kc);
        if (k != 0 && Display.current != null) { Display.current.keyPressed(k); return true; }
        return super.onKeyDown(kc, e);
    }
    @Override public boolean onKeyUp(int kc, KeyEvent e) {
        int k = map(kc);
        if (k != 0 && Display.current != null) { Display.current.keyReleased(k); return true; }
        return super.onKeyUp(kc, e);
    }
    static int map(int kc) {
        switch (kc) {
            case KeyEvent.KEYCODE_DPAD_UP: return GameView.UP;
            case KeyEvent.KEYCODE_DPAD_DOWN: return GameView.DOWN;
            case KeyEvent.KEYCODE_DPAD_LEFT: return GameView.LEFT;
            case KeyEvent.KEYCODE_DPAD_RIGHT: return GameView.RIGHT;
            case KeyEvent.KEYCODE_DPAD_CENTER: case KeyEvent.KEYCODE_ENTER: case KeyEvent.KEYCODE_SPACE: return GameView.OK;
            case KeyEvent.KEYCODE_BACK: case KeyEvent.KEYCODE_DEL: return GameView.CLR;
            case KeyEvent.KEYCODE_STAR: return '*';
            case KeyEvent.KEYCODE_POUND: return '#';
        }
        if (kc >= KeyEvent.KEYCODE_0 && kc <= KeyEvent.KEYCODE_9) return '0' + (kc - KeyEvent.KEYCODE_0);
        return 0;
    }
    @Override protected void onDestroy() { super.onDestroy(); if (isFinishing()) System.exit(0); }
}
