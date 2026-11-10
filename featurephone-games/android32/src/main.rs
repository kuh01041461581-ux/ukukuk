//! 32비트 ARM(armeabi) 안드로이드 네이티브 게임 라이브러리(.so)를 WIE 의 ARM 실행기 위에서 돌려 보는 실험 프로그램.
//! 1단계: ELF 를 올리고 → 재배치(시스템 함수는 호스트 구현으로 연결) → JNI 진입 함수를 호출한다.


use std::{env, fs, sync::Arc};

use spin::Mutex;
use wie_backend::Options;
use wie_core_arm::{Allocator, ArmCore};
use wie_util::Result;

use wie_android::{hle, hle::State, loader};

fn main() -> anyhow::Result<()> {
    tracing_subscriber::fmt().with_env_filter(tracing_subscriber::EnvFilter::from_default_env()).with_writer(std::io::stderr).init();

    let args: Vec<String> = env::args().collect();
    let so_path = args.get(1).expect("사용법: wie-android <lib.so> [data.vfs]");
    let so = fs::read(so_path)?;
    let vfs = args.get(2).map(fs::read).transpose()?.unwrap_or_default();

    let state = Arc::new(Mutex::new(State::new(vfs)));
    let result = futures::executor::block_on(run(so, state.clone()));
    if let Err(error) = &result {
        eprintln!("실행 오류: {error:?}");
    }
    let state = state.lock();
    eprintln!("--- 로그 {}줄, GL 호출 {}회", state.log.len(), state.gl_calls);
    Ok(result?)
}

async fn run(so: Vec<u8>, state: Arc<Mutex<State>>) -> Result<()> {
    let mut core = ArmCore::new(Options {
        enable_gdbserver: false,
        aot: None,
        profile: None,
    })?;
    Allocator::init(&mut core)?;
    hle::register(&mut core, &state)?;

    let lib = loader::load(&mut core, &state, &so)?;
    eprintln!("올림: 기준주소 {:#x}, 내보낸 함수 {}개", lib.base, lib.exports.len());

    let env_ptr = hle::make_jni_env(&mut core, &state)?;
    let find = |name: &str| *lib.exports.get(&format!("Java_co_kr_eamobile_CletEntry_{name}")).unwrap_or_else(|| panic!("{name} 없음"));
    let (init_kernel, init_vfs, init_storage, init_audio, init_display, start_app, native_loop) = (
        find("nativeInitKernel"),
        find("nativeInitVFS"),
        find("nativeInitFileStorage"),
        find("nativeInitAudioConfiguration"),
        find("nativeInitDisplay"),
        find("nativeStartApp"),
        find("nativeLoop"),
    );

    // 자바 객체 흉내: FileDescriptor(가상 파일 번호 100), 경로 문자열
    let fd_obj = hle::new_object(&state, hle::JObj::FileDescriptor(100));
    let vfs_len = state.lock().vfs.len() as u32;
    let path_internal = hle::new_object(&state, hle::JObj::Str("/data/data/game/files".into()));
    let path_external = hle::new_object(&state, hle::JObj::Str("/sdcard/game".into()));

    let mut core2 = core.clone();
    let st2 = state.clone();
    let thread = core.run_in_thread(move || async move {
        let _ = &st2;
        let r: u32 = core2.run_function(init_kernel, &[env_ptr, 0]).await?;
        eprintln!("nativeInitKernel -> {r:#x}");
        // nativeInitVFS(env, obj, FileDescriptor, long offset, long length): long 은 r2:r3 / 스택
        let _: u32 = core2.run_function(init_vfs, &[env_ptr, 0, fd_obj, 0 /* r3: 64비트 정렬용 빈칸 */, 0, 0 /* offset */, vfs_len, 0 /* length */]).await?;
        eprintln!("nativeInitVFS 완료");
        let _: u32 = core2.run_function(init_storage, &[env_ptr, 0, path_internal, path_external]).await?;
        eprintln!("nativeInitFileStorage 완료");
        let _: u32 = core2.run_function(init_audio, &[env_ptr, 0, 202400, 0]).await?;
        eprintln!("nativeInitAudioConfiguration 완료");
        // nativeInitDisplay(screenW, screenH, r, g, b, a, lcdW, lcdH, marginX, marginY, colorType, magnification(float), magFilter, ...)
        let mag = 1.0f32.to_bits();
        // 16개 인자: screenW, screenH, r, g, b, a, lcdW, lcdH, marginX, marginY, colorType, magnification(float), magFilter, 0, 0, 0
        let display: [u32; 16] = [240, 400, 8, 8, 8, 8, 240, 400, 0, 0, 0, mag, 0, 0, 0, 0];
        let mut display_args = vec![env_ptr, 0];
        display_args.extend(display);
        let _: u32 = core2.run_function(init_display, &display_args).await?;
        eprintln!("nativeInitDisplay 완료");
        match core2.run_function::<u32>(start_app, &[env_ptr, 0]).await {
            Ok(_) => eprintln!("nativeStartApp 완료"),
            Err(error) => {
                eprintln!("nativeStartApp 실패: {error:?}\n{}", core2.dump_reg_stack(0x1000_0000));
                eprintln!("--- 직전 시스템 함수 호출 ---");
                for line in st2.lock().trace.iter() {
                    eprintln!("  {line}");
                }
                return Err(error);
            }
        }
        let frames: usize = std::env::var("FRAMES").ok().and_then(|v| v.parse().ok()).unwrap_or(5);
        let dump_dir = std::env::var("DUMP").ok();
        for frame in 0..frames {
            st2.lock().now_ms += 33; // 약 30fps
            let _: u32 = core2.run_function(native_loop, &[env_ptr, 0]).await?;
            if let Some(dir) = &dump_dir {
                let st = st2.lock();
                if st.gl.width > 0 && (frame % 10 == 0 || frame + 1 == frames) {
                    let mut out = format!("P6\n{} {}\n255\n", st.gl.width, st.gl.height).into_bytes();
                    for px in st.gl.frame.chunks_exact(4) {
                        out.extend_from_slice(&px[..3]);
                    }
                    let _ = std::fs::write(format!("{dir}/f{frame:04}.ppm"), out);
                }
            }
        }
        eprintln!("프레임 {frames}개 완료");
        Ok(())
    })?;
    thread.await
}
