"""Yamaha SMAF(MMF, HandyPhone 형식 MTR 트랙) -> 표준 MIDI(Format 0, 1tick=1ms) 변환기.
사양은 공개 문서(SMAF/vavi-sound 해석)를 따라 새로 작성. 사용: smaf2mid.py in.mmf out.mid"""
import struct, sys

def vlq_hp(b, p):
    x = b[p]; p += 1
    if x & 0x80:
        return (((x & 0x7f) + 1) << 7) | b[p], p + 1
    return x, p

def tb_ms(c):
    return {0: 1, 0x10: 1, 1: 2, 0x11: 2, 2: 4, 0x12: 4, 3: 5, 0x13: 5}.get(c, 4)

def hp_pitch(octv, note, sh):
    p = note + octv * 12 + 36
    p += {1: 12, 2: 24, 3: 36, 4: 48, 0x81: -12, 0x82: -24, 0x83: -36, 0x84: -48}.get(sh, 0)
    return max(0, min(127, p))

def mch(base, ch):
    c = base + ch
    return (c + 1) & 15 if c >= 9 else c

def decode_track(seq, dms, gms, base, perc):
    ev = []; p = 0; t = 0; sh = [0] * 4; prog = [0] * 4
    while p < len(seq):
        d, p = vlq_hp(seq, p); t += d * dms
        if p >= len(seq): break
        e1 = seq[p]; p += 1
        if e1 == 0xff:
            e2 = seq[p]; p += 1
            if e2 == 0: continue
            if e2 in (0x2f, 0x51, 0x58, 0xf0):
                ln = seq[p]; p += 1 + ln
        elif e1 == 0x00:
            e2 = seq[p]; p += 1
            if e2 == 0:
                if seq[p] == 0: break
                p += 1; continue
            ch = (e2 & 0xc0) >> 6; evt = (e2 & 0x30) >> 4; data = e2 & 0xf
            if evt == 3:
                v = seq[p]; p += 1
                mc = 9 if perc & (1 << ch) else mch(base, ch)
                if data == 0:
                    prog[ch] = v & 0x7f
                    if not perc & (1 << ch): ev.append((t, 0xc0 | mc, v & 0x7f, None))
                elif data == 1: ev.append((t, 0xb0 | mc, 0, v & 0x7f))
                elif data == 2: sh[ch] = v
                elif data == 3: ev.append((t, 0xb0 | mc, 1, v & 0x7f))
                elif data == 4: ev.append((t, 0xe0 | mc, 0, v & 0x7f))
                elif data == 7: ev.append((t, 0xb0 | mc, 7, v & 0x7f))
                elif data == 0xa: ev.append((t, 0xb0 | mc, 10, v & 0x7f))
                elif data == 0xb: ev.append((t, 0xb0 | mc, 11, v & 0x7f))
        else:
            ch = (e1 & 0xc0) >> 6; octv = (e1 & 0x30) >> 4; note = e1 & 0xf
            gate, p = vlq_hp(seq, p)
            if gate == 0: continue
            if perc & (1 << ch): mc = 9; pitch = prog[ch] & 0x7f
            else: mc = mch(base, ch); pitch = hp_pitch(octv, note, sh[ch])
            ev.append((t, 0x90 | mc, pitch, 100)); ev.append((t + gate * gms, 0x80 | mc, pitch, 0x40))
    return ev

def convert(d):
    assert d[:4] == b'MMMD'
    pos = 8; end = min(len(d), 8 + struct.unpack('>I', d[4:8])[0]); ev = []; tn = 0
    while pos + 8 <= end and tn < 4:
        tag = d[pos:pos + 4]; sz = struct.unpack('>I', d[pos + 4:pos + 8])[0]; body = pos + 8
        if tag[:3] == b'MTR' and d[body] == 0:
            dms, gms = tb_ms(d[body + 2]), tb_ms(d[body + 3])
            perc = 0
            for i in range(4):
                nib = ((d[body + 4] >> (4 * (1 - i))) & 15) if i < 2 else ((d[body + 5] >> (4 * (3 - i))) & 15)
                if nib & 3 == 3: perc |= 1 << i
            q = body + 6
            while q + 8 <= body + sz:
                st = d[q:q + 4]; ss = struct.unpack('>I', d[q + 4:q + 8])[0]
                if st == b'Mtsq':
                    ev += decode_track(d[q + 8:q + 8 + ss], dms, gms, tn * 4, perc); tn += 1; break
                q += 8 + ss
        pos = body + sz
    ev.sort(key=lambda e: e[0])          # 파이썬 sort 는 안정 정렬 -> 같은 시각이면 입력 순서 유지
    out = bytearray(); last = 0
    def vl(v):
        b = [v & 0x7f]; v >>= 7
        while v: b.append((v & 0x7f) | 0x80); v >>= 7
        return bytes(reversed(b))
    out += b'\x00\xff\x51\x03' + (1000000).to_bytes(3, 'big')
    for t, s, a, b in ev:
        out += vl(t - last) + bytes([s, a]) + (bytes([b]) if b is not None else b''); last = t
    out += b'\x00\xff\x2f\x00'
    return b'MThd' + struct.pack('>IHHH', 6, 0, 1, 1000) + b'MTrk' + struct.pack('>I', len(out)) + bytes(out), len(ev), last

if __name__ == '__main__':
    mid, n, ms = convert(open(sys.argv[1], 'rb').read())
    open(sys.argv[2], 'wb').write(mid); print(sys.argv[1], 'events', n, 'length_ms', ms)
