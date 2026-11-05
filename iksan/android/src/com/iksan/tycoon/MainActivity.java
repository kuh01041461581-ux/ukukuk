package com.iksan.tycoon;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.DialogInterface;
import android.os.Bundle;
import android.view.View;
import android.view.WindowManager;
import android.webkit.*;
import java.io.InputStream;

/** 웹 게임(assets/www)을 가상 출처 https://app.local/ 로 서빙하는 WebView 래퍼. ES 모듈·fetch 가 file:// 에서 막히므로 요청을 가로채 assets 에서 응답한다. */
public class MainActivity extends Activity {
    WebView wv;
    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        wv = new WebView(this); setContentView(wv);
        wv.setSystemUiVisibility(View.SYSTEM_UI_FLAG_HIDE_NAVIGATION | View.SYSTEM_UI_FLAG_FULLSCREEN | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY);
        WebSettings s = wv.getSettings(); s.setJavaScriptEnabled(true); s.setDomStorageEnabled(true); s.setAllowFileAccess(false);
        wv.setBackgroundColor(0xFF10141C);
        wv.setWebChromeClient(new WebChromeClient() {
            @Override public boolean onJsAlert(WebView v, String url, String msg, final JsResult r) {
                new AlertDialog.Builder(MainActivity.this).setMessage(msg).setCancelable(false).setPositiveButton("확인", new DialogInterface.OnClickListener() { public void onClick(DialogInterface d, int w) { r.confirm(); } }).show(); return true; } });
        wv.setWebViewClient(new WebViewClient() {
            @Override public WebResourceResponse shouldInterceptRequest(WebView v, WebResourceRequest req) {
                if (!"app.local".equals(req.getUrl().getHost())) return super.shouldInterceptRequest(v, req);
                String path = req.getUrl().getPath(); if (path == null || path.equals("/")) path = "/web/index.html";
                try { InputStream in = getAssets().open("www" + path); return new WebResourceResponse(mime(path), "UTF-8", in); }
                catch (Exception e) { return new WebResourceResponse("text/plain", "UTF-8", 404, "Not Found", null, new java.io.ByteArrayInputStream(new byte[0])); } }
        });
        wv.loadUrl("https://app.local/web/index.html");
    }
    static String mime(String p) {
        if (p.endsWith(".html")) return "text/html"; if (p.endsWith(".js") || p.endsWith(".mjs")) return "application/javascript"; if (p.endsWith(".json")) return "application/json";
        if (p.endsWith(".ttf")) return "font/ttf"; if (p.endsWith(".css")) return "text/css"; if (p.endsWith(".png")) return "image/png"; return "application/octet-stream";
    }
    @Override public void onBackPressed() { if (wv.canGoBack()) wv.goBack(); else super.onBackPressed(); }
    @Override protected void onPause() { super.onPause(); wv.onPause(); }
    @Override protected void onResume() { super.onResume(); wv.onResume(); }
}
