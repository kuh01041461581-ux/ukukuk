"""서울타이쿤2 client.bin1116 을 Unicorn(ARM Thumb)으로 올려 자기재배치 스텁을 실행하고 진입점을 찾는다."""
import struct, sys
from unicorn import *
from unicorn.arm_const import *
d = open(sys.argv[1], 'rb').read()
BASE = 0x01000000
mu = Uc(UC_ARCH_ARM, UC_MODE_THUMB)
mu.mem_map(BASE, 0x400000); mu.mem_write(BASE, d)
mu.mem_map(0x20000000, 0x100000)   # stack/heap
mu.mem_map(0, 0x1000)
LR = 0x20000000 + 0x80001  # sentinel return (thumb)
mu.mem_write(0x20000000, b'\x00\xbf' * 16)
mu.reg_write(UC_ARM_REG_SP, 0x20000000 + 0x80000)
mu.reg_write(UC_ARM_REG_LR, 0x20000001)
mu.reg_write(UC_ARM_REG_R0, 0x45c)
mu.emu_start((BASE + 0xc) | 1, 0x20000000, count=2000000)
r0 = mu.reg_read(UC_ARM_REG_R0)
print('entry/ret r0 = %#x  (file off %#x)' % (r0, r0 - BASE))
mu.mem_write(0x30000000 - 0x10, b'') if False else None
open('/tmp/st2_relocated.bin', 'wb').write(bytes(mu.mem_read(BASE, len(d))))
