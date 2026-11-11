//! 게임 한 판을 돌리는 비동기 작업: 시작 순서를 실행하고, 이후 호스트가 보내는 명령(프레임·터치)을 처리한다.

use std::sync::Arc;

use futures::{StreamExt, channel::{mpsc::UnboundedReceiver, oneshot}};
use spin::Mutex;
use wie_backend::Options;
use wie_core_arm::{Allocator, ArmCore};
use wie_util::{Result, WieError};

use crate::{
    hle::{self, JObj, State},
    loader,
};

pub enum Cmd {
    /// 한 프레임 진행 (현재 시각 ms)
    Frame { now_ms: u64, done: oneshot::Sender<Result<()>> },
    /// 터치: 종류(19 누름, 20 뗌, 21 움직임), x, y, 손가락 번호
    Touch { kind: u32, x: i32, y: i32, id: i32 },
    /// 키: 동작(2 누름, 3 뗌), WIPI 키 코드
    Key { action: u32, code: u32 },
    Pause,
    Resume,
}

pub struct Config {
    pub lcd_width: u32,
    pub lcd_height: u32,
    /// ARM → 웹어셈블리 변환(AOT) 실행기 (웹에서만 제공). None 이면 해석기로 실행
    pub aot: Option<Box<dyn wie_core_arm_aot::CompiledExecutor>>,
    /// 넥슨(BNV) 계열 게임의 JNI 이름 접두사. 예: "Java_com_Nexon_Cygnus". 비어 있으면 이아모바일(CletEntry) 계열
    pub bnv: String,
}

pub type Shared = Arc<Mutex<State>>;

pub async fn run_game(so: Vec<u8>, state: Shared, mut config: Config, mut rx: UnboundedReceiver<Cmd>, ready: oneshot::Sender<Result<()>>) -> Result<()> {
    let setup = (|| -> Result<_> {
        let mut core = ArmCore::new(Options { enable_gdbserver: false, aot: config.aot.take(), profile: None })?;
        Allocator::init(&mut core)?;
        hle::register(&mut core, &state)?;
        let lib = loader::load(&mut core, &state, &so)?;
        let env_ptr = hle::make_jni_env(&mut core, &state)?;
        Ok((core, lib, env_ptr))
    })();
    let (mut core, lib, env_ptr) = match setup {
        Ok(v) => v,
        Err(error) => {
            let _ = ready.send(Err(WieError::FatalError(format!("{error}"))));
            return Ok(());
        }
    };

    // ARM → 웹어셈블리 변환(AOT) 준비: 올려 둔 코드 전체를 한 번 변환한다 (이후 실행이 훨씬 빠름)
    if let Err(error) = core.prepare_execution().await {
        let _ = ready.send(Err(error));
        return Ok(());
    }

    if !config.bnv.is_empty() {
        return run_bnv(core, lib, env_ptr, state, config, rx, ready).await;
    }
    let find = |name: &str| -> Result<u32> {
        lib.exports
            .get(&format!("Java_co_kr_eamobile_CletEntry_{name}"))
            .copied()
            .ok_or_else(|| WieError::FatalError(format!("{name} 없음")))
    };
    let (init_kernel, init_vfs, init_storage, init_audio, init_display, start_app, native_loop) = (
        find("nativeInitKernel")?,
        find("nativeInitVFS")?,
        find("nativeInitFileStorage")?,
        find("nativeInitAudioConfiguration")?,
        find("nativeInitDisplay")?,
        find("nativeStartApp")?,
        find("nativeLoop")?,
    );
    let result_mtx = lib.exports.get("Java_co_kr_eamobile_mtx_CarrierFuncActivity_nativeResultMTX").copied();
    let result_fsad = lib.exports.get("Java_co_kr_eamobile_mtx_CarrierFuncActivity_nativeResultFSAD").copied();
    let (touch, key, pause, resume) = (
        find("nativeTouchHandleEvent")?,
        find("nativeKeyHandleEvent")?,
        find("nativePauseApp")?,
        find("nativeResumeApp")?,
    );

    // 자바 객체 흉내
    let fd_obj = hle::new_object(&state, JObj::FileDescriptor(100));
    let vfs_len = state.lock().vfs.len() as u32;
    let path_internal = hle::new_object(&state, JObj::Str("/data/data/game/files".into()));
    let path_external = hle::new_object(&state, JObj::Str("/sdcard/game".into()));

    let mut core2 = core.clone();
    let st2 = state.clone();
    let thread = core.run_in_thread(move || async move {
        let setup = async {
            let _: u32 = core2.run_function(init_kernel, &[env_ptr, 0]).await?;
            let _: u32 = core2.run_function(init_vfs, &[env_ptr, 0, fd_obj, 0, 0, 0, vfs_len, 0]).await?;
            let _: u32 = core2.run_function(init_storage, &[env_ptr, 0, path_internal, path_external]).await?;
            let _: u32 = core2.run_function(init_audio, &[env_ptr, 0, 202400, 0]).await?;
            let mag = 1.0f32.to_bits();
            let display: [u32; 16] = [config.lcd_width, config.lcd_height, 8, 8, 8, 8, config.lcd_width, config.lcd_height, 0, 0, 0, mag, 0, 0, 0, 0];
            let mut args = vec![env_ptr, 0];
            args.extend(display);
            let _: u32 = core2.run_function(init_display, &args).await?;
            let _: u32 = core2.run_function(start_app, &[env_ptr, 0]).await?;
            Result::Ok(())
        };
        match setup.await {
            Ok(()) => {
                let _ = ready.send(Ok(()));
            }
            Err(error) => {
                let message = format!("{error:?}\n{}", core2.dump_reg_stack(loader::BASE));
                let _ = ready.send(Err(WieError::FatalError(message)));
                return Ok(());
            }
        }

        while let Some(cmd) = rx.next().await {
            match cmd {
                Cmd::Frame { now_ms, done } => {
                    {
                        let mut st = st2.lock();
                        st.now_ms = now_ms;
                        // 빨리 넘기는 구간에서는 프레임마다 게임 시계를 150ms 씩 앞당긴다
                        if now_ms < st.fast_forward_until_ms {
                            st.time_skew_ms += 150;
                        }
                    }
                    // 결제(MTX) 요청에 대한 응답을 게임에 전달
                    let pending: Vec<_> = std::mem::take(&mut st2.lock().pending_mtx);
                    let pending_ads: Vec<_> = std::mem::take(&mut st2.lock().pending_fsad);
                    if let Some(f) = result_fsad {
                        for (result, code) in pending_ads {
                            let _ = core2.run_function::<u32>(f, &[env_ptr, 0, result as u32, code as u32]).await;
                        }
                    }
                    if let Some(f) = result_mtx {
                        for (content, code, sub) in pending {
                            let handle = hle::new_object(&st2, JObj::Str(content));
                            let _ = core2.run_function::<u32>(f, &[env_ptr, 0, handle, code as u32, sub as u32]).await;
                        }
                    }
                    let result = core2.run_function::<u32>(native_loop, &[env_ptr, 0]).await.map(|_| ());
                    let _ = done.send(result);
                }
                Cmd::Touch { kind, x, y, id } => {
                    let _ = core2.run_function::<u32>(touch, &[env_ptr, 0, kind, x as u32, y as u32, id as u32]).await;
                }
                Cmd::Key { action, code } => {
                    let _ = core2.run_function::<u32>(key, &[env_ptr, 0, action, code, 0]).await;
                }
                Cmd::Pause => {
                    let _ = core2.run_function::<u32>(pause, &[env_ptr, 0]).await;
                }
                Cmd::Resume => {
                    let _ = core2.run_function::<u32>(resume, &[env_ptr, 0]).await;
                }
            }
        }
        Ok(())
    })?;
    thread.await
}

async fn run_bnv(mut core: ArmCore, lib: loader::Library, env_ptr: u32, state: Shared, config: Config, mut rx: UnboundedReceiver<Cmd>, ready: oneshot::Sender<Result<()>>) -> Result<()> {
    let find = |name: &str| -> Result<u32> {
        lib.exports.get(&format!("{}_NativeClass_{name}", config.bnv)).copied().ok_or_else(|| WieError::FatalError(format!("{name} 없음")))
    };
    let (vfs_init, path, size, init, frame) = (find("OnVFSInitialize")?, find("OnNativePath")?, find("OnNativeSize")?, find("OnNativeInitialize")?, find("OnNativeFrame")?);
    let init_first = lib.exports.contains_key(&format!("{}_NativeClass_OnNativeCreateRenderer", config.bnv));
    let (press, release, moved, pause, resume) = (find("OnNativeTouchPress")?, find("OnNativeTouchRelease")?, find("OnNativeTouchMove")?, find("OnNativePause")?, find("OnNativeResume")?);
    let fd_obj = hle::new_object(&state, JObj::FileDescriptor(100));
    let vfs_len = state.lock().vfs.len() as u32;
    let path_obj = hle::new_object(&state, JObj::Str("/data/data/game/files".into()));
    let inits = lib.init_functions.clone();
    let (w, h) = (config.lcd_width, config.lcd_height);
    let mut core2 = core.clone();
    let st2 = state.clone();
    let thread = core.run_in_thread(move || async move {
        let setup = async {
            for f in &inits {
                let _: u32 = core2.run_function(*f, &[]).await?;
            }
            let _: u32 = core2.run_function(vfs_init, &[env_ptr, 0, fd_obj, 0, 0, 0, vfs_len, 0]).await?;
            let _: u32 = core2.run_function(path, &[env_ptr, 0, path_obj]).await?;
            if init_first {
                let _: u32 = core2.run_function(init, &[env_ptr, 0]).await?;
                let _: u32 = core2.run_function(size, &[env_ptr, 0, w, h]).await?;
            } else {
                let _: u32 = core2.run_function(size, &[env_ptr, 0, w, h]).await?;
                let _: u32 = core2.run_function(init, &[env_ptr, 0]).await?;
            }
            Result::Ok(())
        };
        match setup.await {
            Ok(()) => {
                let _ = ready.send(Ok(()));
            }
            Err(error) => {
                let message = format!("{error:?}\n{}", core2.dump_reg_stack(loader::BASE));
                let _ = ready.send(Err(WieError::FatalError(message)));
                return Ok(());
            }
        }
        while let Some(cmd) = rx.next().await {
            match cmd {
                Cmd::Frame { now_ms, done } => {
                    st2.lock().now_ms = now_ms;
                    let result = core2.run_function::<u32>(frame, &[env_ptr, 0]).await.map(|_| ());
                    let _ = done.send(result);
                }
                Cmd::Touch { kind, x, y, id } => {
                    let f = match kind { 19 => press, 20 => release, _ => moved };
                    let _ = core2.run_function::<u32>(f, &[env_ptr, 0, 1, id as u32, x as u32, y as u32]).await;
                }
                Cmd::Key { .. } => {}
                Cmd::Pause => {
                    let _ = core2.run_function::<u32>(pause, &[env_ptr, 0]).await;
                }
                Cmd::Resume => {
                    let _ = core2.run_function::<u32>(resume, &[env_ptr, 0]).await;
                }
            }
        }
        Ok(())
    })?;
    thread.await
}
