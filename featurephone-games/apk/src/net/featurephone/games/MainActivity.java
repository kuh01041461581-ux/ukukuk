package net.featurephone.games;

import android.app.Activity;
import android.content.res.AssetManager;
import android.graphics.Color;
import android.os.Bundle;
import android.view.View;
import android.view.WindowManager;
import android.webkit.ValueCallback;
import android.webkit.WebChromeClient;
import android.webkit.WebResourceRequest;
import android.webkit.WebResourceResponse;
import android.webkit.WebSettings;
import android.webkit.WebView;
import android.webkit.WebViewClient;

import java.io.IOException;
import java.io.InputStream;
import java.util.HashMap;
import java.util.Map;

/**
 * 피처폰 게임(WIE 에뮬레이터) 웹 앱을 감싸는 안드로이드 화면.
 * 모든 파일은 APK 안의 assets 에서 가상 주소(https://appassets.androidplatform.net)로 제공하며,
 * 게임 저장은 WebView 의 IndexedDB(앱 전용 저장소)에 보관되어 앱을 꺼도 유지된다.
 */
public class MainActivity extends Activity {
    private static final String HOST = "appassets.androidplatform.net";
    private WebView webView;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);

        webView = new WebView(this);
        webView.setBackgroundColor(Color.BLACK);
        setContentView(webView);

        WebSettings s = webView.getSettings();
        s.setJavaScriptEnabled(true);
        s.setDomStorageEnabled(true);
        s.setDatabaseEnabled(true);
        s.setAllowFileAccess(false);
        s.setAllowContentAccess(false);
        s.setMediaPlaybackRequiresUserGesture(false);
        s.setCacheMode(WebSettings.LOAD_DEFAULT);
        s.setSupportZoom(false);
        s.setBuiltInZoomControls(false);

        webView.setWebChromeClient(new WebChromeClient());
        webView.setWebViewClient(new WebViewClient() {
            @Override
            public WebResourceResponse shouldInterceptRequest(WebView view, WebResourceRequest request) {
                if (!HOST.equals(request.getUrl().getHost())) {
                    return emptyResponse(403);   // 외부 인터넷 접속 차단
                }
                return serveAsset(request.getUrl().getPath());
            }
        });

        if (savedInstanceState != null) {
            webView.restoreState(savedInstanceState);
        } else {
            webView.loadUrl("https://" + HOST + "/index.html");
        }
    }

    private static final Map<String, String> MIME = new HashMap<String, String>();
    static {
        MIME.put("html", "text/html");
        MIME.put("js", "text/javascript");
        MIME.put("mjs", "text/javascript");
        MIME.put("css", "text/css");
        MIME.put("json", "application/json");
        MIME.put("wasm", "application/wasm");
        MIME.put("png", "image/png");
        MIME.put("jpg", "image/jpeg");
        MIME.put("svg", "image/svg+xml");
        MIME.put("ico", "image/x-icon");
        MIME.put("woff", "font/woff");
        MIME.put("woff2", "font/woff2");
        MIME.put("ttf", "font/ttf");
        MIME.put("otf", "font/otf");
        MIME.put("zip", "application/zip");
        MIME.put("jar", "application/java-archive");
        MIME.put("sf2", "application/octet-stream");
        MIME.put("map", "application/json");
    }

    private WebResourceResponse emptyResponse(int code) {
        return new WebResourceResponse("text/plain", "utf-8", code, "Error", new HashMap<String, String>(),
                new java.io.ByteArrayInputStream(new byte[0]));
    }

    private WebResourceResponse serveAsset(String path) {
        if (path == null || path.equals("/") || path.length() == 0) path = "/index.html";
        String name = path.substring(1);
        if (name.contains("..")) return emptyResponse(403);
        try {
            AssetManager am = getAssets();
            InputStream in = am.open("www/" + name, AssetManager.ACCESS_STREAMING);
            int dot = name.lastIndexOf('.');
            String ext = dot >= 0 ? name.substring(dot + 1).toLowerCase() : "";
            String mime = MIME.containsKey(ext) ? MIME.get(ext) : "application/octet-stream";
            Map<String, String> headers = new HashMap<String, String>();
            headers.put("Cache-Control", "no-cache");
            boolean text = mime.startsWith("text/") || mime.equals("application/json");
            return new WebResourceResponse(mime, text ? "utf-8" : null, 200, "OK", headers, in);
        } catch (IOException e) {
            return emptyResponse(404);
        }
    }

    @Override
    public void onBackPressed() {
        // 게임 화면이면 라이브러리로, 라이브러리면 앱 종료
        webView.evaluateJavascript(
            "(function(){var p=document.getElementById('player-view');" +
            "if(p&&!p.hidden){document.getElementById('back-to-library').click();return 'back';}return 'exit';})()",
            new ValueCallback<String>() {
                @Override
                public void onReceiveValue(String value) {
                    if (value != null && value.contains("exit")) finish();
                }
            });
    }

    @Override
    protected void onSaveInstanceState(Bundle outState) {
        super.onSaveInstanceState(outState);
        webView.saveState(outState);
    }

    @Override
    protected void onPause() {
        super.onPause();
        webView.onPause();
    }

    @Override
    protected void onResume() {
        super.onResume();
        webView.onResume();
    }

    @Override
    protected void onDestroy() {
        webView.destroy();
        super.onDestroy();
    }
}
