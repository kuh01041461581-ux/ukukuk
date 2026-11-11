//! 브라우저(웹어셈블리)용 인터페이스. 자바스크립트가 이 객체로 게임을 구동한다.

use std::{cell::RefCell, sync::Arc};

use futures::channel::{mpsc, oneshot};
use spin::Mutex;
use wasm_bindgen::prelude::*;
use wie_util::Result as WieResult;

use crate::{
    hle::State,
    session::{Cmd, Config, run_game},
};

#[wasm_bindgen(start)]
pub fn start() {
    std::panic::set_hook(Box::new(|info| {
        web_log(&format!("PANIC: {info}"));
    }));
}

#[wasm_bindgen]
extern "C" {
    #[wasm_bindgen(js_namespace = console, js_name = error)]
    fn web_log(s: &str);
}

#[wasm_bindgen]
pub struct AndroidGame {
    tx: mpsc::UnboundedSender<Cmd>,
    state: Arc<Mutex<State>>,
    ready: RefCell<Option<oneshot::Receiver<WieResult<()>>>>,
}

#[wasm_bindgen]
impl AndroidGame {
    /// so: 게임의 32비트 라이브러리, vfs: data.vfs 묶음 파일, lcd: 가상 화면 크기
    #[wasm_bindgen(constructor)]
    pub fn new(so: Vec<u8>, vfs: Vec<u8>, lcd_width: u32, lcd_height: u32, use_aot: bool, bnv: String) -> AndroidGame {
        let state = Arc::new(Mutex::new(State::new(vfs)));
        let (tx, rx) = mpsc::unbounded();
        let (ready_tx, ready_rx) = oneshot::channel();
        let st = state.clone();
        wasm_bindgen_futures::spawn_local(async move {
            if let Err(error) = run_game(so, st, Config { lcd_width, lcd_height, bnv, aot: if use_aot { Some(Box::new(crate::aot::WasmExecutor::default())) } else { None } }, rx, ready_tx).await {
                web_log(&format!("게임 종료: {error:?}"));
            }
        });
        AndroidGame { tx, state, ready: RefCell::new(Some(ready_rx)) }
    }

    /// 시작 순서가 끝날 때까지 기다린다
    pub async fn ready(&self) -> Result<(), JsError> {
        let rx = self.ready.borrow_mut().take().ok_or_else(|| JsError::new("이미 시작됨"))?;
        match rx.await {
            Ok(Ok(())) => Ok(()),
            Ok(Err(error)) => Err(JsError::new(&format!("{error}"))),
            Err(_) => Err(JsError::new("시작 작업이 중단됨")),
        }
    }

    /// 한 프레임 진행
    pub async fn frame(&self, now_ms: f64) -> Result<(), JsError> {
        let (done, rx) = oneshot::channel();
        self.tx.unbounded_send(Cmd::Frame { now_ms: now_ms as u64, done }).map_err(|_| JsError::new("게임이 끝났음"))?;
        match rx.await {
            Ok(Ok(())) => Ok(()),
            Ok(Err(error)) => Err(JsError::new(&format!("{error}"))),
            Err(_) => Err(JsError::new("프레임 작업이 중단됨")),
        }
    }

    /// 서버 접속 대기처럼 빨리 넘겨도 되는 구간인지 (true 이면 한 번에 여러 프레임을 돌린다)
    pub fn wants_fast_forward(&self, now_ms: f64) -> bool {
        (now_ms as u64) < self.state.lock().fast_forward_until_ms
    }

    /// 디버그: 지금까지의 시스템 함수 호출 횟수 (JSON)
    pub fn debug_calls(&self) -> String {
        let st = self.state.lock();
        let mut items: Vec<String> = st.calls.iter().map(|(k, v)| format!("\"{k}\":{v}")).collect();
        items.extend(st.java_calls.iter().map(|(k, v)| format!("\"java:{k}\":{v}")));
        format!("{{{}}}", items.join(","))
    }

    pub fn touch(&self, kind: u32, x: i32, y: i32, id: i32) {
        let _ = self.tx.unbounded_send(Cmd::Touch { kind, x, y, id });
    }
    pub fn key(&self, action: u32, code: u32) {
        let _ = self.tx.unbounded_send(Cmd::Key { action, code });
    }
    pub fn pause(&self) {
        let _ = self.tx.unbounded_send(Cmd::Pause);
    }
    pub fn resume(&self) {
        let _ = self.tx.unbounded_send(Cmd::Resume);
    }

    pub fn width(&self) -> u32 {
        self.state.lock().gl.width as u32
    }
    pub fn height(&self) -> u32 {
        self.state.lock().gl.height as u32
    }
    /// 현재 화면 (RGBA, 위에서 아래로)
    pub fn frame_rgba(&self) -> Vec<u8> {
        self.state.lock().gl.frame.clone()
    }

    /// 소리/진동/종료 이벤트(JSON 배열)를 가져가고 비운다
    pub fn take_events(&self) -> String {
        let mut st = self.state.lock();
        if st.events.is_empty() {
            return "[]".to_string();
        }
        let out = format!("[{}]", st.events.join(","));
        st.events.clear();
        out
    }

    /// 내용이 바뀐 저장 파일 목록을 가져가고 비운다
    pub fn take_dirty_files(&self) -> Vec<String> {
        let mut st = self.state.lock();
        let v: Vec<String> = st.dirty.iter().cloned().collect();
        st.dirty.clear();
        v
    }
    /// 저장 파일 내용 (없으면 빈 배열). 지워진 파일은 exists_file 로 구분한다.
    pub fn read_file(&self, path: &str) -> Vec<u8> {
        self.state.lock().fs.get(path).cloned().unwrap_or_default()
    }
    pub fn exists_file(&self, path: &str) -> bool {
        self.state.lock().fs.contains_key(path)
    }
    /// 시작 전에 저장해 둔 파일을 되살린다
    pub fn load_file(&self, path: &str, data: Vec<u8>) {
        self.state.lock().fs.insert(path.to_string(), data);
    }
}
