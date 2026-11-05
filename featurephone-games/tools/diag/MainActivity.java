package net.featurephone.games;

import android.app.Activity;
import android.graphics.Color;
import android.os.Build;
import android.os.Bundle;
import android.view.Gravity;
import android.widget.ScrollView;
import android.widget.TextView;

/** 기기확인: 이 폰이 32비트 안드로이드 앱(armeabi)을 설치할 수 있는지 보여준다. */
public class MainActivity extends Activity {
    private static String join(String[] a) {
        StringBuilder sb = new StringBuilder();
        if (a == null || a.length == 0) return "(없음)";
        for (int i = 0; i < a.length; i++) { if (i > 0) sb.append(", "); sb.append(a[i]); }
        return sb.toString();
    }

    @Override
    protected void onCreate(Bundle b) {
        super.onCreate(b);
        String[] abis = Build.SUPPORTED_ABIS, a32 = Build.SUPPORTED_32_BIT_ABIS, a64 = Build.SUPPORTED_64_BIT_ABIS;
        boolean has32 = false;
        for (int i = 0; i < a32.length; i++) if (a32[i].startsWith("armeabi")) has32 = true;
        boolean sdkOk = Build.VERSION.SDK_INT < 35;   // 안드로이드 15 부터는 대상 버전 24 미만 앱 설치를 막음

        StringBuilder t = new StringBuilder();
        t.append("기기: ").append(Build.MANUFACTURER).append(" ").append(Build.MODEL).append("\n\n");
        t.append("안드로이드: ").append(Build.VERSION.RELEASE).append("  (API ").append(Build.VERSION.SDK_INT).append(")\n\n");
        t.append("지원하는 CPU 종류\n  전체: ").append(join(abis)).append("\n  32비트: ").append(join(a32)).append("\n  64비트: ").append(join(a64)).append("\n\n");
        t.append("검은방·메이플 안드로이드판(32비트 armeabi 전용)\n");
        if (has32) t.append("  → 이 폰은 32비트 ARM 을 지원합니다. CPU 때문에 설치가 막히지는 않습니다.\n");
        else t.append("  → 이 폰은 64비트 전용입니다. 이 앱들은 설치할 수 없습니다.\n    (재서명으로 해결되지 않습니다. 구형 폰, 또는 PC 의 안드로이드 에뮬레이터를 쓰세요.)\n");
        t.append("\n안드로이드 버전: ").append(sdkOk ? "대상 버전 24 이상이면 설치 가능한 범위입니다." : "안드로이드 15 이상: 대상 버전 24 미만 앱은 설치가 막힙니다.").append("\n");
        t.append("\n이 화면을 캡처해서 보내주세요.");

        TextView tv = new TextView(this);
        tv.setText(t.toString()); tv.setTextSize(16); tv.setTextColor(Color.WHITE); tv.setPadding(40, 60, 40, 40);
        tv.setTextIsSelectable(true); tv.setGravity(Gravity.START);
        ScrollView sv = new ScrollView(this); sv.setBackgroundColor(Color.parseColor("#111111")); sv.addView(tv);
        setContentView(sv);
    }
}
