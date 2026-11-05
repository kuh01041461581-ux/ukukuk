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
    /// RGBA8
    pub data: Vec<u8>,
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
                self.ensure_frame();
            }
            "glOrthof" => self.ortho = (f(core, 0), f(core, 1), f(core, 2), f(core, 3)),
            "glClearColor" => self.clear = [f(core, 0), f(core, 1), f(core, 2), f(core, 3)],
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
                self.textures.insert(self.bound, Texture { width: w, height: h, data });
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
            "glDrawArrays" => self.draw_arrays(core, a(core, 0), a(core, 1) as usize, a(core, 2) as usize)?,
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
}
