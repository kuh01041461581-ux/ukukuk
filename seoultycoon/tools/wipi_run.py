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
links = []; classes = {}
STRTAB = BASE + 0x9cac0
def strtab(i): return e.cstr(e.u32(STRTAB + 4 * i), 200)
class HClass:
    def __init__(self, name, vt=0, size=8):
        self.name = name; self.methods = {}
        self.C = e.alloc(0x40); self.I = e.alloc(0x40)
        nm = e.alloc(len(name) + 1); e.mu.mem_write(nm, name.encode() + b'\0')
        e.w32(self.I, nm); e.mu.mem_write(self.I + 0x1a, struct.pack('<H', size))
        e.w32(self.C + 8, self.I)
        e.mu.mem_write(self.C + 0x10, struct.pack('<H', vt)); e.mu.mem_write(self.C + 0x12, struct.pack('<H', 0x80))
        self.vtab = e.alloc(4 * max(vt, 1)); e.w32(self.C + 0xc, self.vtab)
    def method(self, nameptr, key, vidx=0):
        if key not in self.methods:
            M = e.alloc(0x20); fn = e.host('M:%s.%s' % (self.name, key))
            e.w32(M, fn); e.w32(M + 4, self.C); e.w32(M + 0xc, nameptr)
            e.mu.mem_write(M + 0x14, struct.pack('<HH', vidx, 1)); self.methods[key] = M
        return self.methods[key]
hclasses = {}
def hclass(name):
    if name not in hclasses:
        if name == 'org/kwis/msp/lcdui/Jlet':
            hc = HClass(name, vt=19, size=8)
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
    print('  !!! THROW', e.cstr(a[0]).decode(), 'lr=%#x' % (e.mu.reg_read(UC_ARM_REG_LR) - BASE)); e.mu.emu_stop(); return 0
e.impl['WIPI_JBInterface[2]'] = jb_throw
e.impl['WIPI_JBInterface[10]'] = lambda e, a, s: 0          # 클래스 초기화(<clinit>) — 아직 생략
def jb_resolve(e, a, s):
    slot = a[0]; w = e.u32(slot)
    if not (w & 1):                                          # 클래스 C 가 넘어온 경우 = new
        Ci = e.u32(slot + 8); size = struct.unpack('<H', bytes(e.mu.mem_read(Ci + 0x1a, 2)))[0]
        o = e.alloc(max(size, 8) + 16); e.w32(o, slot); return o
    cname = strtab((w & 0xffff) >> 1).decode(); mem = strtab(w >> 16)
    nameptr = e.u32(STRTAB + 4 * (w >> 16))
    hc = hclass(cname); key = mem[1:].decode('latin1')
    M = hc.method(nameptr, key)
    print('  resolve %s :: %s' % (cname, key))
    e.w32(slot, M); return 0
e.impl['WIPI_JBInterface[1]'] = jb_resolve
def link(e, a, s):
    name = e.cstr(a[1], 120)
    nm = name.decode('latin1')
    links.append((a[0], nm))
    if '+' in nm or '(' in nm: h = e.host('link:' + nm)           # 메소드/필드 -> 호스트 함수
    else:                                                          # 클래스 -> 0 으로 채운 클래스 구조체
        if nm not in classes: classes[nm] = hclass(nm).C
        h = classes[nm]
    e.w32(a[0], h)
    return 0
e.impl['env[8]'] = link
e.impl['env[11]'] = lambda e,a,s: e.alloc(a[0])
strobj = {}
def newstr(e, a, s):
    txt = e.cstr(a[0], 400); o = e.alloc(32)
    e.w32(o, classes.get('java/lang/String', 0)); strobj[o] = txt
    return o
e.impl['WIPI_JBInterface[11]'] = newstr
r = e.run(BASE + 0x7b1ec, [scratch, 0, scratch + 64, 0], [env])
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
obj = e.alloc(64); e.w32(obj, C)
print('Seoul2 vtable', hex(e.u32(C + 0xc)), 'count', e.mu.mem_read(C + 0x10, 2).hex())
def method(i):
    M = e.u32(e.u32(I + 0xc) + 4 * i); return e.u32(M) & ~1
e.verbose = True
for nm, fn, args in (('<init>', method(0), [0, obj]), ('startApp', method(1), [0, obj, 0])):
    print('=== %s @%#x' % (nm, fn - BASE))
    try: print('->', hex(e.run(fn, args, count=3_000_000)))
    except Exception as ex: print('EXC', ex)
    print(e.calls.most_common(12))
