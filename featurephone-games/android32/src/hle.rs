//! 안드로이드 시스템 라이브러리(bionic libc, libm, libGLESv1_CM, liblog, JNI)의 호스트 쪽 구현.

use std::{collections::BTreeMap, sync::Arc};

use spin::Mutex;
use wie_core_arm::{Allocator, ArmCore, ResultWriter, SvcId};
use wie_util::{ByteRead, ByteWrite, Result, WieError, read_generic, read_null_terminated_string_bytes, write_generic};

pub const CAT_LIBC: u32 = 1;
pub const CAT_JNI: u32 = 2;
pub const JNI_FUNCTIONS: usize = 240;

pub type Shared = Arc<Mutex<State>>;

pub struct State {
    pub imports: Vec<String>,
    pub log: Vec<String>,
    pub gl_calls: u64,
    pub vfs: Vec<u8>,
    pub errno_cell: u32,
    pub next_texture: u32,
    pub calls: BTreeMap<String, u64>,
    /// JNI 객체 보관소: 핸들 = 인덱스 + 1
    pub objects: Vec<JObj>,
    /// 찾아 둔 자바 메서드: (클래스, 이름, 시그니처). 메서드 ID = 인덱스 + 1
    pub methods: Vec<(String, String, String)>,
    pub java_calls: BTreeMap<String, u64>,
    // ---- 가상 파일 시스템 ----
    /// 열린 파일: fd -> (경로, 현재 위치)  (경로 "<vfs>" 는 data.vfs 묶음)
    pub fds: BTreeMap<i32, (String, usize)>,
    pub next_fd: i32,
    /// 쓰기 가능한 메모리 파일(세이브 등): 경로 -> 내용
    pub fs: BTreeMap<String, Vec<u8>>,
    /// FILE* 흉내: 게스트 포인터 -> fd
    pub file_ptrs: BTreeMap<u32, i32>,
    pub env_ptr: u32,
    pub gl: crate::gl::Gl,
    /// 호스트(자바스크립트)로 보낼 이벤트(JSON 한 줄씩): 소리, 진동, 종료
    pub events: Vec<String>,
    /// 내용이 바뀐 저장 파일 경로
    pub dirty: std::collections::BTreeSet<String>,
    /// 게임 시계(밀리초). 호스트가 프레임마다 갱신한다
    pub now_ms: u64,
    /// 네트워크 접속 실패를 기다리지 않도록 게임 시계를 앞당긴 양(ms)
    pub time_skew_ms: u64,
    /// 게임에 돌려줄 결제(MTX) 결과: (콘텐츠 ID, 결과 코드, 보조 코드)
    pub pending_mtx: Vec<(String, i32, i32)>,
    /// 게임에 돌려줄 전면 광고 결과: (결과, 코드)
    pub pending_fsad: Vec<(i32, i32)>,
    /// 이 시각(ms)까지는 호스트가 게임을 빠르게 돌려 준다(서버 접속 대기 건너뛰기)
    pub fast_forward_until_ms: u64,
    /// 에셋 이름 -> 번호(처음 요청된 순서대로 1,2,3...). 번호-1 = 인덱스
    pub asset_names: Vec<String>,
    /// 최근 호출 기록(문제 추적용)
    pub trace: std::collections::VecDeque<String>,
}

#[derive(Clone, Debug)]
pub enum JObj {
    Class(String),
    Str(String),
    Bytes(Vec<u8>),
    FileDescriptor(i32),
}

impl State {
    pub fn new(vfs: Vec<u8>) -> Self {
        Self {
            imports: Vec::new(),
            log: Vec::new(),
            gl_calls: 0,
            vfs,
            errno_cell: 0,
            next_texture: 1,
            calls: BTreeMap::new(),
            objects: Vec::new(),
            methods: Vec::new(),
            java_calls: BTreeMap::new(),
            fds: BTreeMap::new(),
            next_fd: 3,
            fs: BTreeMap::new(),
            file_ptrs: BTreeMap::new(),
            env_ptr: 0,
            gl: Default::default(),
            events: Vec::new(),
            dirty: Default::default(),
            now_ms: 1_700_000_000_000,
            time_skew_ms: 0,
            pending_mtx: Vec::new(),
            pending_fsad: Vec::new(),
            fast_forward_until_ms: 0,
            asset_names: Vec::new(),
            trace: Default::default(),
        }
    }
}

pub const VFS_PATH: &str = "<vfs>";

pub fn register(core: &mut ArmCore, state: &Shared) -> Result<()> {
    state.lock().fds.insert(100, (VFS_PATH.to_string(), 0));
    core.register_svc_handler(CAT_LIBC, handle_libc, state)?;
    core.register_svc_handler(CAT_JNI, handle_jni, state)
}

fn make_java_vm(core: &mut ArmCore) -> Result<u32> {
    let table = Allocator::alloc(core, 8 * 4)?;
    for i in 0..8u32 {
        let stub = core.make_svc_stub(CAT_JNI, 1000 + i)?;
        write_generic(core, table + i * 4, stub)?;
    }
    let vm = Allocator::alloc(core, 8)?;
    write_generic(core, vm, table)?;
    Ok(vm)
}

/// JNIEnv* 를 만든다: env -> 함수표 포인터 -> 함수 스텁들
pub fn make_jni_env(core: &mut ArmCore, state: &Shared) -> Result<u32> {
    let table = Allocator::alloc(core, (JNI_FUNCTIONS * 4) as u32)?;
    for i in 0..JNI_FUNCTIONS {
        let stub = core.make_svc_stub(CAT_JNI, i as u32)?;
        write_generic(core, table + (i * 4) as u32, stub)?;
    }
    let env = Allocator::alloc(core, 8)?;
    write_generic(core, env, table)?;
    state.lock().env_ptr = env;
    Ok(env)
}

fn guest_malloc(core: &mut ArmCore, size: u32) -> Result<u32> {
    // 할당 정보를 포인터 앞에 저장해 free 때 크기를 알 수 있게 한다
    let base = Allocator::alloc(core, size + 12)?;
    let ptr = (base + 12) & !7;
    write_generic(core, ptr - 8, base)?;
    write_generic(core, ptr - 4, size)?;
    Ok(ptr)
}

fn guest_free(core: &mut ArmCore, ptr: u32) -> Result<()> {
    if ptr != 0 {
        let base: u32 = read_generic(core, ptr - 8)?;
        let size: u32 = read_generic(core, ptr - 4)?;
        Allocator::free(core, base, size + 12)?;
    }
    Ok(())
}

enum Ret {
    U(u32),
    D(f64),
}

impl Ret {
    fn write_with(self, core: &mut ArmCore, lr: u32) -> Result<()> {
        match self {
            Ret::U(v) => v.write(core, lr),
            Ret::D(v) => v.write(core, lr),
        }
    }
}

fn cstr(core: &mut ArmCore, ptr: u32) -> Result<Vec<u8>> {
    if ptr == 0 {
        return Ok(Vec::new());
    }
    read_null_terminated_string_bytes(core, ptr)
}

fn fatal<T>(msg: String) -> Result<T> {
    Err(WieError::FatalError(msg))
}

async fn handle_libc(core: &mut ArmCore, state: &mut Shared, id: SvcId) -> Result<()> {
    let (_, lr) = core.read_pc_lr()?;
    let name = state.lock().imports[id.0 as usize].clone();
    *state.lock().calls.entry(name.clone()).or_insert(0) += 1;
    let p = |core: &ArmCore, i: usize| -> Result<u32> { core.read_param(i) };

    let ret = match name.as_str() {
        // ---- 메모리 ----
        "malloc" | "_Znwj" | "_Znaj" => Ret::U(guest_malloc(core, p(core, 0)?.max(1))?),
        "calloc" => {
            let size = p(core, 0)?.wrapping_mul(p(core, 1)?).max(1);
            let ptr = guest_malloc(core, size)?;
            core.write_bytes(ptr, &vec![0u8; size as usize])?;
            Ret::U(ptr)
        }
        "free" | "_ZdlPv" | "_ZdaPv" => {
            guest_free(core, p(core, 0)?)?;
            Ret::U(0)
        }
        "memcpy" | "memmove" => {
            let (dst, src, n) = (p(core, 0)?, p(core, 1)?, p(core, 2)?);
            if n > 0 {
                let mut buf = vec![0u8; n as usize];
                core.read_bytes(src, &mut buf)?;
                core.write_bytes(dst, &buf)?;
            }
            Ret::U(dst)
        }
        "memset" => {
            let (dst, v, n) = (p(core, 0)?, p(core, 1)?, p(core, 2)?);
            if n > 0 {
                core.write_bytes(dst, &vec![v as u8; n as usize])?;
            }
            Ret::U(dst)
        }
        // ---- 문자열 ----
        "strlen" => Ret::U(cstr(core, p(core, 0)?)?.len() as u32),
        "strcpy" | "strncpy" => {
            let (dst, src) = (p(core, 0)?, p(core, 1)?);
            let mut s = cstr(core, src)?;
            if name == "strncpy" {
                let n = p(core, 2)? as usize;
                s.resize(n, 0); // 모자라면 0 으로 채움, 넘치면 자름(종료 문자 없이)
            } else {
                s.push(0);
            }
            core.write_bytes(dst, &s)?;
            Ret::U(dst)
        }
        "strcat" => {
            let (dst, src) = (p(core, 0)?, p(core, 1)?);
            let len = cstr(core, dst)?.len() as u32;
            let mut s = cstr(core, src)?;
            s.push(0);
            core.write_bytes(dst + len, &s)?;
            Ret::U(dst)
        }
        "strcmp" | "strncmp" => {
            let (a, b) = (cstr(core, p(core, 0)?)?, cstr(core, p(core, 1)?)?);
            let (a, b) = if name == "strncmp" {
                let n = p(core, 2)? as usize;
                (a.into_iter().take(n).collect::<Vec<_>>(), b.into_iter().take(n).collect::<Vec<_>>())
            } else {
                (a, b)
            };
            Ret::U(match a.cmp(&b) {
                std::cmp::Ordering::Less => -1i32 as u32,
                std::cmp::Ordering::Equal => 0,
                std::cmp::Ordering::Greater => 1,
            })
        }
        "strstr" => {
            let (hp, np) = (p(core, 0)?, p(core, 1)?);
            let (h, n) = (cstr(core, hp)?, cstr(core, np)?);
            Ret::U(h.windows(n.len().max(1)).position(|w| w == n.as_slice()).map_or(0, |i| hp + i as u32))
        }
        "strdup" => {
            let mut s = cstr(core, p(core, 0)?)?;
            s.push(0);
            let ptr = guest_malloc(core, s.len() as u32)?;
            core.write_bytes(ptr, &s)?;
            Ret::U(ptr)
        }
        "atoi" => {
            let s = String::from_utf8_lossy(&cstr(core, p(core, 0)?)?).trim().to_string();
            let digits: String = s.chars().enumerate().take_while(|(i, c)| c.is_ascii_digit() || (*i == 0 && (*c == '-' || *c == '+'))).map(|(_, c)| c).collect();
            Ret::U(digits.parse::<i32>().unwrap_or(0) as u32)
        }
        "strerror" => {
            let ptr = guest_malloc(core, 16)?;
            core.write_bytes(ptr, b"error\0")?;
            Ret::U(ptr)
        }
        // ---- 수학 (soft-float: double 은 r0:r1, r2:r3) ----
        "floor" | "ceil" => {
            let v = f64::from_bits(p(core, 0)? as u64 | (p(core, 1)? as u64) << 32);
            Ret::D(if name == "floor" { v.floor() } else { v.ceil() })
        }
        "pow" => {
            let a = f64::from_bits(p(core, 0)? as u64 | (p(core, 1)? as u64) << 32);
            let b = f64::from_bits(p(core, 2)? as u64 | (p(core, 3)? as u64) << 32);
            Ret::D(a.powf(b))
        }
        "qsort" => {
            let (base, n, size, cmp) = (p(core, 0)?, p(core, 1)? as usize, p(core, 2)? as usize, p(core, 3)?);
            let mut items: Vec<Vec<u8>> = Vec::new();
            for i in 0..n {
                let mut b = vec![0u8; size];
                core.read_bytes(base + (i * size) as u32, &mut b)?;
                items.push(b);
            }
            // 안정 삽입 정렬 (비교 함수는 게스트 코드)
            let scratch = guest_malloc(core, (size * 2) as u32)?;
            for i in 1..n {
                let mut j = i;
                while j > 0 {
                    core.write_bytes(scratch, &items[j - 1])?;
                    core.write_bytes(scratch + size as u32, &items[j])?;
                    let r: u32 = core.run_function(cmp, &[scratch, scratch + size as u32]).await?;
                    if (r as i32) > 0 {
                        items.swap(j - 1, j);
                        j -= 1;
                    } else {
                        break;
                    }
                }
            }
            guest_free(core, scratch)?;
            for (i, b) in items.iter().enumerate() {
                core.write_bytes(base + (i * size) as u32, b)?;
            }
            Ret::U(0)
        }
        "ftime" => {
            let ptr = p(core, 0)?;
            let now = { let st = state.lock(); st.now_ms + st.time_skew_ms };
            write_generic(core, ptr, (now / 1000) as u32)?; // time
            write_generic(core, ptr + 4, (now % 1000) as u16)?; // millitm
            write_generic(core, ptr + 6, 0u16)?;
            write_generic(core, ptr + 8, 0u16)?;
            Ret::U(0)
        }
        // ---- 스레드/잠금 (단일 스레드) ----
        "pthread_mutex_lock" | "pthread_mutex_unlock" => Ret::U(0),
        // ---- 오류/로그/중단 ----
        "__errno" => {
            let mut st = state.lock();
            if st.errno_cell == 0 {
                drop(st);
                let cell = guest_malloc(core, 4)?;
                state.lock().errno_cell = cell;
                Ret::U(cell)
            } else {
                Ret::U(st.errno_cell)
            }
        }
        "__stack_chk_fail" | "abort" | "__cxa_call_unexpected" | "__cxa_pure_virtual" => return fatal(format!("게임이 {name} 을(를) 호출")),
        "__cxa_begin_cleanup" | "__cxa_type_match" => return fatal(format!("C++ 예외 처리({name})는 아직 지원하지 않음")),
        "__android_log_print" => {
            let (prio, tag, fmt) = (p(core, 0)?, cstr(core, p(core, 1)?)?, cstr(core, p(core, 2)?)?);
            let mut args = RegArgs { pos: 3 };
            let text = format_c(core, &fmt, &mut args)?;
            let line = format!("[log{prio}] {}: {}", String::from_utf8_lossy(&tag), String::from_utf8_lossy(&text));
            eprintln!("{line}");
            state.lock().log.push(line);
            Ret::U(0)
        }
        "vsnprintf" => {
            let (buf, size, fmt, ap) = (p(core, 0)?, p(core, 1)?, p(core, 2)?, p(core, 3)?);
            let fmt = cstr(core, fmt)?;
            let mut args = MemArgs { ptr: ap };
            let mut text = format_c(core, &fmt, &mut args)?;
            let full = text.len() as u32;
            if size > 0 {
                text.truncate(size as usize - 1);
                text.push(0);
                core.write_bytes(buf, &text)?;
            }
            Ret::U(full)
        }
        // ---- 파일 (가상 파일 시스템) ----
        "open" => {
            let path = String::from_utf8_lossy(&cstr(core, p(core, 0)?)?).to_string();
            let flags = p(core, 1)?;
            let mut st = state.lock();
            let exists = st.fs.contains_key(&path);
            let writable = flags & 3 != 0;
            if !exists && flags & 0x40 == 0 {
                st.log.push(format!("[fs] open 실패(없음): {path}"));
                Ret::U(-1i32 as u32)
            } else {
                if !exists || flags & 0x200 != 0 {
                    st.fs.insert(path.clone(), Vec::new());
                    st.dirty.insert(path.clone());
                }
                let pos = if flags & 0x400 != 0 { st.fs[&path].len() } else { 0 };
                let fd = st.next_fd;
                st.next_fd += 1;
                st.fds.insert(fd, (path.clone(), pos));
                st.log.push(format!("[fs] open {path} (쓰기={writable}) -> fd {fd}"));
                Ret::U(fd as u32)
            }
        }
        "dup" => {
            let fd = p(core, 0)? as i32;
            let mut st = state.lock();
            match st.fds.get(&fd).cloned() {
                Some(f) => {
                    let n = st.next_fd;
                    st.next_fd += 1;
                    st.fds.insert(n, f);
                    Ret::U(n as u32)
                }
                None => Ret::U(-1i32 as u32),
            }
        }
        "close" => {
            let fd = p(core, 0)? as i32;
            Ret::U(if state.lock().fds.remove(&fd).is_some() { 0 } else { -1i32 as u32 })
        }
        "read" | "write" | "lseek" => {
            let (fd, a1, a2) = (p(core, 0)? as i32, p(core, 1)?, p(core, 2)?);
            let mut st = state.lock();
            let Some((path, pos)) = st.fds.get(&fd).cloned() else { return Ret::U(-1i32 as u32).write_with(core, lr) };
            let is_vfs = path == VFS_PATH;
            let len = if is_vfs { st.vfs.len() } else { st.fs.get(&path).map_or(0, |f| f.len()) };
            match name.as_str() {
                "read" => {
                    let n = (a2 as usize).min(len.saturating_sub(pos));
                    let data = if is_vfs { st.vfs[pos..pos + n].to_vec() } else { st.fs[&path][pos..pos + n].to_vec() };
                    st.fds.get_mut(&fd).unwrap().1 = pos + n;
                    drop(st);
                    core.write_bytes(a1, &data)?;
                    Ret::U(n as u32)
                }
                "write" => {
                    let mut data = vec![0u8; a2 as usize];
                    drop(st);
                    core.read_bytes(a1, &mut data)?;
                    let mut st = state.lock();
                    if path != VFS_PATH {
                        let file = st.fs.entry(path.clone()).or_default();
                        if file.len() < pos + data.len() {
                            file.resize(pos + data.len(), 0);
                        }
                        file[pos..pos + data.len()].copy_from_slice(&data);
                        st.dirty.insert(path.clone());
                        st.fds.get_mut(&fd).unwrap().1 = pos + data.len();
                    }
                    Ret::U(a2)
                }
                _ => {
                    let new = match p(core, 2)? {
                        _ => 0,
                    };
                    let _ = new;
                    let whence = a2;
                    let target = match whence {
                        0 => a1 as i32 as i64,
                        1 => pos as i64 + a1 as i32 as i64,
                        _ => len as i64 + a1 as i32 as i64,
                    };
                    let target = target.max(0) as usize;
                    st.fds.get_mut(&fd).unwrap().1 = target;
                    Ret::U(target as u32)
                }
            }
        }
        "fdopen" => {
            // fdopen(fd, mode) -> FILE* (게스트 메모리의 작은 상자)
            let fd = p(core, 0)? as i32;
            if !state.lock().fds.contains_key(&fd) {
                Ret::U(0)
            } else {
                let ptr = guest_malloc(core, 16)?;
                state.lock().file_ptrs.insert(ptr, fd);
                Ret::U(ptr)
            }
        }
        "fclose" => {
            let ptr = p(core, 0)?;
            state.lock().file_ptrs.remove(&ptr);
            Ret::U(0)
        }
        "fseek" | "fread" => {
            let (a0, a1, a2, a3) = (p(core, 0)?, p(core, 1)?, p(core, 2)?, p(core, 3)?);
            let mut st = state.lock();
            if name == "fseek" {
                let fd = st.file_ptrs.get(&a0).copied().unwrap_or(-1);
                let Some((path, pos)) = st.fds.get(&fd).cloned() else { return Ret::U(-1i32 as u32).write_with(core, lr) };
                let len = if path == VFS_PATH { st.vfs.len() } else { st.fs.get(&path).map_or(0, |f| f.len()) };
                let target = match a2 {
                    0 => a1 as i32 as i64,
                    1 => pos as i64 + a1 as i32 as i64,
                    _ => len as i64 + a1 as i32 as i64,
                };
                st.fds.get_mut(&fd).unwrap().1 = target.max(0) as usize;
                Ret::U(0)
            } else {
                // fread(ptr, size, count, FILE*)
                let fd = st.file_ptrs.get(&a3).copied().unwrap_or(-1);
                let Some((path, pos)) = st.fds.get(&fd).cloned() else { return Ret::U(0).write_with(core, lr) };
                let total = (a1 as usize) * (a2 as usize);
                let len = if path == VFS_PATH { st.vfs.len() } else { st.fs.get(&path).map_or(0, |f| f.len()) };
                let n = total.min(len.saturating_sub(pos));
                let whole = if a1 == 0 { 0 } else { n / a1 as usize };
                let n = whole * a1 as usize;
                let data = if path == VFS_PATH { st.vfs[pos..pos + n].to_vec() } else { st.fs[&path][pos..pos + n].to_vec() };
                st.fds.get_mut(&fd).unwrap().1 = pos + n;
                drop(st);
                core.write_bytes(a0, &data)?;
                Ret::U(whole as u32)
            }
        }
        "stat" | "access" => {
            let path = String::from_utf8_lossy(&cstr(core, p(core, 0)?)?).to_string();
            let st = state.lock();
            let size = st.fs.get(&path).map(|f| f.len());
            let is_dir = st.fs.keys().any(|k| k.starts_with(&format!("{path}/")));
            drop(st);
            if size.is_none() && !is_dir {
                Ret::U(-1i32 as u32)
            } else {
                if name == "stat" {
                    let buf = p(core, 1)?;
                    core.write_bytes(buf, &[0u8; 104])?;
                    write_generic(core, buf + 16, if is_dir { 0x41edu32 } else { 0x81a4u32 })?;
                    write_generic(core, buf + 48, size.unwrap_or(0) as u32)?;
                }
                Ret::U(0)
            }
        }
        "mkdir" | "chdir" | "rmdir" | "fcntl" => Ret::U(0),
        "unlink" => {
            let path = String::from_utf8_lossy(&cstr(core, p(core, 0)?)?).to_string();
            let mut st = state.lock();
            let removed = st.fs.remove(&path).is_some();
            if removed {
                st.dirty.insert(path);
            }
            Ret::U(if removed { 0 } else { -1i32 as u32 })
        }
        "rename" => {
            let (a, b) = (String::from_utf8_lossy(&cstr(core, p(core, 0)?)?).to_string(), String::from_utf8_lossy(&cstr(core, p(core, 1)?)?).to_string());
            let mut st = state.lock();
            match st.fs.remove(&a) {
                Some(d) => {
                    st.dirty.insert(a.clone());
                    st.dirty.insert(b.clone());
                    st.fs.insert(b, d);
                    Ret::U(0)
                }
                None => Ret::U(-1i32 as u32),
            }
        }
        // ---- 소켓: 네트워크 기능은 지원하지 않음 ----
        "socket" | "connect" | "recv" | "select" | "setsockopt" | "getsockopt" | "shutdown" | "inet_addr" => {
            // 네트워크는 지원하지 않는다: ENETUNREACH(101) 로 바로 실패시킨다
            let cell = {
                let existing = state.lock().errno_cell;
                if existing == 0 {
                    let c = guest_malloc(core, 4)?;
                    state.lock().errno_cell = c;
                    c
                } else {
                    existing
                }
            };
            write_generic(core, cell, 101u32)?;
            if name == "connect" {
                // 서버 접속을 기다리는 동안은 호스트가 게임을 빠르게 돌려 준다(약 90초 동안)
                let mut st = state.lock();
                st.fast_forward_until_ms = st.now_ms + 90_000;
            }
            state.lock().log.push(format!("[stub] {name}"));
            Ret::U(-1i32 as u32)
        }
        // ---- OpenGL ES 1.x (소프트웨어 구현) ----
        n if n.starts_with("gl") => {
            let (mut gl, mut next) = {
                let mut st = state.lock();
                (std::mem::take(&mut st.gl), st.next_texture)
            };
            gl.call(core, n, &mut next)?;
            let mut st = state.lock();
            st.gl = gl;
            st.next_texture = next;
            st.gl_calls += 1;
            Ret::U(0)
        }
        other => return fatal(format!("구현되지 않은 시스템 함수: {other}")),
    };

    {
        let mut st = state.lock();
        let shown = match &ret {
            Ret::U(v) => format!("{v:#x}"),
            Ret::D(v) => format!("{v}"),
        };
        let args = format!("{:#x},{:#x},{:#x},{:#x}", core.read_param(0).unwrap_or(0), core.read_param(1).unwrap_or(0), core.read_param(2).unwrap_or(0), core.read_param(3).unwrap_or(0));
        st.trace.push_back(format!("{name}({args}) -> {shown}  [호출한 곳 {lr:#x}]"));
        if st.trace.len() > 40 {
            st.trace.pop_front();
        }
    }
    match ret {
        Ret::U(v) => v.write(core, lr),
        Ret::D(v) => v.write(core, lr),
    }
}

/// JNI 함수 호출
async fn handle_jni(core: &mut ArmCore, state: &mut Shared, id: SvcId) -> Result<()> {
    let (_, lr) = core.read_pc_lr()?;
    let idx = id.0;
    let p = |core: &ArmCore, i: usize| -> Result<u32> { core.read_param(i) };
    let ret: u32 = match idx {
        4 => 0x0001_0006, // GetVersion
        219 => {
            // GetJavaVM(env, JavaVM** out): JavaVM 함수표(8칸)를 만들어 준다
            let out = p(core, 1)?;
            let vm = make_java_vm(core)?;
            write_generic(core, out, vm)?;
            0
        }
        1000..=1007 => {
            // JavaVM 함수: GetEnv(vm, void** penv, version) / AttachCurrentThread(vm, void** penv, args)
            if idx == 1004 || idx == 1006 || idx == 1007 {
                let penv = p(core, 1)?;
                let env = state.lock().env_ptr;
                write_generic(core, penv, env)?;
            }
            0
        }
        6 => {
            // FindClass(env, name)
            let name = String::from_utf8_lossy(&cstr(core, p(core, 1)?)?).to_string();
            state.lock().log.push(format!("[jni] FindClass {name}"));
            new_object(state, JObj::Class(name))
        }
        15 | 17 | 228 => 0, // ExceptionOccurred / ExceptionClear / ExceptionCheck
        21 | 25 => p(core, 1)?, // NewGlobalRef / NewLocalRef: 같은 핸들
        22 | 23 | 218 | 217 => 0, // DeleteGlobalRef / DeleteLocalRef / Monitor*
        26 => 0,
        31 => p(core, 1)?, // GetObjectClass (자바 객체의 클래스는 알 수 없으므로 같은 핸들)
        33 | 113 | 94 | 144 => {
            // GetMethodID / GetStaticMethodID / GetFieldID / GetStaticFieldID (env, clazz, name, sig)
            let class_handle = p(core, 1)?;
            let name = String::from_utf8_lossy(&cstr(core, p(core, 2)?)?).to_string();
            let sig = String::from_utf8_lossy(&cstr(core, p(core, 3)?)?).to_string();
            let class_name = match state.lock().objects.get((class_handle as usize).wrapping_sub(1)) {
                Some(JObj::Class(n)) => n.clone(),
                _ => String::new(),
            };
            let mut st = state.lock();
            st.methods.push((class_name, name, sig));
            st.methods.len() as u32
        }
        // CallStatic<Type>Method(env, cls, mid, ...) / CallStatic<Type>MethodV / CallStatic<Type>MethodA
        114..=143 | 34..=63 => {
            let (is_static, base) = if idx >= 114 { (true, 114) } else { (false, 34) };
            let kind = (idx - base) / 3;
            let variant = (idx - base) % 3; // 0: ..., 1: V(va_list), 2: A(jvalue[])
            let mid = p(core, 2)?;
            let (_class_name, name, sig) = state.lock().methods.get(mid.wrapping_sub(1) as usize).cloned().unwrap_or_default();
            let args = read_java_args(core, &sig, variant, 3)?;
            let result = crate::java::call(core, state, &name, &sig, &args)?;
            let _ = (is_static, kind);
            match result {
                crate::java::JValue::Int(v) => v as u32,
                crate::java::JValue::Obj(h) => h,
                crate::java::JValue::Void => 0,
                crate::java::JValue::Long(v) => {
                    // 64비트 반환: r0:r1
                    (v as u64 as u32).write(core, lr).ok();
                    core.write_return_value(&[v as u32, (v as u64 >> 32) as u32])?;
                    return core.set_next_pc(lr);
                }
            }
        }
        100 => {
            // GetIntField(env, obj, fid): FileDescriptor.descriptor 등
            match state.lock().objects.get((p(core, 1)? as usize).wrapping_sub(1)) {
                Some(JObj::FileDescriptor(fd)) => *fd as u32,
                _ => 0,
            }
        }
        167 => {
            // NewStringUTF(env, chars)
            let s = String::from_utf8_lossy(&cstr(core, p(core, 1)?)?).to_string();
            new_object(state, JObj::Str(s))
        }
        168 | 169 => {
            // GetStringUTFLength(168? ) / GetStringUTFChars(169): env, str, isCopy
            let h = p(core, 1)?;
            let text = match state.lock().objects.get((h as usize).wrapping_sub(1)) {
                Some(JObj::Str(s)) => s.clone(),
                _ => String::new(),
            };
            if idx == 168 {
                text.len() as u32
            } else {
                let ptr = guest_malloc(core, text.len() as u32 + 1)?;
                let mut b = text.into_bytes();
                b.push(0);
                core.write_bytes(ptr, &b)?;
                let flag = p(core, 2)?;
                if flag != 0 {
                    write_generic(core, flag, 1u8)?;
                }
                ptr
            }
        }
        170 => {
            // ReleaseStringUTFChars(env, str, chars)
            guest_free(core, p(core, 2)?)?;
            0
        }
        171 => {
            // GetArrayLength
            match state.lock().objects.get((p(core, 1)? as usize).wrapping_sub(1)) {
                Some(JObj::Bytes(b)) => b.len() as u32,
                _ => 0,
            }
        }
        176 => {
            // NewByteArray(env, len)
            let len = p(core, 1)? as usize;
            new_object(state, JObj::Bytes(vec![0; len]))
        }
        200 => {
            // GetByteArrayRegion(env, arr, start, len, buf)
            let (h, start, len, buf) = (p(core, 1)?, p(core, 2)? as usize, p(core, 3)? as usize, p(core, 4)?);
            let data = match state.lock().objects.get((h as usize).wrapping_sub(1)) {
                Some(JObj::Bytes(b)) => b[start.min(b.len())..(start + len).min(b.len())].to_vec(),
                _ => Vec::new(),
            };
            core.write_bytes(buf, &data)?;
            0
        }
        208 => {
            // SetByteArrayRegion(env, arr, start, len, buf)
            let (h, start, len, buf) = (p(core, 1)?, p(core, 2)? as usize, p(core, 3)? as usize, p(core, 4)?);
            let mut data = vec![0u8; len];
            core.read_bytes(buf, &mut data)?;
            if let Some(JObj::Bytes(b)) = state.lock().objects.get_mut((h as usize).wrapping_sub(1)) {
                if start + len <= b.len() {
                    b[start..start + len].copy_from_slice(&data);
                }
            }
            0
        }
        184 | 222 => {
            // GetByteArrayElements / GetPrimitiveArrayCritical: 게스트 메모리에 복사해서 준다
            let h = p(core, 1)?;
            let data = match state.lock().objects.get((h as usize).wrapping_sub(1)) {
                Some(JObj::Bytes(b)) => b.clone(),
                _ => Vec::new(),
            };
            let ptr = guest_malloc(core, data.len() as u32 + 1)?;
            core.write_bytes(ptr, &data)?;
            let flag = p(core, 2)?;
            if flag != 0 {
                write_generic(core, flag, 1u8)?;
            }
            ptr
        }
        192 | 223 => {
            // ReleaseByteArrayElements(env, arr, elems, mode): 변경 내용을 되돌려 쓴다(mode 2 = JNI_ABORT)
            let (h, ptr, mode) = (p(core, 1)?, p(core, 2)?, p(core, 3)?);
            let len = match state.lock().objects.get((h as usize).wrapping_sub(1)) {
                Some(JObj::Bytes(b)) => b.len(),
                _ => 0,
            };
            if mode != 2 && len > 0 {
                let mut data = vec![0u8; len];
                core.read_bytes(ptr, &mut data)?;
                if let Some(JObj::Bytes(b)) = state.lock().objects.get_mut((h as usize).wrapping_sub(1)) {
                    *b = data;
                }
            }
            if mode != 1 {
                guest_free(core, ptr)?;
            }
            0
        }
        other => {
            eprintln!("[jni] 구현되지 않은 JNI 함수 번호 {other} (r1={:#x}, r2={:#x})", p(core, 1)?, p(core, 2)?);
            0
        }
    };
    ret.write(core, lr)
}

pub fn new_object(state: &Shared, obj: JObj) -> u32 {
    let mut st = state.lock();
    st.objects.push(obj);
    st.objects.len() as u32
}

/// 시그니처 "(IJLjava/lang/String;[B)I" 에 맞춰 가변 인자를 읽는다
fn read_java_args(core: &mut ArmCore, sig: &str, variant: u32, first: usize) -> Result<Vec<crate::java::JValue>> {
    use crate::java::JValue;
    let params = sig.trim_start_matches('(').split(')').next().unwrap_or("");
    let mut kinds: Vec<char> = Vec::new();
    let mut chars = params.chars().peekable();
    while let Some(c) = chars.next() {
        match c {
            'L' => {
                for d in chars.by_ref() {
                    if d == ';' {
                        break;
                    }
                }
                kinds.push('L');
            }
            '[' => {
                while chars.peek() == Some(&'[') {
                    chars.next();
                }
                if chars.next() == Some('L') {
                    for d in chars.by_ref() {
                        if d == ';' {
                            break;
                        }
                    }
                }
                kinds.push('L');
            }
            c => kinds.push(c),
        }
    }
    let mut out = Vec::new();
    match variant {
        0 | 1 => {
            let mut reg = RegArgs { pos: first };
            let mut mem = MemArgs { ptr: if variant == 1 { core.read_param(first)? } else { 0 } };
            let args: &mut dyn Args = if variant == 0 { &mut reg } else { &mut mem };
            for k in kinds {
                out.push(match k {
                    'J' => JValue::Long(args.dword(core)? as i64),
                    'D' => JValue::Long(args.dword(core)? as i64), // double 은 쓰지 않는 것으로 가정
                    'F' => JValue::Int(f64::from_bits(args.dword(core)?) as f32 as i32), // float 는 double 로 승격되어 전달됨
                    'L' => JValue::Obj(args.word(core)?),
                    _ => JValue::Int(args.word(core)? as i32),
                });
            }
        }
        _ => {
            // jvalue[]: 항목당 8바이트
            let base = core.read_param(first)?;
            for (i, k) in kinds.iter().enumerate() {
                let at = base + (i * 8) as u32;
                out.push(match k {
                    'J' | 'D' => {
                        let lo: u32 = read_generic(core, at)?;
                        let hi: u32 = read_generic(core, at + 4)?;
                        JValue::Long((lo as u64 | (hi as u64) << 32) as i64)
                    }
                    'L' => JValue::Obj(read_generic(core, at)?),
                    _ => JValue::Int(read_generic::<u32, _>(core, at)? as i32),
                });
            }
        }
    }
    Ok(out)
}

// ---------------- printf 계열 서식 ----------------

pub trait Args {
    fn word(&mut self, core: &mut ArmCore) -> Result<u32>;
    fn dword(&mut self, core: &mut ArmCore) -> Result<u64>;
}

/// 가변 인자(...) 가 레지스터 r(pos).. 와 스택에 있을 때
struct RegArgs {
    pos: usize,
}
impl Args for RegArgs {
    fn word(&mut self, core: &mut ArmCore) -> Result<u32> {
        let v = core.read_param(self.pos)?;
        self.pos += 1;
        Ok(v)
    }
    fn dword(&mut self, core: &mut ArmCore) -> Result<u64> {
        if self.pos % 2 == 1 {
            self.pos += 1;
        }
        let lo = self.word(core)? as u64;
        let hi = self.word(core)? as u64;
        Ok(lo | hi << 32)
    }
}

/// va_list: 인자 영역을 가리키는 포인터
struct MemArgs {
    ptr: u32,
}
impl Args for MemArgs {
    fn word(&mut self, core: &mut ArmCore) -> Result<u32> {
        let v: u32 = read_generic(core, self.ptr)?;
        self.ptr += 4;
        Ok(v)
    }
    fn dword(&mut self, core: &mut ArmCore) -> Result<u64> {
        self.ptr = (self.ptr + 7) & !7;
        let lo = self.word(core)? as u64;
        let hi = self.word(core)? as u64;
        Ok(lo | hi << 32)
    }
}

fn pad(mut s: Vec<u8>, width: usize, left: bool, zero: bool) -> Vec<u8> {
    if s.len() < width {
        let fill = width - s.len();
        if left {
            s.extend(std::iter::repeat(b' ').take(fill));
        } else if zero {
            let at = if s.first().is_some_and(|c| *c == b'-' || *c == b'+') { 1 } else { 0 };
            for _ in 0..fill {
                s.insert(at, b'0');
            }
        } else {
            let mut v = vec![b' '; fill];
            v.extend(s);
            s = v;
        }
    }
    s
}

fn format_c(core: &mut ArmCore, fmt: &[u8], args: &mut dyn Args) -> Result<Vec<u8>> {
    let mut out = Vec::new();
    let mut i = 0;
    while i < fmt.len() {
        if fmt[i] != b'%' {
            out.push(fmt[i]);
            i += 1;
            continue;
        }
        i += 1;
        let (mut left, mut zero, mut plus, mut space) = (false, false, false, false);
        while i < fmt.len() && b"-0+ #".contains(&fmt[i]) {
            match fmt[i] {
                b'-' => left = true,
                b'0' => zero = true,
                b'+' => plus = true,
                b' ' => space = true,
                _ => {}
            }
            i += 1;
        }
        let mut width = 0usize;
        if i < fmt.len() && fmt[i] == b'*' {
            width = args.word(core)? as i32 as usize;
            i += 1;
        }
        while i < fmt.len() && fmt[i].is_ascii_digit() {
            width = width * 10 + (fmt[i] - b'0') as usize;
            i += 1;
        }
        let mut prec: Option<usize> = None;
        if i < fmt.len() && fmt[i] == b'.' {
            i += 1;
            let mut p = 0usize;
            if i < fmt.len() && fmt[i] == b'*' {
                p = args.word(core)? as usize;
                i += 1;
            }
            while i < fmt.len() && fmt[i].is_ascii_digit() {
                p = p * 10 + (fmt[i] - b'0') as usize;
                i += 1;
            }
            prec = Some(p);
        }
        let mut long_long = false;
        while i < fmt.len() && b"hlzjt".contains(&fmt[i]) {
            if fmt[i] == b'l' && i + 1 < fmt.len() && fmt[i + 1] == b'l' {
                long_long = true;
            }
            i += 1;
        }
        if i >= fmt.len() {
            break;
        }
        let conv = fmt[i];
        i += 1;
        let body: Vec<u8> = match conv {
            b'%' => b"%".to_vec(),
            b'd' | b'i' => {
                let v = if long_long { args.dword(core)? as i64 } else { args.word(core)? as i32 as i64 };
                let mut s = v.to_string();
                if v >= 0 && plus {
                    s.insert(0, '+');
                } else if v >= 0 && space {
                    s.insert(0, ' ');
                }
                s.into_bytes()
            }
            b'u' => (if long_long { args.dword(core)? } else { args.word(core)? as u64 }).to_string().into_bytes(),
            b'x' => format!("{:x}", if long_long { args.dword(core)? } else { args.word(core)? as u64 }).into_bytes(),
            b'X' => format!("{:X}", if long_long { args.dword(core)? } else { args.word(core)? as u64 }).into_bytes(),
            b'o' => format!("{:o}", args.word(core)?).into_bytes(),
            b'p' => format!("0x{:x}", args.word(core)?).into_bytes(),
            b'c' => vec![args.word(core)? as u8],
            b's' => {
                let ptr = args.word(core)?;
                let mut s = if ptr == 0 { b"(null)".to_vec() } else { cstr(core, ptr)? };
                if let Some(p) = prec {
                    s.truncate(p);
                }
                s
            }
            b'f' | b'F' | b'e' | b'E' | b'g' | b'G' => {
                let v = f64::from_bits(args.dword(core)?);
                format!("{:.*}", prec.unwrap_or(6), v).into_bytes()
            }
            other => vec![b'%', other],
        };
        out.extend(pad(body, width, left, zero && !left));
    }
    Ok(out)
}
