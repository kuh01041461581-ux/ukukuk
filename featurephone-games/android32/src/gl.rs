//! 아주 작은 OpenGL ES 1.x 소프트웨어 구현.
//! 이 게임들은 화면 전체를 소프트웨어로 그려 텍스처로 올린 뒤, 텍스처를 입힌 사각형(삼각형 띠) 하나로 화면에 그린다.
//! 그 사용 범위(고정 파이프라인, 2D 직교 투영, 텍스처 + 색 곱)만 구현한다.

use std::collections::BTreeMap;

use wie_core_arm::ArmCore;
use wie_util::{ByteRead, Result, read_generic};

const GL_FLOAT: u32 = 0x1406;
const GL_RGB: u32 = 0x1907;
const GL_RGBA: u32 = 0x1908;
const GL_LUMINANCE: u32 = 0x1909;
const GL_ALPHA: u32 = 0x1906;
const GL_LUMINANCE_ALPHA: u32 = 0x190a;
const GL_UNSIGNED_BYTE: u32 = 0x1401;
const GL_UNSIGNED_SHORT_5_6_5: u32 = 0x8363;
const GL_UNSIGNED_SHORT_4_4_4_4: u32 = 0x8033;
const GL_UNSIGNED_SHORT_5_5_5_1: u32 = 0x8034;
const GL_BLEND: u32 = 0xbe2;
const GL_TEXTURE_2D: u32 = 0xde1;

#[derive(Default, Clone)]
pub struct Texture {
    pub width: usize,
    pub height: usize,
    /// RGBA8 (첫 줄 = GL 의 v=0)
    pub data: Vec<u8>,
    pub linear: bool,
    pub repeat: bool,
}

#[derive(Default, Clone, Copy)]
struct ArrayPointer {
    size: u32,
    kind: u32,
    stride: u32,
    pointer: u32,
}

pub struct Gl {
    pub textures: BTreeMap<u32, Texture>,
    bound: u32,
    vertex: ArrayPointer,
    texcoord: ArrayPointer,
    color: [f32; 4],
    clear: [f32; 4],
    viewport: (i32, i32, i32, i32),
    ortho: (f32, f32, f32, f32), // left, right, bottom, top
    blend: bool,
    texture_2d: bool,
    pub width: usize,
    pub height: usize,
    /// 화면(RGBA8, 위에서 아래로)
    pub frame: Vec<u8>,
    pub draws: u64,
    pub shader_sources: BTreeMap<u32, String>,
    next_object: u32,
    // ---- OpenGL ES 2.0 ----
    programs: BTreeMap<u32, Program>,
    cur_program: u32,
    attribs: [ArrayPointer; 8],
    attrib_enabled: [bool; 8],
    uniform_names: BTreeMap<u32, String>,
    fbos: BTreeMap<u32, u32>,
    cur_fbo: u32,
    blend_func: (u32, u32),
    scissor: (i32, i32, i32, i32),
    scissor_on: bool,
}

#[derive(Default, Clone)]
struct Program {
    shaders: Vec<u32>,
    attrib_index: BTreeMap<String, u32>,
    uniforms: BTreeMap<String, [f32; 16]>,
    /// 0 = 텍스처, 1 = 텍스처×Color, 2 = 단색(a_Color), 3 = 알파 마스크(Color.rgb, Color.a×tex.a)
    kind: u8,
    has_matrix: bool,
}

impl Default for Gl {
    fn default() -> Self {
        Self {
            textures: BTreeMap::new(),
            bound: 0,
            vertex: ArrayPointer::default(),
            texcoord: ArrayPointer::default(),
            color: [1.0; 4],
            clear: [0.0, 0.0, 0.0, 1.0],
            viewport: (0, 0, 0, 0),
            ortho: (0.0, 1.0, 0.0, 1.0),
            blend: false,
            texture_2d: false,
            width: 0,
            height: 0,
            frame: Vec::new(),
            draws: 0,
            shader_sources: BTreeMap::new(),
            next_object: 100,
            programs: BTreeMap::new(),
            cur_program: 0,
            attribs: [ArrayPointer::default(); 8],
            attrib_enabled: [false; 8],
            uniform_names: BTreeMap::new(),
            fbos: BTreeMap::new(),
            cur_fbo: 0,
            blend_func: (0x302, 0x303),
            scissor: (0, 0, 0, 0),
            scissor_on: false,
        }
    }
}

fn f(core: &ArmCore, i: usize) -> f32 {
    f32::from_bits(core.read_param(i).unwrap_or(0))
}

fn convert(format: u32, kind: u32, data: &[u8], pixels: usize) -> Vec<u8> {
    let mut out = vec![0u8; pixels * 4];
    for i in 0..pixels {
        let o = i * 4;
        let (r, g, b, a) = match (format, kind) {
            (GL_RGB, GL_UNSIGNED_BYTE) => (data[i * 3], data[i * 3 + 1], data[i * 3 + 2], 255),
            (GL_RGBA, GL_UNSIGNED_BYTE) => (data[i * 4], data[i * 4 + 1], data[i * 4 + 2], data[i * 4 + 3]),
            (GL_LUMINANCE, GL_UNSIGNED_BYTE) => (data[i], data[i], data[i], 255),
            (GL_ALPHA, GL_UNSIGNED_BYTE) => (255, 255, 255, data[i]),
            (GL_LUMINANCE_ALPHA, GL_UNSIGNED_BYTE) => (data[i * 2], data[i * 2], data[i * 2], data[i * 2 + 1]),
            (_, GL_UNSIGNED_SHORT_5_6_5) => {
                let v = u16::from_le_bytes([data[i * 2], data[i * 2 + 1]]);
                (((((v >> 11) & 31) as u32) * 255 / 31) as u8, ((((v >> 5) & 63) as u32) * 255 / 63) as u8, (((v & 31) as u32) * 255 / 31) as u8, 255)
            }
            (_, GL_UNSIGNED_SHORT_4_4_4_4) => {
                let v = u16::from_le_bytes([data[i * 2], data[i * 2 + 1]]);
                ((((v >> 12) & 15) * 17) as u8, (((v >> 8) & 15) * 17) as u8, (((v >> 4) & 15) * 17) as u8, ((v & 15) * 17) as u8)
            }
            (_, GL_UNSIGNED_SHORT_5_5_5_1) => {
                let v = u16::from_le_bytes([data[i * 2], data[i * 2 + 1]]);
                (((((v >> 11) & 31) as u32) * 255 / 31) as u8, ((((v >> 6) & 31) as u32) * 255 / 31) as u8, ((((v >> 1) & 31) as u32) * 255 / 31) as u8, if v & 1 == 1 { 255 } else { 0 })
            }
            _ => (255, 0, 255, 255),
        };
        out[o..o + 4].copy_from_slice(&[r, g, b, a]);
    }
    out
}

fn bytes_per_pixel(format: u32, kind: u32) -> usize {
    match kind {
        GL_UNSIGNED_SHORT_5_6_5 | GL_UNSIGNED_SHORT_4_4_4_4 | GL_UNSIGNED_SHORT_5_5_5_1 => 2,
        _ => match format {
            GL_RGB => 3,
            GL_RGBA => 4,
            GL_LUMINANCE_ALPHA => 2,
            _ => 1,
        },
    }
}

impl Gl {
    fn ensure_frame(&mut self) {
        let (_, _, w, h) = self.viewport;
        if w > 0 && h > 0 && (self.width != w as usize || self.height != h as usize) {
            self.width = w as usize;
            self.height = h as usize;
            self.frame = vec![0; self.width * self.height * 4];
        }
    }

    /// GL 함수를 처리한다. 처리했으면 Some(반환값)
    pub fn call(&mut self, core: &mut ArmCore, name: &str, next_texture: &mut u32) -> Result<Option<u32>> {
        let a = |core: &ArmCore, i: usize| core.read_param(i).unwrap_or(0);
        match name {
            "glViewport" => {
                self.viewport = (a(core, 0) as i32, a(core, 1) as i32, a(core, 2) as i32, a(core, 3) as i32);
                if self.cur_fbo == 0 { self.ensure_frame(); }
            }
            "glOrthof" => self.ortho = (f(core, 0), f(core, 1), f(core, 2), f(core, 3)),
            "glClearColor" => self.clear = [f(core, 0), f(core, 1), f(core, 2), f(core, 3)],
            "glClear" if self.cur_fbo != 0 => {
                let c = self.clear.map(|v| (v.clamp(0.0, 1.0) * 255.0) as u8);
                if let Some(tex) = self.fbos.get(&self.cur_fbo).copied().and_then(|t| self.textures.get_mut(&t)) {
                    for px in tex.data.chunks_exact_mut(4) { px.copy_from_slice(&c); }
                }
            }
            "glClear" => {
                self.ensure_frame();
                let c = self.clear.map(|v| (v.clamp(0.0, 1.0) * 255.0) as u8);
                for px in self.frame.chunks_exact_mut(4) {
                    px.copy_from_slice(&c);
                }
            }
            "glColor4f" => self.color = [f(core, 0), f(core, 1), f(core, 2), f(core, 3)],
            "glEnable" | "glDisable" => {
                let on = name == "glEnable";
                match a(core, 0) {
                    GL_BLEND => self.blend = on,
                    0xc11 => self.scissor_on = on,
                    GL_TEXTURE_2D => self.texture_2d = on,
                    _ => {}
                }
            }
            "glGenTextures" => {
                let (count, ptr) = (a(core, 0), a(core, 1));
                for i in 0..count {
                    let id = *next_texture;
                    *next_texture += 1;
                    self.textures.insert(id, Texture::default());
                    wie_util::write_generic(core, ptr + i * 4, id)?;
                }
            }
            "glDeleteTextures" => {
                let (count, ptr) = (a(core, 0), a(core, 1));
                for i in 0..count {
                    let id: u32 = read_generic(core, ptr + i * 4)?;
                    self.textures.remove(&id);
                }
            }
            "glBindTexture" => self.bound = a(core, 1),
            "glTexImage2D" => {
                // (target, level, internalformat, width, height, border, format, type, pixels)
                let (w, h, format, kind, pixels) = (a(core, 3) as usize, a(core, 4) as usize, a(core, 6), a(core, 7), a(core, 8));
                let data = if pixels == 0 {
                    vec![0u8; w * h * 4]
                } else {
                    let mut raw = vec![0u8; w * h * bytes_per_pixel(format, kind)];
                    core.read_bytes(pixels, &mut raw)?;
                    convert(format, kind, &raw, w * h)
                };
                self.textures.insert(self.bound, Texture { width: w, height: h, data, linear: self.textures.get(&self.bound).map_or(false, |t| t.linear), repeat: self.textures.get(&self.bound).map_or(true, |t| t.repeat) });
            }
            "glTexSubImage2D" => {
                // (target, level, xoffset, yoffset, width, height, format, type, pixels)
                let (xo, yo, w, h, format, kind, pixels) = (a(core, 2) as usize, a(core, 3) as usize, a(core, 4) as usize, a(core, 5) as usize, a(core, 6), a(core, 7), a(core, 8));
                let mut raw = vec![0u8; w * h * bytes_per_pixel(format, kind)];
                core.read_bytes(pixels, &mut raw)?;
                let rgba = convert(format, kind, &raw, w * h);
                if let Some(tex) = self.textures.get_mut(&self.bound) {
                    for row in 0..h {
                        if yo + row >= tex.height {
                            break;
                        }
                        let n = w.min(tex.width.saturating_sub(xo));
                        let dst = ((yo + row) * tex.width + xo) * 4;
                        tex.data[dst..dst + n * 4].copy_from_slice(&rgba[row * w * 4..row * w * 4 + n * 4]);
                    }
                }
            }
            "glVertexPointer" => self.vertex = ArrayPointer { size: a(core, 0), kind: a(core, 1), stride: a(core, 2), pointer: a(core, 3) },
            "glTexCoordPointer" => self.texcoord = ArrayPointer { size: a(core, 0), kind: a(core, 1), stride: a(core, 2), pointer: a(core, 3) },
            "glDrawArrays" => {
                if self.cur_program != 0 { self.draw2(core, a(core, 0), a(core, 1) as usize, a(core, 2) as usize, None)? } else { self.draw_arrays(core, a(core, 0), a(core, 1) as usize, a(core, 2) as usize)? }
            }
            "glDrawElements" => {
                let (mode, count, kind, ptr) = (a(core, 0), a(core, 1) as usize, a(core, 2), a(core, 3));
                let mut idx = Vec::with_capacity(count);
                for i in 0..count {
                    idx.push(if kind == 0x1403 { let v: u16 = read_generic(core, ptr + i as u32 * 2)?; v as usize } else { let v: u8 = read_generic(core, ptr + i as u32)?; v as usize });
                }
                self.draw2(core, mode, 0, count, Some(idx))?;
            }
            "glAttachShader" => { self.programs.entry(a(core, 0)).or_default().shaders.push(a(core, 1)); }
            "glBindAttribLocation" => {
                let name = String::from_utf8_lossy(&wie_util::read_null_terminated_string_bytes(core, a(core, 2))?).to_string();
                self.programs.entry(a(core, 0)).or_default().attrib_index.insert(name, a(core, 1));
            }
            "glLinkProgram" => {
                let prog = a(core, 0);
                let (mut kind, mut has_matrix) = (0u8, false);
                if let Some(p) = self.programs.get(&prog) {
                    for sh in &p.shaders {
                        let src = self.shader_sources.get(sh).cloned().unwrap_or_default();
                        if src.contains("projectionMatrix") { has_matrix = true; }
                        if src.contains("gl_FragColor") {
                            kind = if src.contains("a_Color") { 2 } else if src.contains("Color.a*clr.a") { 3 } else if src.contains("*Color") || src.contains("* Color") { 1 } else { 0 };
                        }
                    }
                }
                let p = self.programs.entry(prog).or_default();
                p.kind = kind;
                p.has_matrix = has_matrix;
            }
            "glUseProgram" => self.cur_program = a(core, 0),
            "glGetUniformLocation" => {
                let name = String::from_utf8_lossy(&wie_util::read_null_terminated_string_bytes(core, a(core, 1))?).to_string();
                self.next_object += 1;
                self.uniform_names.insert(self.next_object, format!("{}:{}", a(core, 0), name));
                return Ok(Some(self.next_object));
            }
            "glUniformMatrix4fv" => {
                let (loc, ptr) = (a(core, 0), a(core, 3));
                let mut m = [0f32; 16];
                for i in 0..16 { m[i] = f32::from_bits(read_generic(core, ptr + i as u32 * 4)?); }
                if a(core, 2) != 0 { let t = m; for r in 0..4 { for c in 0..4 { m[c * 4 + r] = t[r * 4 + c]; } } }
                if let Some((prog, name)) = self.uniform_names.get(&loc).and_then(|n| n.split_once(':')).map(|(p, n)| (p.parse::<u32>().unwrap_or(0), n.to_string())) {
                    self.programs.entry(prog).or_default().uniforms.insert(name, m);
                }
            }
            "glUniform4f" => {
                let loc = a(core, 0);
                let v = [f(core, 1), f(core, 2), f(core, 3), f(core, 4)];
                if let Some((prog, name)) = self.uniform_names.get(&loc).and_then(|n| n.split_once(':')).map(|(p, n)| (p.parse::<u32>().unwrap_or(0), n.to_string())) {
                    let mut m = [0f32; 16];
                    m[..4].copy_from_slice(&v);
                    self.programs.entry(prog).or_default().uniforms.insert(name, m);
                }
            }
            "glVertexAttribPointer" => {
                let i = a(core, 0) as usize;
                if i < 8 { self.attribs[i] = ArrayPointer { size: a(core, 1), kind: a(core, 2), stride: a(core, 4), pointer: a(core, 5) }; }
            }
            "glEnableVertexAttribArray" | "glDisableVertexAttribArray" => {
                let i = a(core, 0) as usize;
                if i < 8 { self.attrib_enabled[i] = name == "glEnableVertexAttribArray"; }
            }
            "glBindFramebuffer" => { self.cur_fbo = a(core, 1); }
            "glFramebufferTexture2D" => { self.fbos.insert(self.cur_fbo, a(core, 3)); }
            "glDeleteFramebuffers" => {}
            "glBlendFunc" => self.blend_func = (a(core, 0), a(core, 1)),
            "glScissor" => self.scissor = (a(core, 0) as i32, a(core, 1) as i32, a(core, 2) as i32, a(core, 3) as i32),
            "glTexParameteri" | "glTexParameterf" => {
                let (pname, param) = (a(core, 1), if name == "glTexParameterf" { f(core, 2) as u32 } else { a(core, 2) });
                if let Some(t) = self.textures.get_mut(&self.bound) {
                    match pname {
                        0x2800 | 0x2801 => t.linear = param != 0x2600 && param != 0x2700 && param != 0x2702,
                        0x2802 | 0x2803 => t.repeat = param == 0x2901,
                        _ => {}
                    }
                }
            }
            "glGetIntegerv" => {
                let (pname, ptr) = (a(core, 0), a(core, 1));
                match pname {
                    0x0d33 => wie_util::write_generic(core, ptr, 4096u32)?,
                    0x8ca6 => wie_util::write_generic(core, ptr, self.cur_fbo)?,
                    0x0ba2 => { for (i, v) in [self.viewport.0, self.viewport.1, self.viewport.2, self.viewport.3].iter().enumerate() { wie_util::write_generic(core, ptr + i as u32 * 4, *v as u32)?; } }
                    _ => wie_util::write_generic(core, ptr, 0u32)?,
                }
            }
            "glShaderSource" => {
                let (shader, count, strings) = (a(core, 0), a(core, 1), a(core, 2));
                let mut text = String::new();
                for i in 0..count {
                    let p: u32 = read_generic(core, strings + i * 4)?;
                    text.push_str(&String::from_utf8_lossy(&wie_util::read_null_terminated_string_bytes(core, p)?));
                }
                if std::env::var("TRACE_GL").is_ok() { eprintln!("[shader {shader}]\n{text}\n[/shader]"); }
                self.shader_sources.insert(shader, text);
            }
            "glCreateShader" | "glCreateProgram" => { self.next_object += 1; return Ok(Some(self.next_object)); }
            "glGetShaderiv" | "glGetProgramiv" => {
                // pname: COMPILE_STATUS 0x8b81, LINK_STATUS 0x8b82
                wie_util::write_generic(core, a(core, 2), 1u32)?;
            }
            "glCheckFramebufferStatus" => return Ok(Some(0x8cd5)),
            "glGenFramebuffers" | "glGenRenderbuffers" => {
                let (count, ptr) = (a(core, 0), a(core, 1));
                for i in 0..count { self.next_object += 1; wie_util::write_generic(core, ptr + i * 4, self.next_object)?; }
            }
            "glIsTexture" => return Ok(Some(1)),
            _ => {} // MatrixMode, LoadIdentity, CullFace, Enable/DisableClientState, TexParameterf 등은 무시
        }
        Ok(Some(0))
    }

    fn fetch(&self, core: &mut ArmCore, p: ArrayPointer, index: usize) -> Result<[f32; 2]> {
        let stride = if p.stride == 0 { p.size * 4 } else { p.stride };
        let at = p.pointer + index as u32 * stride;
        if p.kind != GL_FLOAT {
            return Ok([0.0, 0.0]);
        }
        let x: f32 = f32::from_bits(read_generic(core, at)?);
        let y: f32 = f32::from_bits(read_generic(core, at + 4)?);
        Ok([x, y])
    }

    fn draw_arrays(&mut self, core: &mut ArmCore, mode: u32, first: usize, count: usize) -> Result<()> {
        self.draws += 1;
        self.ensure_frame();
        if self.width == 0 || count < 3 {
            return Ok(());
        }
        let mut verts = Vec::new();
        for i in first..first + count {
            let pos = self.fetch(core, self.vertex, i)?;
            let uv = if self.texture_2d { self.fetch(core, self.texcoord, i)? } else { [0.0, 0.0] };
            verts.push((pos, uv));
        }
        // 삼각형 목록으로 바꾼다 (4: TRIANGLES, 5: TRIANGLE_STRIP, 6: TRIANGLE_FAN)
        let mut triangles: Vec<[usize; 3]> = Vec::new();
        match mode {
            4 => (0..verts.len() / 3).for_each(|t| triangles.push([t * 3, t * 3 + 1, t * 3 + 2])),
            5 => (0..verts.len() - 2).for_each(|t| triangles.push(if t % 2 == 0 { [t, t + 1, t + 2] } else { [t + 1, t, t + 2] })),
            6 => (1..verts.len() - 1).for_each(|t| triangles.push([0, t, t + 1])),
            _ => return Ok(()),
        }
        let (l, r, b, t) = self.ortho;
        let (vx, vy, vw, vh) = self.viewport;
        let to_screen = |p: [f32; 2]| -> [f32; 2] {
            let nx = (2.0 * p[0] - (r + l)) / (r - l);
            let ny = (2.0 * p[1] - (t + b)) / (t - b);
            [vx as f32 + (nx + 1.0) * 0.5 * vw as f32, vy as f32 + (ny + 1.0) * 0.5 * vh as f32]
        };
        let tex = if self.texture_2d { self.textures.get(&self.bound).cloned() } else { None };
        let color = self.color;
        for tri in triangles {
            let p: Vec<[f32; 2]> = tri.iter().map(|&i| to_screen(verts[i].0)).collect();
            let uv: Vec<[f32; 2]> = tri.iter().map(|&i| verts[i].1).collect();
            let min_x = p.iter().map(|v| v[0]).fold(f32::MAX, f32::min).floor().max(0.0) as i32;
            let max_x = p.iter().map(|v| v[0]).fold(f32::MIN, f32::max).ceil().min(self.width as f32) as i32;
            let min_y = p.iter().map(|v| v[1]).fold(f32::MAX, f32::min).floor().max(0.0) as i32;
            let max_y = p.iter().map(|v| v[1]).fold(f32::MIN, f32::max).ceil().min(self.height as f32) as i32;
            let area = (p[1][0] - p[0][0]) * (p[2][1] - p[0][1]) - (p[2][0] - p[0][0]) * (p[1][1] - p[0][1]);
            if area.abs() < 1e-6 {
                continue;
            }
            for y in min_y..max_y {
                for x in min_x..max_x {
                    let (px, py) = (x as f32 + 0.5, y as f32 + 0.5);
                    let w0 = ((p[1][0] - px) * (p[2][1] - py) - (p[2][0] - px) * (p[1][1] - py)) / area;
                    let w1 = ((p[2][0] - px) * (p[0][1] - py) - (p[0][0] - px) * (p[2][1] - py)) / area;
                    let w2 = 1.0 - w0 - w1;
                    if w0 < 0.0 || w1 < 0.0 || w2 < 0.0 {
                        continue;
                    }
                    let mut rgba = color;
                    if let Some(tex) = &tex {
                        if tex.width > 0 && tex.height > 0 {
                            let u = w0 * uv[0][0] + w1 * uv[1][0] + w2 * uv[2][0];
                            let v = w0 * uv[0][1] + w1 * uv[1][1] + w2 * uv[2][1];
                            let tx = ((u * tex.width as f32) as i32).clamp(0, tex.width as i32 - 1) as usize;
                            let ty = ((v * tex.height as f32) as i32).clamp(0, tex.height as i32 - 1) as usize;
                            let o = (ty * tex.width + tx) * 4;
                            for c in 0..4 {
                                rgba[c] *= tex.data[o + c] as f32 / 255.0;
                            }
                        }
                    }
                    // GL 은 아래가 0 이므로 위아래를 뒤집어 저장한다
                    let row = self.height - 1 - y as usize;
                    let o = (row * self.width + x as usize) * 4;
                    if self.blend && rgba[3] < 1.0 {
                        for c in 0..3 {
                            let dst = self.frame[o + c] as f32 / 255.0;
                            self.frame[o + c] = ((rgba[c] * rgba[3] + dst * (1.0 - rgba[3])).clamp(0.0, 1.0) * 255.0) as u8;
                        }
                    } else {
                        for c in 0..3 {
                            self.frame[o + c] = (rgba[c].clamp(0.0, 1.0) * 255.0) as u8;
                        }
                    }
                    self.frame[o + 3] = 255;
                }
            }
        }
        Ok(())
    }

    fn attrib_f(&self, core: &mut ArmCore, i: usize, vertex: usize, n: usize) -> Result<[f32; 4]> {
        let p = self.attribs[i];
        let stride = if p.stride == 0 { p.size * 4 } else { p.stride };
        let at = p.pointer + vertex as u32 * stride;
        let mut out = [0.0, 0.0, 0.0, 1.0];
        for k in 0..n.min(p.size as usize) {
            out[k] = match p.kind {
                GL_FLOAT => f32::from_bits(read_generic(core, at + k as u32 * 4)?),
                0x1403 => { let v: u16 = read_generic(core, at + k as u32 * 2)?; v as f32 }
                0x1401 => { let v: u8 = read_generic(core, at + k as u32)?; v as f32 }
                _ => 0.0,
            };
        }
        Ok(out)
    }

    fn draw2(&mut self, core: &mut ArmCore, mode: u32, first: usize, count: usize, indices: Option<Vec<usize>>) -> Result<()> {
        self.draws += 1;
        let Some(prog) = self.programs.get(&self.cur_program).cloned() else { return Ok(()) };
        let order: Vec<usize> = match indices { Some(i) => i, None => (first..first + count).collect() };
        if order.len() < 3 { return Ok(()); }
        let pos_i = prog.attrib_index.get("a_Position").copied().unwrap_or(0) as usize;
        let uv_i = prog.attrib_index.get("a_TexCoord").copied().unwrap_or(1) as usize;
        let mat = prog.uniforms.get("projectionMatrix").copied().unwrap_or([1., 0., 0., 0., 0., 1., 0., 0., 0., 0., 1., 0., 0., 0., 0., 1.]);
        let color = if prog.kind == 2 { prog.uniforms.get("a_Color") } else { prog.uniforms.get("Color") }.map(|m| [m[0], m[1], m[2], m[3]]).unwrap_or([1.0; 4]);
        // 꼭짓점: (화면 좌표, uv)
        let (vx, vy, vw, vh) = self.viewport;
        let mut verts: Vec<([f32; 2], [f32; 2])> = Vec::with_capacity(order.len());
        for &vi in &order {
            let p = self.attrib_f(core, pos_i, vi, 3)?;
            let uv = if prog.kind != 2 && self.attrib_enabled[uv_i] { self.attrib_f(core, uv_i, vi, 2)? } else { [0.0; 4] };
            let (cx, cy, cw) = if prog.has_matrix {
                (mat[0] * p[0] + mat[4] * p[1] + mat[8] * p[2] + mat[12], mat[1] * p[0] + mat[5] * p[1] + mat[9] * p[2] + mat[13], mat[3] * p[0] + mat[7] * p[1] + mat[11] * p[2] + mat[15])
            } else { (p[0], p[1], 1.0) };
            let cw = if cw.abs() < 1e-9 { 1.0 } else { cw };
            let (nx, ny) = (cx / cw, cy / cw);
            verts.push(([vx as f32 + (nx + 1.0) * 0.5 * vw as f32, vy as f32 + (ny + 1.0) * 0.5 * vh as f32], [uv[0], uv[1]]));
        }
        let mut triangles: Vec<[usize; 3]> = Vec::new();
        match mode {
            4 => (0..verts.len() / 3).for_each(|t| triangles.push([t * 3, t * 3 + 1, t * 3 + 2])),
            5 => (0..verts.len() - 2).for_each(|t| triangles.push(if t % 2 == 0 { [t, t + 1, t + 2] } else { [t + 1, t, t + 2] })),
            6 => (1..verts.len() - 1).for_each(|t| triangles.push([0, t, t + 1])),
            _ => return Ok(()),
        }
        // 대상: 화면 또는 FBO 텍스처
        let fbo_tex = if self.cur_fbo != 0 { self.fbos.get(&self.cur_fbo).copied() } else { None };
        let (mut target, tw, th, flip) = match fbo_tex {
            Some(t) => match self.textures.get_mut(&t) { Some(tx) => (std::mem::take(&mut tx.data), tx.width, tx.height, false), None => return Ok(()) },
            None => { self.ensure_frame(); (std::mem::take(&mut self.frame), self.width, self.height, true) }
        };
        let tex = if prog.kind != 2 { self.textures.get(&self.bound).cloned() } else { None };
        let blend = self.blend;
        let (sf, df) = self.blend_func;
        let scissor = if self.scissor_on { Some(self.scissor) } else { None };
        let factor = |f: u32, src: [f32; 4], dst: [f32; 4]| -> [f32; 4] {
            match f { 0 => [0.0; 4], 1 => [1.0; 4], 0x300 => src, 0x301 => [1.0 - src[0], 1.0 - src[1], 1.0 - src[2], 1.0 - src[3]], 0x302 => [src[3]; 4], 0x303 => [1.0 - src[3]; 4], 0x304 => [dst[3]; 4], 0x305 => [1.0 - dst[3]; 4], _ => [1.0; 4] }
        };
        if tw > 0 && th > 0 {
            for tri in triangles {
                let p: Vec<[f32; 2]> = tri.iter().map(|&i| verts[i].0).collect();
                let uv: Vec<[f32; 2]> = tri.iter().map(|&i| verts[i].1).collect();
                let area = (p[1][0] - p[0][0]) * (p[2][1] - p[0][1]) - (p[2][0] - p[0][0]) * (p[1][1] - p[0][1]);
                if area.abs() < 1e-6 { continue; }
                let min_x = p.iter().map(|v| v[0]).fold(f32::MAX, f32::min).floor().max(0.0) as i32;
                let max_x = p.iter().map(|v| v[0]).fold(f32::MIN, f32::max).ceil().min(tw as f32) as i32;
                let min_y = p.iter().map(|v| v[1]).fold(f32::MAX, f32::min).floor().max(0.0) as i32;
                let max_y = p.iter().map(|v| v[1]).fold(f32::MIN, f32::max).ceil().min(th as f32) as i32;
                for y in min_y..max_y {
                    for x in min_x..max_x {
                        if let Some((sx, sy, sw, sh)) = scissor { if x < sx || y < sy || x >= sx + sw || y >= sy + sh { continue; } }
                        let (px, py) = (x as f32 + 0.5, y as f32 + 0.5);
                        let w0 = ((p[1][0] - px) * (p[2][1] - py) - (p[2][0] - px) * (p[1][1] - py)) / area;
                        let w1 = ((p[2][0] - px) * (p[0][1] - py) - (p[0][0] - px) * (p[2][1] - py)) / area;
                        let w2 = 1.0 - w0 - w1;
                        if w0 < -1e-4 || w1 < -1e-4 || w2 < -1e-4 { continue; }
                        let mut texel = [1.0f32; 4];
                        if let Some(t) = &tex {
                            if t.width > 0 && t.height > 0 {
                                let u = w0 * uv[0][0] + w1 * uv[1][0] + w2 * uv[2][0];
                                let v = w0 * uv[0][1] + w1 * uv[1][1] + w2 * uv[2][1];
                                texel = sample(t, u, v);
                            }
                        }
                        let src = match prog.kind {
                            0 => texel,
                            1 => [texel[0] * color[0], texel[1] * color[1], texel[2] * color[2], texel[3] * color[3]],
                            2 => color,
                            _ => [color[0], color[1], color[2], color[3] * texel[3]],
                        };
                        let row = if flip { th - 1 - y as usize } else { y as usize };
                        let o = (row * tw + x as usize) * 4;
                        let dst = [target[o] as f32 / 255.0, target[o + 1] as f32 / 255.0, target[o + 2] as f32 / 255.0, target[o + 3] as f32 / 255.0];
                        let out = if blend {
                            let (a, b) = (factor(sf, src, dst), factor(df, src, dst));
                            [src[0] * a[0] + dst[0] * b[0], src[1] * a[1] + dst[1] * b[1], src[2] * a[2] + dst[2] * b[2], src[3] * a[3] + dst[3] * b[3]]
                        } else { src };
                        for c in 0..4 { target[o + c] = (out[c].clamp(0.0, 1.0) * 255.0 + 0.5) as u8; }
                    }
                }
            }
        }
        match fbo_tex {
            Some(t) => { if let Some(tx) = self.textures.get_mut(&t) { tx.data = target; } }
            None => self.frame = target,
        }
        Ok(())
    }
}

fn sample(t: &Texture, u: f32, v: f32) -> [f32; 4] {
    let wrap = |c: f32, n: usize| -> f32 { (if t.repeat { c - c.floor() } else { c.clamp(0.0, 1.0) }) * n as f32 };
    let (fx, fy) = (wrap(u, t.width), wrap(v, t.height));
    let at = |x: i64, y: i64| -> [f32; 4] {
        let (x, y) = if t.repeat { (x.rem_euclid(t.width as i64), y.rem_euclid(t.height as i64)) } else { (x.clamp(0, t.width as i64 - 1), y.clamp(0, t.height as i64 - 1)) };
        let o = (y as usize * t.width + x as usize) * 4;
        [t.data[o] as f32 / 255.0, t.data[o + 1] as f32 / 255.0, t.data[o + 2] as f32 / 255.0, t.data[o + 3] as f32 / 255.0]
    };
    if !t.linear { return at(fx as i64, fy as i64); }
    let (gx, gy) = (fx - 0.5, fy - 0.5);
    let (x0, y0) = (gx.floor(), gy.floor());
    let (tx, ty) = (gx - x0, gy - y0);
    let (a, b, c, d) = (at(x0 as i64, y0 as i64), at(x0 as i64 + 1, y0 as i64), at(x0 as i64, y0 as i64 + 1), at(x0 as i64 + 1, y0 as i64 + 1));
    let mut out = [0.0; 4];
    for i in 0..4 { out[i] = (a[i] * (1.0 - tx) + b[i] * tx) * (1.0 - ty) + (c[i] * (1.0 - tx) + d[i] * tx) * ty; }
    out
}
