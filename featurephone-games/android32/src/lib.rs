//! 32비트 ARM(armeabi) 안드로이드 네이티브 게임 라이브러리(.so)를 WIE 의 ARM 실행기 위에서 돌린다.

pub mod gl;
pub mod hle;
pub mod java;
pub mod loader;
pub mod session;

#[cfg(target_arch = "wasm32")]
mod aot;
#[cfg(target_arch = "wasm32")]
pub mod web;
