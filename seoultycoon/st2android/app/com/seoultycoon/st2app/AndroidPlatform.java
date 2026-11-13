package com.seoultycoon.st2app;

import android.content.Context;
import android.content.res.AssetManager;
import android.graphics.*;
import android.media.MediaPlayer;
import com.seoultycoon.st2.Platform;
import java.io.*;

/** 안드로이드용 Platform: 리소스=assets/st2/, 저장=filesDir, 글자=Galmuri11, 소리=MediaPlayer(MIDI) */
public class AndroidPlatform implements Platform {
    final Context ctx; final AssetManager am; final File dir; final Paint paint = new Paint();
    int vol = 100; int seq;
    public AndroidPlatform(Context c, Typeface tf) {
        ctx = c; am = c.getAssets(); dir = c.getFilesDir();
        paint.setTypeface(tf); paint.setTextSize(12); paint.setAntiAlias(false); paint.setColor(Color.WHITE);
        try { hdPaint.setTypeface(Typeface.createFromAsset(am, "pretendard.ttf")); hdPaint.setTextSize(11f * com.seoultycoon.st2.Surf.K); hdPaint.setColor(Color.WHITE); hdOk = true; } catch (Throwable t) { hdOk = false; }
    }
    static byte[] all(InputStream in) throws IOException {
        ByteArrayOutputStream o = new ByteArrayOutputStream(); byte[] b = new byte[16384]; int n; while ((n = in.read(b)) > 0) o.write(b, 0, n); in.close(); return o.toByteArray();
    }
    public byte[] resource(String n) { try { return all(am.open("st2mod/" + n)); } catch (IOException e0) { } try { return all(am.open("st2/" + n)); } catch (IOException e) { return null; } }   // 모드(st2mod) 우선
    public int[] decodePng(byte[] d, int[] out) {
        Bitmap b = BitmapFactory.decodeByteArray(d, 0, d.length); if (b == null) return null;
        out[0] = b.getWidth(); out[1] = b.getHeight(); int[] px = new int[out[0] * out[1]]; b.getPixels(px, 0, out[0], 0, 0, out[0], out[1]); b.recycle(); return px;
    }
    final Paint hdPaint = new Paint(Paint.ANTI_ALIAS_FLAG); boolean hdOk;
    public byte[] renderTextHd(String s, int targetW, int[] dims) {
        if (!hdOk || targetW <= 0 || s.length() == 0) return null;
        int K = com.seoultycoon.st2.Surf.K, h = 14 * K, w = targetW + K;
        hdPaint.setTextSize(11.8f * K); hdPaint.setLetterSpacing(0f); float sw = hdPaint.measureText(s); if (sw <= 0) return null;
        if (sw > targetW) { hdPaint.setTextSize(11.8f * K * targetW / sw); sw = targetW; } else if (s.length() > 1) hdPaint.setLetterSpacing(Math.min(1.2f * K, (targetW - sw) / (s.length() - 1)) / hdPaint.getTextSize());
        Bitmap b = Bitmap.createBitmap(w, h, Bitmap.Config.ARGB_8888); Canvas c = new Canvas(b); c.drawText(s, 0, 11 * K - 1, hdPaint);
        int[] px = new int[w * h]; b.getPixels(px, 0, w, 0, 0, w, h); b.recycle();
        byte[] r = new byte[w * h]; for (int i = 0; i < r.length; i++) r[i] = (byte) (px[i] >>> 24); dims[0] = w; dims[1] = h; return r;
    }
    public byte[] renderText(String s, int[] dims) {
        int w = Math.max(1, (int) Math.ceil(paint.measureText(s)) + 1), h = 14;
        Bitmap b = Bitmap.createBitmap(w, h, Bitmap.Config.ARGB_8888); Canvas c = new Canvas(b); c.drawText(s, 0, 11, paint);
        int[] px = new int[w * h]; b.getPixels(px, 0, w, 0, 0, w, h); b.recycle();
        byte[] r = new byte[w * h]; for (int i = 0; i < r.length; i++) r[i] = (byte) ((px[i] >>> 24) >= 128 ? 255 : 0);
        dims[0] = w; dims[1] = h; return r;
    }
    byte[] pakCache;     // 건물·지형 그림 묶음(약 1MB)은 게임이 그림을 부를 때마다 통째로 읽으므로 한 번만 읽어 둔다(반복해서 읽다가 메모리가 모자라 멈추던 문제)
    public byte[] readFile(String n) {
        if (n.equals("data1.pak") && pakCache != null) return pakCache;
        if (n.equals("data1.pak")) try { pakCache = all(am.open("st2mod/" + n)); return pakCache; } catch (IOException e0) { }   // 모드 건물·지형 그림 묶음 우선
        File f = new File(dir, n);
        try { if (f.exists()) return all(new FileInputStream(f)); } catch (IOException e) { }
        try { return all(am.open("st2/" + n)); } catch (IOException e) { return null; }       // data1.pak 처럼 처음엔 assets 에서
    }
    public void writeFile(String n, byte[] d) { try { FileOutputStream o = new FileOutputStream(new File(dir, n)); o.write(d); o.close(); } catch (IOException e) { } }
    public boolean fileExists(String n) {
        if (new File(dir, n).exists()) return true;
        if (n.equals("data1.pak")) try { am.open("st2mod/" + n).close(); return true; } catch (IOException e0) { }
        try { am.open("st2/" + n).close(); return n.equals("data1.pak"); } catch (IOException e) { return false; }
    }
    public void log(String s) { android.util.Log.d("st2", s); }
    final java.util.HashMap<Integer, MediaPlayer> players = new java.util.HashMap<>();
    public synchronized void playClip(int id, byte[] midi, boolean loop) {
        stopClip(id);
        try {
            File f = new File(ctx.getCacheDir(), "snd" + (seq++ & 15) + ".mid"); FileOutputStream o = new FileOutputStream(f); o.write(midi); o.close();
            MediaPlayer m = new MediaPlayer(); m.setDataSource(f.getAbsolutePath()); m.setLooping(loop); float v = vol / 100f; m.setVolume(v, v); m.prepare(); m.start(); players.put(id, m);
        } catch (Exception e) { log("clip " + e); }
    }
    public synchronized void stopClip(int id) { MediaPlayer m = players.remove(id); try { if (m != null) { m.stop(); m.release(); } } catch (Exception e) { } }
    public synchronized void setClipVolume(int v) { vol = Math.max(0, Math.min(100, v * 5)); for (MediaPlayer m : players.values()) try { m.setVolume(vol / 100f, vol / 100f); } catch (Exception e) { } }
    public synchronized void pause() { for (MediaPlayer m : players.values()) try { m.pause(); } catch (Exception e) { } }
    public synchronized void resume() { for (MediaPlayer m : players.values()) try { m.start(); } catch (Exception e) { } }
}
