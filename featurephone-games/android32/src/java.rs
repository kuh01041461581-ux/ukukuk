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
        "isNetworkAvailable" | "isNetworkRoaming" | "isAirplaneModeOn" | "isFBOSupported" => JValue::Int(0),
        // ---- 볼륨 ----
        "getVolume" | "getMusicVolume" | "getSoundVolume" => JValue::Int(10),
        "setVolume" | "setMusicVolume" | "setSoundVolume" => JValue::Void,
        // ---- 진동 / 종료 / 화면 ----
        "vibration" | "exitApp" | "setWIPIDisplayInfo" => {
            eprintln!("[java] {name}({:?})", args);
            if sig.ends_with('I') { JValue::Int(0) } else { JValue::Void }
        }
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
        // ---- 소리(아직 소리는 내지 않음) ----
        n if n.starts_with("clip") || n.starts_with("put") || n.contains("BufferID") || n == "getClipStatus" => {
            eprintln!("[java] {name}({:?})", args.iter().map(int).collect::<Vec<_>>());
            if sig.ends_with('V') { JValue::Void } else { JValue::Int(0) }
        }
        // ---- 결제/광고/전화/웹 ----
        "initMTX" | "requestMTX" | "startFullScreenAd" | "loadWebpage" | "phnCallPlace" => JValue::Int(-1),
        other => {
            eprintln!("[java] 구현되지 않은 자바 메서드 {other}{sig}");
            if sig.ends_with('V') { JValue::Void } else if sig.ends_with('J') { JValue::Long(0) } else { JValue::Int(0) }
        }
    };
    Ok(result)
}
