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
def link(e, a, s):
    name = e.cstr(a[1], 120)
    nm = name.decode('latin1')
    links.append((a[0], nm))
    if '+' in nm or '(' in nm: h = e.host('link:' + nm)           # 메소드/필드 -> 호스트 함수
    else:                                                          # 클래스 -> 0 으로 채운 클래스 구조체
        if nm not in classes:
            c = e.alloc(0x100); e.mu.mem_write(c + 0x12, struct.pack('<H', 0x80)); classes[nm] = c
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
