"""재배치된 이미지에서 Thumb 코드를 디스어셈블하고 pc-상대 리터럴을 값/문자열로 풀어서 보여준다.
사용: disasm_thumb.py relocated.bin 시작hex 끝hex"""
import sys, struct, capstone
d = open(sys.argv[1], 'rb').read(); B = 0x1000000
a, b = int(sys.argv[2], 16), int(sys.argv[3], 16)
md = capstone.Cs(capstone.CS_ARCH_ARM, capstone.CS_MODE_THUMB); md.detail = True
def lit(v):
    s = ''
    if B <= v < B + len(d):
        o = v - B; e = d.find(b'\0', o, o + 80)
        t = d[o:e]
        if len(t) >= 3 and all(32 <= c < 127 for c in t): s = ' "%s"' % t.decode()
        else: s = ' ->%#x' % o
    return s
for i in md.disasm(d[a:b], a):
    extra = ''
    if i.mnemonic == 'ldr' and 'pc' in i.op_str and '#' in i.op_str:
        off = int(i.op_str.split('#')[1].rstrip(']'), 16)
        la = ((i.address + 4) & ~3) + off
        v = struct.unpack_from('<I', d, la)[0]; extra = '   ; [%#x]=%#x%s' % (la, v, lit(v))
    print('%#x  %-6s %s%s' % (i.address, i.mnemonic, i.op_str, extra))
