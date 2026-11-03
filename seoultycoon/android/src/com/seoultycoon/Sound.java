package com.seoultycoon;

import android.content.Context;
import android.media.MediaPlayer;
import com.skt.m.AudioClip;
import java.io.File;
import java.io.FileOutputStream;
import java.util.concurrent.CountDownLatch;

/** 원본 MMF 효과음/배경음은 빌드 때 표준 MIDI 로 변환해 들어 있다. MediaPlayer 로 재생(play/loop 는 끝날 때까지 블록 — SKT API 동작과 동일). */
public class Sound implements AudioClip {
    public static volatile int volume = 5;
    public static volatile Context ctx;
    private static int seq;
    private File file; private MediaPlayer mp; private volatile CountDownLatch done = new CountDownLatch(1);

    public synchronized void open(byte[] b, int o, int l) {
        try {
            file = new File(ctx.getCacheDir(), "snd" + (seq++ & 7) + ".mid");
            FileOutputStream f = new FileOutputStream(file); f.write(b, o, l); f.close();
        } catch (Exception e) { file = null; }
    }
    private boolean start(boolean loop) {
        try {
            synchronized (this) {
                if (file == null) return false;
                done = new CountDownLatch(1);
                mp = new MediaPlayer(); mp.setDataSource(file.getAbsolutePath()); mp.setLooping(loop);
                float v = Math.max(0, Math.min(10, volume)) / 10f; mp.setVolume(v, v);
                mp.setOnCompletionListener(new MediaPlayer.OnCompletionListener() { public void onCompletion(MediaPlayer m) { done.countDown(); } });
                mp.setOnErrorListener(new MediaPlayer.OnErrorListener() { public boolean onError(MediaPlayer m, int w, int x) { done.countDown(); return true; } });
                mp.prepare(); mp.start();
            }
            return true;
        } catch (Exception e) { return false; }
    }
    public void play() { if (start(false)) try { done.await(); } catch (InterruptedException e) { } }
    public void loop() { if (start(true)) try { done.await(); } catch (InterruptedException e) { } }
    public synchronized void stop() {
        try { if (mp != null) { mp.stop(); mp.release(); mp = null; } } catch (Exception e) { }
        done.countDown();
    }
    public synchronized void close() { stop(); if (file != null) file.delete(); file = null; }
}
