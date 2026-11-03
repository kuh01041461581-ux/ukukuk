"""서울타이쿤2 WIPI(Java AOT) 호스트 런타임 — 에뮬레이터 위에 자바 라이브러리/WIPI API 를 파이썬으로 구현.
사용: python3 tools/wipi_rt.py work/st2/client.bin1116 work/st2 [출력폴더]
"""
import sys, os, struct, time, random as _random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import wipi_emu
from wipi_emu import *
from PIL import Image as PImage, ImageDraw, ImageFont

IMG, RES, OUT = sys.argv[1], sys.argv[2], (sys.argv[3] if len(sys.argv) > 3 else '/tmp/st2out')
os.makedirs(OUT, exist_ok=True)
SCREEN_W, SCREEN_H = 240, 320
e = Emu(IMG, verbose=True)
RETT = wipi_emu.RETT
ST = {}            # 핸들 -> 파이썬 상태
LOG = []
TRACE = bool(os.environ.get('TRACE'))

# ------------------------------------------------------------------ 인터페이스(env / knl / JB)
ifaces = {}
def lookup(e, a, s):
    name = e.cstr(a[0]).decode()
    if name not in ifaces:
        ifaces[name] = e.iface(name, 80)
        if name == 'WIPIC_knlInterface':
            tbl = e.iface('clib', 24); e.impl['WIPIC_knlInterface[33]'] = lambda e, a, s: tbl
    return ifaces[name]
env = e.iface('env', 64); e.w32(env, e.host('env.lookup', lookup))

# ------------------------------------------------------------------ 클래스 / 메소드 합성
IMPL = {}
def impl(cls, sig):
    def deco(f): IMPL[cls + '::' + sig] = f; return f
    return deco
STRTAB = BASE + 0x9cac0
def strtab(i): return e.cstr(e.u32(STRTAB + 4 * i), 300)
img_classes = {}
for i in range(193):
    Cx = BASE + 0xac6c4 + i * 0x14; Ix = e.u32(Cx + 8)
    if BASE <= Ix < BASE + 0x600000 and BASE <= e.u32(Ix) < BASE + 0x600000:
        img_classes[e.cstr(e.u32(Ix), 120).decode('latin1')] = Cx
def in_heap_or_img(p): return (BASE <= p < BASE + 0x600000) or (0x2000000 <= p < 0x3800000)
def propagate(hostC, vidx, M):
    for nm, Cx in img_classes.items():
        c = Cx; depth = 0
        while c and depth < 16:
            if c == hostC and Cx != hostC:
                vt = e.u32(Cx + 0xc)
                if vt: e.w32(vt + 4 * vidx, M)
                break
            sup = e.u32(e.u32(c + 8) + 8)
            c = sup if (sup and not (sup & 1) and in_heap_or_img(sup)) else 0
            depth += 1
class HClass:
    def __init__(self, name, vt=64, size=0):
        self.name = name; self.methods = {}; self.reserved = set()
        self.C = e.alloc_low(0x40); self.I = e.alloc_low(0x40)
        nm = e.alloc_low(len(name) + 1); e.mu.mem_write(nm, name.encode() + b'\0')
        e.w32(self.I, nm); e.mu.mem_write(self.I + 0x1a, struct.pack('<H', size))
        e.w32(self.C + 8, self.I)
        e.mu.mem_write(self.C + 0x10, struct.pack('<H', vt)); e.mu.mem_write(self.C + 0x12, struct.pack('<H', 0x80))
        self.vtab = e.alloc_low(4 * max(vt, 1)); e.w32(self.C + 0xc, self.vtab); self.nextv = 0
    def method(self, nameptr, key, vidx=None):
        if key not in self.methods:
            M = e.alloc_low(0x20); hn = 'M:%s.%s' % (self.name, key); fn = e.host(hn)
            ik = self.name + '::' + key
            e.impl[hn] = make_caller(ik, key)
            if ')' in key: RETT[hn] = key[key.index(')') + 1]
            if vidx is None:
                while self.nextv in self.reserved: self.nextv += 1
                vidx = self.nextv; self.nextv += 1
            hid = 0x100 + len(NAT); NAT[hid] = (ik, key); e.w32(M + 8, hid)
            e.w32(M, fn); e.w32(M + 4, self.C); e.w32(M + 0xc, nameptr)
            e.mu.mem_write(M + 0x14, struct.pack('<HH', vidx, 1)); self.methods[key] = M; e.w32(self.vtab + 4 * vidx, M)
            propagate(self.C, vidx, M)
        return self.methods[key]
NAT = {}
resbuf = None
hclasses = {}
def hclass(name):
    if name not in hclasses:
        if name == 'org/kwis/msp/lcdui/Jlet':
            hc = HClass(name); hc.reserved = set(range(0, 19)); hc.nextv = 19; hclasses[name] = hc
            seoul_I = e.u32(BASE + 0xac804 + 8)
            for k in range(19):
                M = e.alloc_low(0x20); e.w32(M, e.host('M:Jlet.vt%d' % k)); e.w32(M + 4, hc.C)
                if 15 <= k <= 18: e.w32(M + 0xc, e.u32(e.u32(e.u32(seoul_I + 0xc) + 4 * (k - 14)) + 0xc))
                else:
                    nmp = e.alloc_low(16); e.mu.mem_write(nmp, b'Z()V+vt%d\0' % k); e.w32(M + 0xc, nmp)
                e.mu.mem_write(M + 0x14, struct.pack('<HH', k, 1)); e.w32(hc.vtab + 4 * k, M)
        else: hclasses[name] = HClass(name)
    return hclasses[name]
def classfor(name): return img_classes[name] if name in img_classes else hclass(name).C

# ------------------------------------------------------------------ 객체 / 배열 / 문자열
def new_obj(C):
    size = struct.unpack('<H', bytes(e.mu.mem_read(e.u32(C + 8) + 0x1a, 2)))[0]
    body = e.alloc(4 + size + 8); e.w32(body, (C << 5) & 0xffffffff)
    h = e.alloc(8); e.w32(h, body); return h
AR = {}
def new_array(C, n, esz):
    if n * esz > 4_000_000 or n < 0:
        print('  !!! huge array', n, esz, 'lr=%#x' % (e.mu.reg_read(UC_ARM_REG_LR) - BASE)); e.mu.emu_stop(); e.threw = True; n = 0
    body = e.alloc(8 + n * esz + 8); e.w32(body, (C << 5) & 0xffffffff); e.w32(body + 4, n)
    h = e.alloc(8); e.w32(h, body); AR[h] = (body, n, esz); return h
def arr_info(h):
    if h in AR: return AR[h]
    body = e.u32(h); n = e.u32(body + 4); return (body, n, 1)
def jstr(s):
    h = new_obj(classfor('java/lang/String')); ST[h] = s; return h
def pystr(h): return ST.get(h, '') if h else None
def byte_array(data):
    h = new_array(classfor('[B'), len(data), 1); body = AR[h][0]; e.mu.mem_write(body + 8, bytes(data)); return h
def array_bytes(h):
    body, n, esz = arr_info(h); return bytes(e.mu.mem_read(body + 8, n * esz))
def hobj_of(name):
    return new_obj(classfor(name))
singleton = {}
def single(name):
    if name not in singleton: singleton[name] = hobj_of(name)
    return singleton[name]
def cls_of(h): return (e.u32(e.u32(h)) >> 5) & 0xffffffff
def cname_of(h):
    try:
        C = cls_of(h); return e.cstr(e.u32(e.u32(C + 8)), 120).decode('latin1')
    except Exception: return '?'

# ------------------------------------------------------------------ 호출 인자 해석
def parse_sig(key):
    d = key[1:key.index(')')]; out = []; i = 0
    while i < len(d):
        c = d[i]
        if c in 'JD': out.append(c); i += 1
        elif c == 'L': j = d.index(';', i); out.append('L'); i = j + 1
        elif c == '[':
            while d[i] == '[': i += 1
            if d[i] == 'L': i = d.index(';', i) + 1
            else: i += 1
            out.append('L')
        else: out.append(c); i += 1
    return out
def make_caller(ik, key):
    fn = IMPL.get(ik)
    types = parse_sig(key)
    def call(e, a, s):
        regs = list(a) + list(s)
        static = ik in STATIC
        i = 1; this = None
        if not static: this = regs[1]; i = 2
        args = []
        for t in types:
            if t in 'JD':
                if i % 2: i += 1
                v = regs[i] | (regs[i + 1] << 32)
                if t == 'J' and v >> 63: v -= 1 << 64
                args.append(v); i += 2
            else:
                v = regs[i]; i += 1
                if t in 'IZBSC' and v >> 31: v -= 1 << 32
                args.append(v)
        if fn is None:
            if e.verbose: print('  [UNIMPL] %s this=%s args=%s' % (ik, hex(this) if this else None, [hex(x) if isinstance(x, int) and x > 255 else x for x in args]))
            return 0
        if TRACE: print('  [call] %s this=%s args=%s' % (ik, hex(this) if this else None, [hex(x) if isinstance(x, int) and x > 255 else x for x in args]))
        r = fn(this, *args) if not static else fn(*args)
        return r if r is not None else 0
    return call
STATIC = set()
def impl_s(cls, sig):
    def deco(f): IMPL[cls + '::' + sig] = f; STATIC.add(cls + '::' + sig); return f
    return deco

# ------------------------------------------------------------------ JB 표
fcount = {}
def jb_resolve(e, a, s):
    slot, cb = a[0], a[1]; w = e.u32(slot)
    if not (w & 1): return new_obj(slot)
    if cb == BASE + 0x7b4d5:
        e.w32(slot, classfor(strtab(w >> 1).decode('latin1'))); return 0
    cname = strtab((w & 0xffff) >> 1).decode(); mem = strtab(w >> 16)
    nameptr = e.u32(STRTAB + 4 * (w >> 16)); key = mem[1:].decode('latin1')
    if cb == BASE + 0x7b469:
        F = e.alloc_low(0x20); n = fcount.get(cname, 0); fcount[cname] = n + 1
        e.w32(F + 0xc, 4 * n); e.w32(F + 4, classfor(cname)); e.w32(slot, F); return 0
    M = hclass(cname).method(nameptr, key); e.w32(slot, M)
    if e.verbose and (cname + '::' + key) not in IMPL: print('  resolve(미구현) %s :: %s' % (cname, key))
    return 0
PRIM = {4: ('[Z', 1), 5: ('[C', 2), 6: ('[F', 4), 7: ('[D', 8), 8: ('[B', 1), 9: ('[S', 2), 10: ('[I', 4), 11: ('[J', 8)}
primtab = e.alloc(0x40)
for atype, (nm, esz) in PRIM.items(): e.w32(primtab + atype * 4, classfor(nm))
def esz_of_class(C):
    nm = e.cstr(e.u32(e.u32(C + 8)), 60).decode('latin1')
    return {'[Z': 1, '[B': 1, '[C': 2, '[S': 2, '[I': 4, '[F': 4, '[J': 8, '[D': 8}.get(nm, 4)
def jb_throw(e, a, s):
    if a[0] < 0x100:                                         # (atype*4, length, 콜백) = 기본형 배열 생성
        C = e.u32(primtab + a[0]); return new_array(C, a[1], PRIM[a[0] >> 2][1])
    print('  !!! THROW', e.cstr(a[0]).decode(), 'lr=%#x' % (e.mu.reg_read(UC_ARM_REG_LR) - BASE)); e.mu.emu_stop(); e.threw = True; return 0
e.threw = False
e.impl['WIPI_JBInterface[1]'] = jb_resolve
e.impl['WIPI_JBInterface[2]'] = jb_throw
e.impl['WIPI_JBInterface[6]'] = lambda e, a, s: new_array(a[0], a[1], esz_of_class(a[0]))
e.impl['WIPI_JBInterface[10]'] = lambda e, a, s: 0
def jb_str(e, a, s):
    n = struct.unpack('<H', bytes(e.mu.mem_read(a[0], 2)))[0]            # u16 길이 + UTF-16LE
    return jstr(bytes(e.mu.mem_read(a[0] + 2, 2 * n)).decode('utf-16le', 'replace'))
e.impl['WIPI_JBInterface[11]'] = jb_str
def jb12(e, a, s):
    """JB[12](nativeHandle, argsptr): 호스트 네이티브(정적/기타) 메소드 호출. 인자는 메모리 배열, 결과는 8바이트 버퍼 포인터로 반환"""
    global resbuf
    if resbuf is None: resbuf = e.alloc(16)
    h = a[0]
    if h not in NAT: return a[0]                       # 알 수 없으면 주소 그대로(8바이트 읽기 도우미 역할)
    ik, key = NAT[h]; fn = IMPL.get(ik); static = ik in STATIC
    words = [e.u32(a[1] + 4 * i) for i in range(10)]
    types = parse_sig(key); i = 0; this = None; args = []
    if not static: this = words[0]; i = 1
    for t in types:
        if t in 'JD': v = words[i] | (words[i + 1] << 32); i += 2
        else: v = words[i]; i += 1
        if t in 'IZBSC' and v >> 31: v -= 1 << 32
        if t == 'J' and v >> 63: v -= 1 << 64
        args.append(v)
    if TRACE: print('  [native] %s this=%s args=%s' % (ik, hex(this) if this else None, args))
    if fn is None:
        if e.verbose: print('  [UNIMPL-native] %s args=%s' % (ik, args))
        r = 0
    else: r = (fn(this, *args) if not static else fn(*args)) or 0
    e.mu.mem_write(resbuf, struct.pack('<Q', r & (2**64 - 1)))
    return resbuf
e.impl['WIPI_JBInterface[12]'] = jb12
def link(e, a, s):
    nm = e.cstr(a[1], 120).decode('latin1'); e.w32(a[0], classfor(nm)); return 0
e.impl['env[8]'] = link
e.impl['env[11]'] = lambda e, a, s: e.alloc(a[0])
scratch = e.alloc(256)
root = e.alloc(64); l2 = e.alloc(64); l3 = e.alloc(256); e.w32(root, l2); e.w32(l2, l3)
r = e.run(BASE + 0x7b1ec, [scratch, root, 0, primtab], [env]); assert r == 0
r = e.run(BASE + 0x7b02c, [0, 0, 0, 0]); assert r == 0, hex(r)

# ================================================================== 자바 라이브러리
def res(name):
    name = name.lstrip('/')
    p = os.path.join(RES, name)
    return open(p, 'rb').read() if os.path.exists(p) else None

@impl('java/lang/Object', '()V+<init>')
def _(this): pass
@impl('java/lang/Object', '()I+hashCode')
def _(this): return this
@impl('java/lang/Object', '(Ljava/lang/Object;)Z+equals')
def _(this, o): return 1 if this == o else 0
@impl('java/lang/Object', '()Ljava/lang/Class;+getClass')
def _(this):
    h = hobj_of('java/lang/Class'); ST[h] = ('class', cls_of(this)); return h
@impl('java/lang/Object', '()Ljava/lang/String;+toString')
def _(this): return jstr(cname_of(this) + '@' + hex(this))
for n in ('()V+notify', '()V+notifyAll'):
    IMPL['java/lang/Object::' + n] = lambda this, *a: None
# 문자열
@impl('java/lang/String', '([B)V+<init>')
def _(this, b): ST[this] = array_bytes(b).decode('cp949', 'replace')
@impl('java/lang/String', '([BII)V+<init>')
def _(this, b, o, l): ST[this] = array_bytes(b)[o:o + l].decode('cp949', 'replace')
@impl('java/lang/String', '()I+length')
def _(this): return len(ST[this])
@impl('java/lang/String', '()[B+getBytes')
def _(this): return byte_array(ST[this].encode('cp949', 'replace'))
@impl('java/lang/String', '(I)Ljava/lang/String;+substring')
def _(this, i): return jstr(ST[this][i:])
@impl('java/lang/String', '(II)Ljava/lang/String;+substring')
def _(this, i, j): return jstr(ST[this][i:j])
@impl('java/lang/String', '(Ljava/lang/Object;)Z+equals')
def _(this, o): return 1 if (o and ST.get(o) == ST.get(this)) else 0
@impl('java/lang/String', '(Ljava/lang/String;I)I+indexOf')
def _(this, s2, i): return ST[this].find(ST[s2], i)
@impl('java/lang/StringBuffer', '()V+<init>')
def _(this): ST[this] = ''
@impl('java/lang/StringBuffer', '(I)V+<init>')
def _(this, n): ST[this] = ''
@impl('java/lang/StringBuffer', '(Ljava/lang/String;)Ljava/lang/StringBuffer;+append')
def _(this, s2): ST[this] += ST.get(s2, 'null') if s2 else 'null'; return this
@impl('java/lang/StringBuffer', '(I)Ljava/lang/StringBuffer;+append')
def _(this, i): ST[this] += str(i); return this
@impl('java/lang/StringBuffer', '()Ljava/lang/String;+toString')
def _(this): return jstr(ST[this])
@impl('java/lang/StringBuffer', '()I+length')
def _(this): return len(ST[this])
@impl('java/lang/StringBuffer', '(II)Ljava/lang/StringBuffer;+delete')
def _(this, i, j): ST[this] = ST[this][:i] + ST[this][j:]; return this
@impl_s('java/lang/Integer', '(I)Ljava/lang/String;+toString')
def _(i): return jstr(str(i))
@impl_s('java/lang/Integer', '(I)Ljava/lang/String;+toHexString')
def _(i): return jstr('%x' % (i & 0xffffffff))
@impl_s('java/lang/Math', '(I)I+abs')
def _(i): return abs(i)
@impl_s('java/lang/Math', '(II)I+max')
def _(a, b): return max(a, b)
@impl_s('java/lang/Math', '(II)I+min')
def _(a, b): return min(a, b)
@impl_s('java/lang/System', '()J+currentTimeMillis')
def _():
    vnow[0] += 2; return 1700000000000 + vnow[0]
@impl_s('java/lang/System', '()V+gc')
def _(): pass
@impl_s('java/lang/System', '(I)V+exit')
def _(i): print('System.exit', i); e.mu.emu_stop(); e.exited = True
@impl_s('java/lang/System', '(Ljava/lang/Object;ILjava/lang/Object;II)V+arraycopy')
def _(src, sp, dst, dp, n):
    sb, sn, se = arr_info(src); db, dn, de = arr_info(dst)
    e.mu.mem_write(db + 8 + dp * de, bytes(e.mu.mem_read(sb + 8 + sp * se, n * se)))
@impl_s('java/lang/Runtime', '()Ljava/lang/Runtime;+getRuntime')
def _(): return single('java/lang/Runtime')
@impl('java/util/Random', '()V+<init>')
def _(this): ST[this] = _random.Random()
@impl('java/util/Random', '(J)V+setSeed')
def _(this, seed): ST[this].seed(seed)
@impl('java/util/Random', '()I+nextInt')
def _(this): return ST[this].randint(-2**31, 2**31 - 1)
@impl('java/util/Stack', '()V+<init>')
def _(this): ST[this] = []
@impl('java/util/Stack', '(Ljava/lang/Object;)Ljava/lang/Object;+push')
def _(this, o): ST[this].append(o); return o
@impl('java/util/Stack', '()Ljava/lang/Object;+pop')
def _(this): return ST[this].pop() if ST[this] else 0
@impl('java/util/Stack', '()Z+empty')
def _(this): return 0 if ST[this] else 1
# 스트림
class BS:
    def __init__(s, b): s.b = b; s.p = 0
    def read1(s):
        if s.p >= len(s.b): return -1
        v = s.b[s.p]; s.p += 1; return v
    def take(s, n): d = s.b[s.p:s.p + n]; s.p += len(d); return d
@impl('java/lang/Class', '(Ljava/lang/String;)Ljava/io/InputStream;+getResourceAsStream')
def _(this, name):
    d = res(pystr(name))
    if TRACE: print('  getResourceAsStream', repr(pystr(name)), None if d is None else len(d))
    if d is None: print('  resource not found', repr(pystr(name)), 'ST has', name in ST, [ (hex(k), v) for k, v in ST.items() if isinstance(v, str)][:0]); return 0
    h = hobj_of('java/io/InputStream'); ST[h] = BS(d); return h
@impl('java/io/InputStream', '()I+available')
def _(this): return len(ST[this].b) - ST[this].p
@impl('java/io/InputStream', '()V+close')
def _(this): pass
@impl('java/io/InputStream', '([B)I+read')
def _(this, b):
    body, n, _ = arr_info(b); d = ST[this].take(n)
    if not d: return -1
    e.mu.mem_write(body + 8, d); return len(d)
@impl('java/io/InputStream', '([BII)I+read')
def _(this, b, o, l):
    body, n, _ = arr_info(b); d = ST[this].take(l)
    if not d and l: return -1
    e.mu.mem_write(body + 8 + o, d); return len(d)
@impl('java/io/InputStream', '(J)J+skip')
def _(this, n): return len(ST[this].take(n))
@impl('java/io/DataInputStream', '(Ljava/io/InputStream;)V+<init>')
def _(this, inp): ST[this] = ST[inp]
for nm, fmt in (('()B+readByte', '>b'), ('()I+readInt', '>i'), ('()S+readShort', '>h'), ('()I+readUnsignedShort', '>H')):
    def mk(fmt):
        sz = struct.calcsize(fmt)
        return lambda this: struct.unpack(fmt, ST[this].take(sz).ljust(sz, b'\0'))[0]
    IMPL['java/io/DataInputStream::' + nm] = mk(fmt)
for nm in ('()V+close', '([B)I+read', '([BII)I+read', '(I)I+skipBytes'):
    IMPL['java/io/DataInputStream::' + nm] = IMPL.get('java/io/InputStream::' + nm) or (lambda this, n: len(ST[this].take(n)))
IMPL['java/io/DataInputStream::(I)I+skipBytes'] = lambda this, n: len(ST[this].take(n))
@impl('java/io/ByteArrayInputStream', '([B)V+<init>')
def _(this, b): ST[this] = BS(array_bytes(b))
@impl('java/io/ByteArrayOutputStream', '()V+<init>')
def _(this): ST[this] = bytearray()
@impl('java/io/ByteArrayOutputStream', '()[B+toByteArray')
def _(this): return byte_array(ST[this])
@impl('java/io/ByteArrayOutputStream', '()I+size')
def _(this): return len(ST[this])
@impl('java/io/DataOutputStream', '(Ljava/io/OutputStream;)V+<init>')
def _(this, o): ST[this] = ST[o]
for nm, fmt in (('(I)V+writeByte', '>B'), ('(I)V+writeInt', '>i'), ('(I)V+writeShort', '>H')):
    def mk(fmt): return lambda this, v: ST[this].extend(struct.pack(fmt, v & ((1 << (8 * struct.calcsize(fmt))) - 1) if fmt != '>i' else v))
    IMPL['java/io/DataOutputStream::' + nm] = mk(fmt)
IMPL['java/io/DataOutputStream::()V+close'] = lambda this: None
@impl('java/lang/Thread', '()V+<init>')
def _(this): ST[this] = {'run': None}
@impl('java/lang/Thread', '(Ljava/lang/Runnable;)V+<init>')
def _(this, r): ST[this] = {'run': r}
@impl('java/lang/Thread', '(Ljava/lang/Runnable;Ljava/lang/String;)V+<init>')
def _(this, r, n): ST[this] = {'run': r}
@impl('java/lang/Thread', '(Ljava/lang/String;)V+<init>')
def _(this, n): ST[this] = {'run': None}
THREADS = []; vnow = [0]; cur_thread = [None]
class Thr:
    def __init__(t, h): t.h = h; t.ctx = None; t.done = False; t.wake = 0; t.n = len(THREADS)
@impl('java/lang/Thread', '()V+start')
def _(this):
    t = Thr(this); THREADS.append(t); ST[this]['thr'] = t
    tgt = ST[this].get('run') or this
    t.target = tgt; print('  Thread.start', hex(this), 'target', cname_of(tgt))
def yield_now(ms=0):
    t = cur_thread[0]
    if t is not None: t.wake = vnow[0] + ms
    e.mu.emu_stop()
@impl_s('java/lang/Thread', '(J)V+sleep')
def _(ms): yield_now(max(ms, 1))
@impl_s('java/lang/Thread', '()V+yield')
def _(): yield_now(0)
for n in ('()V+wait', '(J)V+wait', '(JI)V+wait'):
    IMPL['java/lang/Object::' + n] = (lambda this, *a: yield_now(10))
@impl_s('java/lang/Thread', '()Ljava/lang/Thread;+currentThread')
def _(): return cur_thread[0].h if cur_thread[0] else single('java/lang/Thread')
@impl('java/lang/Thread', '()V+join')
def _(this): yield_now(5)
@impl('java/lang/Thread', '()Z+isAlive')
def _(this): t = ST.get(this, {}).get('thr'); return 1 if t and not t.done else 0
@impl('java/lang/Thread', '(I)V+setPriority')
def _(this, p): pass
@impl('java/lang/Thread', '()V+interrupt')
def _(this): pass
@impl_s('java/util/Calendar', '()Ljava/util/Calendar;+getInstance')
def _(): h = hobj_of('java/util/Calendar'); ST[h] = time.localtime(); return h
@impl('java/util/Calendar', '(I)I+get')
def _(this, f):
    t = ST[this]; return {1: t.tm_year, 2: t.tm_mon - 1, 5: t.tm_mday, 11: t.tm_hour, 12: t.tm_min, 13: t.tm_sec, 7: (t.tm_wday + 1) % 7 + 1}.get(f, 0)
@impl('java/util/Date', '()V+<init>')
def _(this): ST[this] = time.time()
@impl('java/util/Calendar', '(Ljava/util/Date;)V+setTime')
def _(this, d): ST[this] = time.localtime(ST[d])

SAVE = os.path.join(OUT, 'save'); os.makedirs(SAVE, exist_ok=True)
@impl_s('org/kwis/msp/io/FileSystem', '(Ljava/lang/String;)Z+exists')
def _(name): return 1 if os.path.exists(os.path.join(SAVE, pystr(name).strip('/'))) else 0
@impl('org/kwis/msp/io/File', '(Ljava/lang/String;I)V+<init>')
def _(this, name, mode):
    p = os.path.join(SAVE, pystr(name).strip('/')); ST[this] = {'p': p, 'mode': mode, 'pos': 0, 'buf': bytearray(open(p, 'rb').read()) if (os.path.exists(p) and mode != 3) else bytearray()}
@impl('org/kwis/msp/io/File', '([BII)I+write')
def _(this, b, o, l):
    f = ST[this]; f['buf'][f['pos']:f['pos'] + l] = array_bytes(b)[o:o + l]; f['pos'] += l; return l
@impl('org/kwis/msp/io/File', '([BII)I+read')
def _(this, b, o, l):
    f = ST[this]; d = bytes(f['buf'][f['pos']:f['pos'] + l]); f['pos'] += len(d)
    if not d and l: return -1
    e.mu.mem_write(arr_info(b)[0] + 8 + o, d); return len(d)
@impl('org/kwis/msp/io/File', '([B)I+read')
def _(this, b): return IMPL['org/kwis/msp/io/File::([BII)I+read'](this, b, 0, arr_info(b)[1])
@impl('org/kwis/msp/io/File', '()I+sizeOf')
def _(this): return len(ST[this]['buf'])
@impl('org/kwis/msp/io/File', '(I)V+seek')
def _(this, n): ST[this]['pos'] = n
@impl('org/kwis/msp/io/File', '()V+close')
def _(this):
    f = ST[this]
    if f['mode'] != 1: open(f['p'], 'wb').write(bytes(f['buf']))
@impl('org/kwis/msp/io/File', '()Ljava/io/DataInputStream;+openDataInputStream')
def _(this): h = hobj_of('java/io/DataInputStream'); ST[h] = BS(bytes(ST[this]['buf'])); return h
@impl('java/io/DataOutputStream', '([BII)V+write')
def _(this, b, o, l): ST[this].extend(array_bytes(b)[o:o + l])
@impl('org/kwis/msp/media/Clip', '(Ljava/lang/String;)V+<init>')
def _(this, n): pass
@impl('org/kwis/msp/media/Clip', '(Ljava/lang/String;[B)V+<init>')
def _(this, n, b): pass
@impl('org/kwis/msp/media/Clip', '(I)Z+setVolume')
def _(this, v): return 1
@impl('org/kwis/msp/media/Player', '(Lorg/kwis/msp/media/Clip;Z)Z+play')
def _(this, c, loop): return 1
@impl('org/kwis/msp/media/Player', '(Lorg/kwis/msp/media/Clip;)Z+stop')
def _(this, c): return 1
@impl('org/kwis/msp/media/Vibrator', '(II)V+on')
def _(this, a, b): pass

# ================================================================== WIPI API
FONT = ImageFont.truetype(os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'android', 'assets', 'galmuri11.ttf'), 11)
class Surf:
    def __init__(s, w, h): s.img = PImage.new('RGBA', (w, h), (0, 0, 0, 255)); s.d = ImageDraw.Draw(s.img); s.color = (0, 0, 0, 255); s.clip = None
class Gfx:
    def __init__(s, surf): s.s = surf
def rgb(c): return ((c >> 16) & 255, (c >> 8) & 255, c & 255, 255)
current_card = [0]
@impl_s('org/kwis/msp/lcdui/Display', '()Lorg/kwis/msp/lcdui/Display;+getDefaultDisplay')
def _(): return single('org/kwis/msp/lcdui/Display')
@impl('org/kwis/msp/lcdui/Display', '(Lorg/kwis/msp/lcdui/Card;)V+pushCard')
def _(this, c): current_card[0] = c; print('  pushCard', hex(c), cname_of(c))
@impl('org/kwis/msp/lcdui/Card', '()I+getWidth')
def _(this): return SCREEN_W
@impl('org/kwis/msp/lcdui/Card', '()I+getHeight')
def _(this): return SCREEN_H
for n in ('()V+<init>', '(Z)V+<init>', '(I)V+<init>', '(IZ)V+<init>', '(IIII)V+<init>', '(IIIII)V+<init>',
          '(Lorg/kwis/msp/lcdui/Display;)V+<init>', '(Lorg/kwis/msp/lcdui/Display;IIII)V+<init>', '(Lorg/kwis/msp/lcdui/Display;IIIIZ)V+<init>'):
    IMPL['org/kwis/msp/lcdui/Card::' + n] = lambda this, *a: None
repaint_req = [True]
for n in ('()V+repaint', '(IIII)V+repaint', '()V+serviceRepaints'):
    IMPL['org/kwis/msp/lcdui/Card::' + n] = lambda this, *a: repaint_req.__setitem__(0, True)
@impl('org/kwis/msp/lcdui/Card', '()Lorg/kwis/msp/lcdui/Display;+getDisplay')
def _(this): return single('org/kwis/msp/lcdui/Display')
@impl('org/kwis/msp/lcdui/Card', '()Z+isShown')
def _(this): return 1
@impl('org/kwis/msp/lcdui/Card', '()I+getX')
def _(this): return 0
@impl('org/kwis/msp/lcdui/Card', '()I+getY')
def _(this): return 0
for n in ('()V+<init>', '(Z)V+<init>', '()V+show'):
    IMPL['org/kwis/msp/lwc/AnnunciatorComponent::' + n] = lambda this, *a: None
IMPL['org/kwis/msp/handset/BackLight::()V+alwaysOn'] = lambda *a: None
STATIC.add('org/kwis/msp/handset/BackLight::()V+alwaysOn')
@impl('org/kwis/msp/lcdui/Jlet', '()V+<init>')
def _(this): pass
# --- Image / Graphics
def new_image(pimg):
    h = hobj_of('org/kwis/msp/lcdui/Image'); s = Surf(*pimg.size); s.img = pimg.convert('RGBA'); s.d = ImageDraw.Draw(s.img); ST[h] = s; return h
@impl_s('org/kwis/msp/lcdui/Image', '(Ljava/lang/String;)Lorg/kwis/msp/lcdui/Image;+createImage')
def _(name):
    d = res(pystr(name))
    if TRACE: print('  createImage', pystr(name), d is not None)
    if d is None: print('  image not found', pystr(name)); return 0
    import io; return new_image(PImage.open(io.BytesIO(d)))
@impl_s('org/kwis/msp/lcdui/Image', '([BII)Lorg/kwis/msp/lcdui/Image;+createImage')
def _(b, o, l):
    import io; return new_image(PImage.open(io.BytesIO(array_bytes(b)[o:o + l])))
@impl_s('org/kwis/msp/lcdui/Image', '(II)Lorg/kwis/msp/lcdui/Image;+createImage')
def _(w, h_): return new_image(PImage.new('RGBA', (w, h_), (0, 0, 0, 0)))
@impl('org/kwis/msp/lcdui/Image', '()I+getWidth')
def _(this): return ST[this].img.width
@impl('org/kwis/msp/lcdui/Image', '()I+getHeight')
def _(this): return ST[this].img.height
@impl('org/kwis/msp/lcdui/Image', '()Lorg/kwis/msp/lcdui/Graphics;+getGraphics')
def _(this): return make_gfx(ST[this])
def make_gfx(surf):
    h = hobj_of('org/kwis/msp/lcdui/Graphics'); ST[h] = Gfx(surf); return h
G = 'org/kwis/msp/lcdui/Graphics'
@impl(G, '(I)V+setColor')
def _(this, c): ST[this].s.color = rgb(c)
@impl(G, '()I+getColor')
def _(this): c = ST[this].s.color; return (c[0] << 16) | (c[1] << 8) | c[2]
@impl(G, '(I)V+setAlpha')
def _(this, a): pass
@impl(G, '(IIII)V+fillRect')
def _(this, x, y, w, h): s = ST[this].s; s.d.rectangle([x, y, x + w - 1, y + h - 1], fill=s.color)
@impl(G, '(IIII)V+drawRect')
def _(this, x, y, w, h): s = ST[this].s; s.d.rectangle([x, y, x + w, y + h], outline=s.color)
@impl(G, '(IIII)V+drawLine')
def _(this, a, b, c, d_): s = ST[this].s; s.d.line([a, b, c, d_], fill=s.color)
@impl(G, '(IIII)V+setClip')
def _(this, x, y, w, h): ST[this].s.clip = (x, y, w, h)
@impl(G, '(IIIIII)V+fillRoundRect')
def _(this, x, y, w, h, aw, ah): s = ST[this].s; s.d.rounded_rectangle([x, y, x + w - 1, y + h - 1], aw // 2, fill=s.color)
@impl(G, '(IIIIII)V+drawRoundRect')
def _(this, x, y, w, h, aw, ah): s = ST[this].s; s.d.rounded_rectangle([x, y, x + w, y + h], aw // 2, outline=s.color)
@impl(G, '(IIIIII)V+fillArc')
def _(this, x, y, w, h, sa, ea): s = ST[this].s; s.d.pieslice([x, y, x + w, y + h], -sa - ea, -sa, fill=s.color)
@impl(G, '(IIIIII)V+drawArc')
def _(this, x, y, w, h, sa, ea): s = ST[this].s; s.d.arc([x, y, x + w, y + h], -sa - ea, -sa, fill=s.color)
@impl(G, '(Lorg/kwis/msp/lcdui/Image;III)V+drawImage')
def _(this, im, x, y, anchor):
    if not im or im not in ST: return
    s = ST[this].s; src = ST[im].img
    if anchor & 1: x -= src.width // 2
    if anchor & 8: x -= src.width
    if anchor & 2: y -= src.height // 2
    if anchor & 32: y -= src.height
    s.img.alpha_composite(src, (x, y)) if (x >= 0 and y >= 0) else s.img.paste(src, (x, y), src)
@impl(G, '(Ljava/lang/String;III)V+drawString')
def _(this, st, x, y, anchor):
    s = ST[this].s; t = pystr(st); w = s.d.textlength(t, font=FONT)
    if anchor & 1: x -= int(w) // 2
    if anchor & 8: x -= int(w)
    s.d.text((x, y), t, font=FONT, fill=s.color)
@impl(G, '(II)I+getPixel')
def _(this, x, y): p = ST[this].s.img.getpixel((x, y)); return (p[0] << 16) | (p[1] << 8) | p[2]
for n in ('()I+getClipX', '()I+getClipY'): IMPL[G + '::' + n] = lambda this: 0
IMPL[G + '::()I+getClipWidth'] = lambda this: SCREEN_W
IMPL[G + '::()I+getClipHeight'] = lambda this: SCREEN_H

# ================================================================== 실행
def find_code(h, suffix):
    C = cls_of(h); vt = e.u32(C + 0xc); n = struct.unpack('<H', bytes(e.mu.mem_read(C + 0x10, 2)))[0]
    want = suffix.split('+')
    for i in range(n):
        M = e.u32(vt + 4 * i)
        if M:
            nm = e.cstr(e.u32(M + 0xc), 200).decode('latin1')[1:]
            if nm == suffix or nm.endswith('+' + want[-1]) and want[0] in nm: return e.u32(M) & ~1
    return None
def vcall(h, suffix, args=()):
    """핸들 h 의 vtable 에서 '...+suffix' 메소드를 찾아 호출"""
    C = cls_of(h); vt = e.u32(C + 0xc); n = struct.unpack('<H', bytes(e.mu.mem_read(C + 0x10, 2)))[0]
    for i in range(n):
        M = e.u32(vt + 4 * i)
        if M and e.cstr(e.u32(M + 0xc), 200).decode('latin1')[1:].endswith('+' + suffix.split('+')[-1]) and suffix.split('+')[0] in e.cstr(e.u32(M + 0xc), 200).decode('latin1'):
            return e.run(e.u32(M) & ~1, [0, h] + list(args), count=20_000_000)
    print('  vcall: not found', suffix, cname_of(h)); return None
def run_slice(t, count=20_000_000):
    cur_thread[0] = t
    if t.ctx is None:
        code = find_code(t.target, '()V+run')
        if code is None: print('  thread: run() not found for', cname_of(t.target)); t.done = True; return
        sp = STACK + STACK_SZ - 0x100 - (t.n + 1) * 0x20000
        e.mu.reg_write(UC_ARM_REG_SP, sp); e.mu.reg_write(UC_ARM_REG_LR, RET | 1)
        e.setr(0, 0); e.setr(1, t.target); pc = code | 1
    else:
        e.mu.context_restore(t.ctx); pc = e.mu.reg_read(UC_ARM_REG_PC); pc |= 1 if (e.mu.reg_read(UC_ARM_REG_CPSR) & 0x20) else 0
    try: e.mu.emu_start(pc, RET, count=count)
    except Exception as ex: print('  thread EXC', ex); t.done = True; cur_thread[0] = None; return
    if e.mu.reg_read(UC_ARM_REG_PC) == RET or e.threw: t.done = True
    else:
        t.ctx = e.mu.context_save()
        if TRACE: print('  slice end pc=%#x lr=%#x' % (e.mu.reg_read(UC_ARM_REG_PC) - BASE, e.mu.reg_read(UC_ARM_REG_LR) - BASE))
    cur_thread[0] = None
def snapshot(name):
    surf = Surf(SCREEN_W, SCREEN_H); g = make_gfx(surf)
    r = vcall(current_card[0], '(Lorg/kwis/msp/lcdui/Graphics;)V+paint', [g])
    surf.img.convert('RGB').save(os.path.join(OUT, name + '.png'))
PROF = bool(os.environ.get('PROF'))
if __name__ == '__main__':
    C = BASE + 0xac804; I = e.u32(C + 8); obj = new_obj(C)
    def method(i): return e.u32(e.u32(e.u32(I + 0xc) + 4 * i)) & ~1
    for nm, fn, args in (('<init>', method(0), [0, obj]), ('startApp', method(1), [0, obj, 0])):
        print('=== %s' % nm)
        e.threw = False
        try: print('->', hex(e.run(fn, args, count=30_000_000)))
        except Exception as ex:
            import traceback; traceback.print_exc(limit=-3)
        if e.threw: break
    if current_card[0]:
        r = vcall(current_card[0], '(Z)V+showNotify', [1]); print('showNotify ->', r)
    if PROF:
        import collections
        hist = collections.Counter()
        e.mu.hook_add(UC_HOOK_BLOCK, lambda uc, a, sz, ud: hist.update([a - BASE]))
        cnt = [0]
        def dbg(uc, a, sz, ud):
            if cnt[0] < 24:
                cnt[0] += 1
                print('  lcmp a=(%#x,%#x) b=(%#x,%#x)' % (e.r(0), e.r(1), e.r(2), e.r(3)))
        for a in (0x7bcf8,): e.mu.hook_add(UC_HOOK_CODE, dbg, begin=BASE + a, end=BASE + a)
        for t in THREADS: run_slice(t, 600_000)
        sl = BASE + 0xad2a8
        R = e.u32(sl + 0x30); print('R=%#x' % R, [hex(e.u32(R + 4 * i)) for i in range(6)]); M0 = e.u32(R); print('  M name', e.cstr(e.u32(M0 + 0xc), 100), 'class', e.cstr(e.u32(e.u32(e.u32(M0 + 4) + 8)), 80))
        w = e.u32(R)
        if w & 1: print('  word ->', strtab((w & 0xffff) >> 1), strtab(w >> 16))
        print('HOT blocks:', [(hex(a), n) for a, n in hist.most_common(12)]); sys.exit(0)
    NR = int(os.environ.get('ROUNDS', '300')); shots = set(range(1, NR + 1, max(1, NR // 20)))
    print('allocated MB', (e.heap_ptr - 0x50000000) >> 20)
    for rnd in range(1, NR + 1):
        vnow[0] += 33
        for t in list(THREADS):
            if not t.done and t.wake <= vnow[0]: run_slice(t)
        if rnd < 6: print('round', rnd, 'heapMB', (e.heap_ptr - 0x50000000) >> 20, [(t.n, t.done) for t in THREADS])
        if e.threw: break
        if rnd in shots and current_card[0]:
            snapshot('frame%03d' % rnd); print('round', rnd, 'threads', [(t.n, t.done, t.wake) for t in THREADS])
