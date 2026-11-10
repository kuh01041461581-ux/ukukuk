//! ARM 32비트 공유 라이브러리(ELF) 로더: 구간 올리기 + 재배치.

use std::{collections::BTreeMap, sync::Arc};

use elf::{ElfBytes, abi, endian::AnyEndian};
use spin::Mutex;
use wie_core_arm::ArmCore;
use wie_util::{ByteWrite, Result, WieError, write_generic};

use crate::hle::{CAT_LIBC, State};

pub const BASE: u32 = 0x1000_0000;

pub struct Library {
    pub base: u32,
    /// 이름 → 실제 주소 (Thumb 함수는 최하위 비트가 1)
    pub exports: BTreeMap<String, u32>,
    /// 정적 생성자(.init_array) 주소들: 로드 직후 순서대로 실행해야 한다
    pub init_functions: Vec<u32>,
}

const R_ARM_ABS32: u32 = 2;
const R_ARM_GLOB_DAT: u32 = 21;
const R_ARM_JUMP_SLOT: u32 = 22;
const R_ARM_RELATIVE: u32 = 23;

fn err<E: std::fmt::Display>(e: E) -> WieError {
    WieError::FatalError(format!("ELF: {e}"))
}

pub fn load(core: &mut ArmCore, state: &Arc<Mutex<State>>, data: &[u8]) -> Result<Library> {
    let file = ElfBytes::<AnyEndian>::minimal_parse(data).map_err(err)?;
    if file.ehdr.e_machine != abi::EM_ARM || file.ehdr.class != elf::file::Class::ELF32 {
        return Err(err("32비트 ARM ELF 가 아님"));
    }

    // 1) PT_LOAD 구간을 하나의 이미지로 합친다
    let segments: Vec<_> = file.segments().ok_or_else(|| err("프로그램 헤더 없음"))?.iter().filter(|s| s.p_type == abi::PT_LOAD).collect();
    let total = segments.iter().map(|s| (s.p_vaddr + s.p_memsz) as usize).max().unwrap_or(0);
    let mut image = vec![0u8; total];
    for s in &segments {
        let src = &data[s.p_offset as usize..(s.p_offset + s.p_filesz) as usize];
        image[s.p_vaddr as usize..s.p_vaddr as usize + src.len()].copy_from_slice(src);
    }
    core.load(&image, BASE, total)?;

    // 2) 동적 심볼
    let (dynsym, dynstr) = file.dynamic_symbol_table().map_err(err)?.ok_or_else(|| err("동적 심볼 없음"))?;
    let mut exports = BTreeMap::new();
    let mut symbol_address: Vec<u32> = Vec::new();
    for symbol in dynsym.iter() {
        let name = dynstr.get(symbol.st_name as usize).map_err(err)?.to_string();
        let address = if symbol.is_undefined() {
            0
        } else {
            let a = BASE.wrapping_add(symbol.st_value as u32);
            if symbol.st_symtype() == abi::STT_FUNC {
                exports.insert(name.clone(), a);
            }
            a
        };
        symbol_address.push(address);
        // 미해결 심볼은 아래 재배치에서 호스트 함수(스텁)로 연결한다
        let _ = &name;
    }
    let names: Vec<String> = dynsym.iter().map(|s| dynstr.get(s.st_name as usize).unwrap_or("").to_string()).collect();
    let undefined: Vec<bool> = dynsym.iter().map(|s| s.is_undefined()).collect();

    // 3) 재배치
    let mut stub_cache: BTreeMap<String, u32> = BTreeMap::new();
    let mut apply = |core: &mut ArmCore, offset: u32, sym: usize, kind: u32| -> Result<()> {
        let place = BASE + offset;
        match kind {
            R_ARM_RELATIVE => {
                let cur: u32 = wie_util::read_generic(core, place)?;
                write_generic(core, place, cur.wrapping_add(BASE))
            }
            R_ARM_GLOB_DAT | R_ARM_JUMP_SLOT | R_ARM_ABS32 => {
                let target = if undefined[sym] {
                    resolve_import(core, state, &mut stub_cache, &names[sym])?
                } else {
                    symbol_address[sym]
                };
                let value = if kind == R_ARM_ABS32 {
                    let addend: u32 = wie_util::read_generic(core, place)?;
                    target.wrapping_add(addend)
                } else {
                    target
                };
                write_generic(core, place, value)
            }
            other => Err(err(format!("지원하지 않는 재배치 종류 {other}"))),
        }
    };

    for shdr in file.section_headers().ok_or_else(|| err("섹션 헤더 없음"))?.iter() {
        if shdr.sh_type == abi::SHT_REL {
            for rel in file.section_data_as_rels(&shdr).map_err(err)? {
                apply(core, rel.r_offset as u32, rel.r_sym as usize, rel.r_type)?;
            }
        }
    }

    // 4) 정적 생성자 목록 (.init_array)
    let mut init_functions = Vec::new();
    if let Some(dynamic) = file.dynamic().map_err(err)? {
        let (mut array, mut size) = (0u32, 0u32);
        for d in dynamic.iter() {
            match d.d_tag {
                25 => array = d.d_ptr() as u32,
                27 => size = d.d_val() as u32,
                _ => {}
            }
        }
        for i in 0..size / 4 {
            let f: u32 = wie_util::read_generic(core, BASE + array + i * 4)?;
            if f != 0 && f != 0xffff_ffff {
                init_functions.push(f);
            }
        }
    }

    Ok(Library { base: BASE, exports, init_functions })
}

/// 가져다 쓰는 함수/변수를 호스트 구현으로 연결한다. 함수는 SVC 스텁 주소, 데이터는 새로 할당한 변수 주소.
fn resolve_import(core: &mut ArmCore, state: &Arc<Mutex<State>>, cache: &mut BTreeMap<String, u32>, name: &str) -> Result<u32> {
    if let Some(a) = cache.get(name) {
        return Ok(*a);
    }
    let address = match name {
        // 데이터 심볼
        "__stack_chk_guard" => {
            let a = wie_core_arm::Allocator::alloc(core, 8)?;
            write_generic(core, a, 0xdeadbeefu32)?;
            a
        }
        _ => {
            let id = {
                let mut st = state.lock();
                st.imports.push(name.to_string());
                (st.imports.len() - 1) as u32
            };
            core.make_svc_stub(CAT_LIBC, id)?
        }
    };
    cache.insert(name.to_string(), address);
    let _ = core.write_bytes(0, &[]); // (ByteWrite 사용 표시)
    Ok(address)
}
