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
links = []
def link(e, a, s):
    name = e.cstr(a[1], 120)
    nm = name.decode('latin1')
    links.append((a[0], nm))
    h = e.host('link:' + nm)
    e.w32(a[0], h)
    return 0
e.impl['env[8]'] = link
r = e.run(BASE + 0x7b1ec, [scratch, 0, scratch + 64, 0], [env])
print('init ->', hex(r)); print(e.calls)

import sys
for cb in [int(x,16) for x in sys.argv[2:]]:
    print('--- call %#x' % cb)
    try: print('->', hex(e.run(BASE + cb, [0, 0, 0, 0])))
    except Exception as ex: print('EXC', ex)
    print(e.calls)

print(len(links)); [print(' ', repr(n)) for _, n in links[:60]]
