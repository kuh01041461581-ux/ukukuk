//! 원래 안드로이드 자바 쪽(`CletEntry` 등)이 네이티브에 제공하던 기능의 호스트 구현.
//! 네이티브 코드는 JNI 로 이름을 찾아 이 함수들을 부른다.

use wie_core_arm::ArmCore;
use wie_util::Result;

use crate::hle::{JObj, Shared, new_object};

#[derive(Clone, Debug)]
pub enum JValue {
    Int(i32),
    Long(i64),
    Obj(u32),
    Void,
}

fn string_arg(state: &Shared, v: &JValue) -> String {
    if let JValue::Obj(h) = v {
        if let Some(JObj::Str(s)) = state.lock().objects.get((*h as usize).wrapping_sub(1)) {
            return s.clone();
        }
    }
    String::new()
}

fn long(v: &JValue) -> i64 {
    match v {
        JValue::Int(i) => *i as i64,
        JValue::Long(l) => *l,
        JValue::Obj(o) => *o as i64,
        JValue::Void => 0,
    }
}

fn int(v: &JValue) -> i32 {
    match v {
        JValue::Int(i) => *i,
        JValue::Long(l) => *l as i32,
        JValue::Obj(o) => *o as i32,
        JValue::Void => 0,
    }
}

pub fn call(_core: &mut ArmCore, state: &Shared, name: &str, sig: &str, args: &[JValue]) -> Result<JValue> {
    *state.lock().java_calls.entry(name.to_string()).or_insert(0) += 1;
    let s = |text: &str| JValue::Obj(new_object(state, JObj::Str(text.to_string())));
    let result = match name {
        // ---- 기기 정보 ----
        "getIMEI" | "getIMSI" | "getMin" => s("000000000000000"),
        "getManufacturer" => s("samsung"),
        "getModel" => s("SM-S926N"),
        "getOSVersion" => JValue::Int(34),
        "getOpenGLESVersion" => s("OpenGL ES-CM 1.1"),
        "freeMemory" => JValue::Int(256 * 1024 * 1024),
        "totalMemory" => JValue::Int(512 * 1024 * 1024),
        "getAvailableExternalMemorySize" | "getAvailableInternalMemorySize" => JValue::Long(8 << 30),
        "getTotalExternalMemorySize" | "getTotalInternalMemorySize" => JValue::Long(64 << 30),
        "isNetworkAvailable" => {
            // 네트워크가 없다고 답하면 게임이 "접속중" 화면을 한참 보여 준다: 그동안 호스트가 게임을 빠르게 돌려 준다
            let mut st = state.lock();
            st.fast_forward_until_ms = st.now_ms + 15_000;
            JValue::Int(0)
        }
        "isNetworkRoaming" | "isAirplaneModeOn" | "isFBOSupported" => JValue::Int(0),
        // ---- 볼륨 ----
        "getVolume" | "getMusicVolume" | "getSoundVolume" => JValue::Int(10),
        "setVolume" | "setMusicVolume" | "setSoundVolume" => JValue::Void,
        // ---- 진동 / 종료 / 화면 ----
        "vibration" => {
            let ms = args.first().map(int).unwrap_or(0);
            state.lock().events.push(format!(r#"{{"t":"vibrate","ms":{ms}}}"#));
            JValue::Int(0)
        }
        "exitApp" => {
            state.lock().events.push(r#"{"t":"exit"}"#.to_string());
            JValue::Void
        }
        "setWIPIDisplayInfo" => JValue::Void,
        // ---- 에셋 ----
        "getAssetSize" => {
            eprintln!("[java] getAssetSize({})", string_arg(state, &args[0]));
            JValue::Int(-1)
        }
        "loadAsset" => {
            eprintln!("[java] loadAsset({:?})", args.iter().map(int).collect::<Vec<_>>());
            JValue::Int(-1)
        }
        "getUniqueNumberFromStrings" => {
            // 자바 원본(MidasAssetManager): 처음 요청된 순서대로 1, 2, 3 ... 번호를 부여한다
            let text = string_arg(state, &args[0]);
            let mut st = state.lock();
            let index = match st.asset_names.iter().position(|n| *n == text) {
                Some(i) => i,
                None => {
                    st.asset_names.push(text);
                    st.asset_names.len() - 1
                }
            };
            JValue::Int(index as i32 + 1)
        }
        "getUniqueAssetNameFromID" => {
            let id = int(&args[0]);
            let name = state.lock().asset_names.get((id as usize).wrapping_sub(1)).cloned().unwrap_or_default();
            s(&name)
        }
        // ---- 소리: 호스트(자바스크립트)가 재생한다. 이벤트로 전달 ----
        "putSoundClip" | "putMusicClip" => {
            // putSoundClip(id, offset, length, ?) / putMusicClip(id, offset, length): 데이터는 data.vfs 안의 구간
            let kind = if name == "putSoundClip" { "sound" } else { "music" };
            let v: Vec<i64> = args.iter().map(long).collect();
            state.lock().events.push(format!(r#"{{"t":"load","kind":"{kind}","id":{},"offset":{},"length":{}}}"#, v[0], v[1], v[2]));
            JValue::Int(0)
        }
        n if n.starts_with("clipPlay") || n.starts_with("clipStop") || n.starts_with("clipPause") || n.starts_with("clipResume") || n.starts_with("clipFree") => {
            let op = ["Play", "Stop", "Pause", "Resume", "Free"].iter().find(|o| n.starts_with(&format!("clip{o}"))).unwrap().to_lowercase();
            let kind = if n.ends_with("Music") { "music" } else if n.ends_with("Sound") { "sound" } else { "any" };
            let id = args.first().map(long).unwrap_or(0);
            let looped = args.get(1).map(int).unwrap_or(0) != 0;
            state.lock().events.push(format!(r#"{{"t":"{op}","kind":"{kind}","id":{id},"loop":{looped}}}"#));
            JValue::Int(0)
        }
        "getClipStatus" | "putClip" | "attachBufferIDtoResourceID" | "detachBufferIDfromResourceID" => {
            if sig.ends_with('V') { JValue::Void } else { JValue::Int(0) }
        }
        // ---- 결제/광고/전화/웹 ----
        "initMTX" => JValue::Int(0),
        "requestMTX" => {
            // 결제 서버(MTX)에는 접속할 수 없다: 요청 즉시 "실패" 결과(-1, -12)를 게임에 돌려준다
            let content = args.get(1).map(|a| string_arg(state, a)).unwrap_or_default();
            state.lock().pending_mtx.push((content, -1, -12));
            JValue::Int(0)
        }
        "startFullScreenAd" => {
            // 광고는 지원하지 않는다: 광고를 닫은(취소한) 것처럼 즉시 결과(-1)를 돌려준다
            state.lock().pending_fsad.push((-1, 0));
            JValue::Int(0)
        }
        "loadWebpage" | "phnCallPlace" => JValue::Int(-1),
        other => {
            eprintln!("[java] 구현되지 않은 자바 메서드 {other}{sig}");
            if sig.ends_with('V') { JValue::Void } else if sig.ends_with('J') { JValue::Long(0) } else { JValue::Int(0) }
        }
    };
    Ok(result)
}
