import sys
sys.path.insert(0, __file__.rsplit('/', 1)[0])
from wipi_emu import *

e = Emu(sys.argv[1])
ifaces = {}
def lookup(e, a, s):
    name = e.cstr(a[0]).decode(); print('lookup(%r, %d, %d, %d, %d)' % (name, *[(x if x < 2**31 else x - 2**32) for x in (a[1], a[2], a[3], s[0])]))
    if name not in ifaces:
        p = e.iface(name, 80)
        if name == 'WIPIC_knlInterface':
            tbl = e.iface('clib', 24)
            e.impl['WIPIC_knlInterface[33]'] = lambda e, a, s: tbl
        ifaces[name] = p
    return ifaces[name]
env = e.iface('env', 64); e.w32(env, e.host('env.lookup', lookup))
scratch = e.alloc(256)
import wipi_emu
def propagate(hostC, vidx, M):
    """호스트 클래스에 늦게 생긴 메소드를 이미 링크된 하위 이미지 클래스의 vtable 에도 써 넣는다."""
    for nm, Cx in img_classes.items():
        c = Cx; depth = 0
        while c and depth < 16:
            if c == hostC and Cx != hostC:
                vt = e.u32(Cx + 0xc)
                if vt: e.w32(vt + 4 * vidx, M)
                break
            sup = e.u32(e.u32(c + 8) + 8)
            c = sup if (sup and not (sup & 1) and BASE <= sup < BASE + 0x600000 or 0x2000000 <= sup < 0x3800000) else 0
            depth += 1
IMPL = {}; RET = wipi_emu.RETT
import time
def impl(cls, sig):
    def deco(f): IMPL[cls + '::' + sig] = f; return f
    return deco
@impl('java/lang/System', '()J+currentTimeMillis')
def _ctm(e, a, s): return int(time.time() * 1000)
@impl('java/util/Random', '(J)V+setSeed')
def _seed(e, a, s): return 0
hobjs = {}
def hobj(name):
    if name not in hobjs: hobjs[name] = new_obj(hclass(name).C)
    return hobjs[name]
@impl('org/kwis/msp/lcdui/Display', '()Lorg/kwis/msp/lcdui/Display;+getDefaultDisplay')
def _gdd(e, a, s): return hobj('org/kwis/msp/lcdui/Display')
@impl('org/kwis/msp/lcdui/Card', '()I+getWidth')
def _gw(e, a, s): return 240
@impl('org/kwis/msp/lcdui/Card', '()I+getHeight')
def _gh(e, a, s): return 320
links = []; classes = {}
img_classes = {}
for i in range(193):
    Cx = BASE + 0xac6c4 + i * 0x14; Ix = e.u32(Cx + 8)
    if BASE <= Ix < BASE + 0x600000 and BASE <= e.u32(Ix) < BASE + 0x600000:
        img_classes[e.cstr(e.u32(Ix), 120).decode('latin1')] = Cx
def classfor(name):
    return img_classes[name] if name in img_classes else hclass(name).C
STRTAB = BASE + 0x9cac0
def strtab(i): return e.cstr(e.u32(STRTAB + 4 * i), 200)
class HClass:
    def __init__(self, name, vt=64, size=0):
        self.name = name; self.methods = {}; self.reserved = set()
        self.C = e.alloc(0x40); self.I = e.alloc(0x40)
        nm = e.alloc(len(name) + 1); e.mu.mem_write(nm, name.encode() + b'\0')
        e.w32(self.I, nm); e.mu.mem_write(self.I + 0x1a, struct.pack('<H', size))
        e.w32(self.C + 8, self.I)
        e.mu.mem_write(self.C + 0x10, struct.pack('<H', vt)); e.mu.mem_write(self.C + 0x12, struct.pack('<H', 0x80))
        self.vtab = e.alloc(4 * max(vt, 1)); e.w32(self.C + 0xc, self.vtab); self.nextv = 0
    def method(self, nameptr, key, vidx=None):
        if key not in self.methods:
            M = e.alloc(0x20); hn = 'M:%s.%s' % (self.name, key); fn = e.host(hn)
            if hn not in e.impl and (self.name + '::' + key) in IMPL:
                e.impl[hn] = IMPL[self.name + '::' + key]
            if '()' in key[:3] or ')' in key:
                rt = key[key.index(')') + 1] if ')' in key else 'V'; RET[hn] = rt
            if vidx is None:
                while self.nextv in self.reserved: self.nextv += 1
                vidx = self.nextv; self.nextv += 1
            e.w32(M, fn); e.w32(M + 4, self.C); e.w32(M + 0xc, nameptr)
            e.mu.mem_write(M + 0x14, struct.pack('<HH', vidx, 1)); self.methods[key] = M; e.w32(self.vtab + 4 * vidx, M)
            propagate(self.C, vidx, M)
        return self.methods[key]
hclasses = {}
def hclass(name):
    if name not in hclasses:
        if name == 'org/kwis/msp/lcdui/Jlet':
            hc = HClass(name, vt=64, size=0); hc.reserved = set(range(0, 19)); hc.nextv = 19
            # 슬롯 15..18 = startApp/pauseApp/resumeApp/destroyApp (Seoul2 의 오버라이드 시그니처 문자열 재사용)
            sigs = {15: 0x1087 and None}
            hclasses[name] = hc
            seoul_I = e.u32(BASE + 0xac804 + 8)
            for k in range(19):
                M = e.alloc(0x20); e.w32(M, e.host('M:Jlet.vt%d' % k)); e.w32(M + 4, hc.C)
                if 15 <= k <= 18:
                    own = e.u32(e.u32(seoul_I + 0xc) + 4 * (k - 14)); e.w32(M + 0xc, e.u32(own + 0xc))
                else:
                    nmp = e.alloc(16); e.mu.mem_write(nmp, b'Z()V+vt%d\0' % k); e.w32(M + 0xc, nmp)
                e.mu.mem_write(M + 0x14, struct.pack('<HH', k, 1)); e.w32(hc.vtab + 4 * k, M)
        else:
            hclasses[name] = HClass(name)
    return hclasses[name]
def jb_throw(e, a, s):
    if a[0] < 0x100000:                                      # 작은 수 = newarray(len, elemsize, class)
        return new_array(a[2], a[0], a[1])
    print('  !!! THROW', e.cstr(a[0]).decode(), 'lr=%#x' % (e.mu.reg_read(UC_ARM_REG_LR) - BASE)); e.mu.emu_stop(); return 0
e.impl['WIPI_JBInterface[2]'] = jb_throw
e.impl['WIPI_JBInterface[10]'] = lambda e, a, s: 0
e.impl['WIPI_JBInterface[12]'] = lambda e, a, s: (e.ret64 or e.alloc(16)) if e.ret64 is None else e.ret64          # 클래스 초기화(<clinit>) — 아직 생략
def new_obj(C, extra=0):
    """핸들 기반 객체: 핸들 -> 칸[0]=몸체 포인터, 몸체[0]=(Class<<5)|플래그, 필드는 몸체+4+오프셋"""
    Ci = e.u32(C + 8); size = struct.unpack('<H', bytes(e.mu.mem_read(Ci + 0x1a, 2)))[0]
    body = e.alloc(4 + size + extra + 8); e.w32(body, (C << 5) & 0xffffffff)
    h = e.alloc(8); e.w32(h, body); return h
def new_array(C, n, esz):
    body = e.alloc(8 + n * esz + 8); e.w32(body, (C << 5) & 0xffffffff); e.w32(body + 4, n)
    h = e.alloc(8); e.w32(h, body); return h
fcount = {}
def jb_resolve(e, a, s):
    slot, cb = a[0], a[1]; w = e.u32(slot)
    if not (w & 1):                                          # 클래스 C 가 넘어온 경우 = new
        return new_obj(slot)
    if cb == BASE + 0x7b4d5:                                 # 클래스 참조
        e.w32(slot, classfor(strtab(w >> 1).decode('latin1'))); print('  resolve class', strtab(w >> 1).decode()); return 0
    cname = strtab((w & 0xffff) >> 1).decode(); mem = strtab(w >> 16)
    nameptr = e.u32(STRTAB + 4 * (w >> 16)); key = mem[1:].decode('latin1')
    if cb == BASE + 0x7b469:                                 # 필드 참조 -> F{+0xc=오프셋}
        F = e.alloc(0x20); n = fcount.get(cname, 0); fcount[cname] = n + 1
        e.w32(F + 0xc, 4 * n); e.w32(F + 4, classfor(cname)); e.w32(slot, F)
        print('  resolve FIELD %s :: %s -> off %d' % (cname, key, 4 * n)); return 0
    hc = hclass(cname); M = hc.method(nameptr, key)
    print('  resolve %s :: %s  M=%#x' % (cname, key, M)); e.w32(slot, M); return 0
e.impl['WIPI_JBInterface[1]'] = jb_resolve
def link(e, a, s):
    name = e.cstr(a[1], 120)
    nm = name.decode('latin1')
    links.append((a[0], nm))
    if '+' in nm or '(' in nm: h = e.host('link:' + nm)           # 메소드/필드 -> 호스트 함수
    else:                                                          # 클래스 -> 0 으로 채운 클래스 구조체
        h = classfor(nm)
    e.w32(a[0], h)
    return 0
e.impl['env[8]'] = link
e.impl['env[11]'] = lambda e,a,s: e.alloc(a[0])
strobj = {}
def newstr(e, a, s):
    txt = e.cstr(a[0], 400); h = new_obj(hclass('java/lang/String').C); strobj[h] = txt
    return h
e.impl['WIPI_JBInterface[11]'] = newstr
r = e.run(BASE + 0x7b1ec, [scratch, 0, 0, 0], [env])
print('init ->', hex(r)); print(e.calls)

import sys
for cb in [int(x,16) for x in sys.argv[2:]]:
    print('--- call %#x' % cb)
    try: print('->', hex(e.run(BASE + cb, [0, 0, 0, 0])))
    except Exception as ex: print('EXC', ex)
    print(e.calls)

print(len(links)); seen=[]
for _, n in links:
    if n not in seen: seen.append(n)
print(len(seen)); [print(' ', repr(n)) for n in seen[:80]]

# ---- 앱 클래스(Seoul2) 인스턴스 생성 후 <init>, startApp 호출
C = BASE + 0xac804; I = e.u32(C + 8)
obj = new_obj(C)
print('Seoul2 vtable', hex(e.u32(C + 0xc)), 'count', e.mu.mem_read(C + 0x10, 2).hex())
def method(i):
    M = e.u32(e.u32(I + 0xc) + 4 * i); return e.u32(M) & ~1
e.verbose = True
for nm, fn, args in (('<init>', method(0), [0, obj]), ('startApp', method(1), [0, obj, 0])):
    print('=== %s @%#x' % (nm, fn - BASE))
    try: print('->', hex(e.run(fn, args, count=3_000_000)))
    except Exception as ex: print('EXC', ex)
    print(e.calls.most_common(12))
