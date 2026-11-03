"""서울타이쿤2 (WIPI Java→ARM AOT 이미지) Unicorn 에뮬레이터.
호스트 함수는 '매직 주소'에 걸어두고 호출 시 파이썬이 처리한다. 모르는 슬롯은 이름·인자를 로그에 남긴다."""
import struct, sys, collections
from unicorn import *
from unicorn.arm_const import *

BASE = 0x01000000; IMG = 0x600000
STACK = 0x20000000; STACK_SZ = 0x200000
HEAP = 0x30000000; HEAP_SZ = 0x4000000
MAGIC = 0x40000000; MAGIC_SZ = 0x100000
RET = MAGIC + 0x80000

class Emu:
    def __init__(self, path, verbose=True):
        self.verbose = verbose
        self.mu = Uc(UC_ARCH_ARM, UC_MODE_THUMB)
        d = open(path, 'rb').read()
        self.mu.mem_map(BASE, IMG); self.mu.mem_write(BASE, d)
        self.mu.mem_map(STACK, STACK_SZ); self.mu.mem_map(HEAP, HEAP_SZ); self.mu.mem_map(MAGIC, MAGIC_SZ)
        self.mu.mem_write(MAGIC, b'\x70\x47' * (MAGIC_SZ // 2))     # bx lr
        self.mu.mem_map(0, 0x1000)
        self.heap_ptr = HEAP; self.magic_ptr = MAGIC; self.hosts = {}; self.names = {}
        self.calls = collections.Counter()
        self.mu.hook_add(UC_HOOK_CODE, self._code, begin=MAGIC, end=MAGIC + MAGIC_SZ)
        self.mu.hook_add(UC_HOOK_MEM_READ_UNMAPPED | UC_HOOK_MEM_WRITE_UNMAPPED | UC_HOOK_MEM_FETCH_UNMAPPED, self._unmapped)
        self.impl = {}
        # relocation stub
        self.run(BASE + 0xc, [0x45c]); self.entry = self.r(0)
    # -- regs/mem
    def r(self, i): return self.mu.reg_read(UC_ARM_REG_R0 + i)
    def setr(self, i, v): self.mu.reg_write(UC_ARM_REG_R0 + i, v & 0xffffffff)
    def u32(self, a): return struct.unpack('<I', self.mu.mem_read(a, 4))[0]
    def w32(self, a, v): self.mu.mem_write(a, struct.pack('<I', v & 0xffffffff))
    def cstr(self, a, n=256):
        b = bytes(self.mu.mem_read(a, n)); return b.split(b'\0')[0]
    def alloc(self, n):
        n = (n + 15) & ~15; a = self.heap_ptr; self.heap_ptr += n; return a
    def host(self, name, fn=None):
        a = self.magic_ptr; self.magic_ptr += 4
        self.hosts[a] = name; self.names[name] = a
        if fn: self.impl[name] = fn
        return a | 1
    # -- execution
    def run(self, addr, args=(), stack_args=(), count=20_000_000):
        sp = STACK + STACK_SZ - 0x100 - 4 * len(stack_args)
        for i, v in enumerate(stack_args): self.w32(sp + 4 * i, v)
        self.mu.reg_write(UC_ARM_REG_SP, sp)
        for i, v in enumerate(args): self.setr(i, v)
        self.mu.reg_write(UC_ARM_REG_LR, RET | 1)
        self.mu.emu_start(addr | 1, RET, count=count)
        return self.r(0)
    def _code(self, uc, addr, size, ud):
        if addr == RET or addr not in self.hosts: return
        name = self.hosts[addr]; self.calls[name] += 1
        args = [self.r(i) for i in range(4)]
        sp = uc.reg_read(UC_ARM_REG_SP); sargs = [self.u32(sp + 4 * i) for i in range(4)]
        fn = self.impl.get(name)
        if fn: ret = fn(self, args, sargs)
        else:
            ret = 0
            if self.verbose:
                st = [self.u32(sp + 4 * i) for i in range(0, 64)]
                bt = ['%#x' % (v - BASE - 1) for v in st if BASE + 0x100 < v < BASE + 0x82700 and v & 1][:8]
                print('     backtrace(file offs):', ' '.join(bt))
            if self.verbose: print('  [host?] %s(%s | %s) lr=%#x' % (name, ','.join('%#x' % a for a in args), ','.join('%#x' % a for a in sargs), uc.reg_read(UC_ARM_REG_LR) - BASE))
        self.setr(0, ret or 0)
    def _unmapped(self, uc, access, addr, size, val, ud):
        pc = uc.reg_read(UC_ARM_REG_PC)
        print('!! unmapped access %d at %#x pc=%#x (file %#x)' % (access, addr, pc, pc - BASE)); return False
    def iface(self, name, nslots=64):
        base = self.alloc(4 * nslots)
        for i in range(nslots): self.w32(base + 4 * i, self.host('%s[%d]' % (name, i)))
        return base

if __name__ == '__main__':
    e = Emu(sys.argv[1])
    print('entry struct at %#x' % (e.entry - BASE))
