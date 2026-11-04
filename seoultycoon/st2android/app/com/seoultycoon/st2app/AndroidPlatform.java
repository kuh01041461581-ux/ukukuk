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
    volatile MediaPlayer mp; int vol = 100; int seq;
    public AndroidPlatform(Context c, Typeface tf) {
        ctx = c; am = c.getAssets(); dir = c.getFilesDir();
        paint.setTypeface(tf); paint.setTextSize(11); paint.setAntiAlias(false); paint.setColor(Color.WHITE);
    }
    static byte[] all(InputStream in) throws IOException {
        ByteArrayOutputStream o = new ByteArrayOutputStream(); byte[] b = new byte[16384]; int n; while ((n = in.read(b)) > 0) o.write(b, 0, n); in.close(); return o.toByteArray();
    }
    public byte[] resource(String n) { try { return all(am.open("st2/" + n)); } catch (IOException e) { return null; } }
    public int[] decodePng(byte[] d, int[] out) {
        Bitmap b = BitmapFactory.decodeByteArray(d, 0, d.length); if (b == null) return null;
        out[0] = b.getWidth(); out[1] = b.getHeight(); int[] px = new int[out[0] * out[1]]; b.getPixels(px, 0, out[0], 0, 0, out[0], out[1]); b.recycle(); return px;
    }
    public byte[] renderText(String s, int[] dims) {
        int w = Math.max(1, (int) Math.ceil(paint.measureText(s)) + 1), h = 14;
        Bitmap b = Bitmap.createBitmap(w, h, Bitmap.Config.ARGB_8888); Canvas c = new Canvas(b); c.drawText(s, 0, 11, paint);
        int[] px = new int[w * h]; b.getPixels(px, 0, w, 0, 0, w, h); b.recycle();
        byte[] r = new byte[w * h]; for (int i = 0; i < r.length; i++) r[i] = (byte) ((px[i] >>> 24) >= 128 ? 255 : 0);
        dims[0] = w; dims[1] = h; return r;
    }
    public byte[] readFile(String n) {
        File f = new File(dir, n);
        try { if (f.exists()) return all(new FileInputStream(f)); } catch (IOException e) { }
        try { return all(am.open("st2/" + n)); } catch (IOException e) { return null; }       // data1.pak 처럼 처음엔 assets 에서
    }
    public void writeFile(String n, byte[] d) { try { FileOutputStream o = new FileOutputStream(new File(dir, n)); o.write(d); o.close(); } catch (IOException e) { } }
    public boolean fileExists(String n) {
        if (new File(dir, n).exists()) return true;
        try { am.open("st2/" + n).close(); return n.equals("data1.pak"); } catch (IOException e) { return false; }
    }
    public void log(String s) { android.util.Log.d("st2", s); }
    public synchronized void playClip(byte[] midi, boolean loop) {
        stopClip();
        try {
            File f = new File(ctx.getCacheDir(), "bgm" + (seq++ & 3) + ".mid"); FileOutputStream o = new FileOutputStream(f); o.write(midi); o.close();
            MediaPlayer m = new MediaPlayer(); m.setDataSource(f.getAbsolutePath()); m.setLooping(loop); float v = vol / 100f; m.setVolume(v, v); m.prepare(); m.start(); mp = m;
        } catch (Exception e) { log("clip " + e); }
    }
    public synchronized void stopClip() { try { if (mp != null) { mp.stop(); mp.release(); } } catch (Exception e) { } mp = null; }
    public synchronized void setClipVolume(int v) { vol = Math.max(0, Math.min(100, v * 5)); try { if (mp != null) mp.setVolume(vol / 100f, vol / 100f); } catch (Exception e) { } }
    public void pause() { try { if (mp != null) mp.pause(); } catch (Exception e) { } }
    public void resume() { try { if (mp != null) mp.start(); } catch (Exception e) { } }
}
