"""stage 폴더의 *.d 중 MMF(MMMD) 를 표준 MIDI 바이트로 바꿔 같은 이름으로 덮어쓴다 (안드로이드가 MMF 를 재생하지 못하므로)."""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from smaf2mid import convert
n = 0
for f in sorted(os.listdir(sys.argv[1])):
    p = os.path.join(sys.argv[1], f)
    if f.endswith('.d') and open(p, 'rb').read(4) == b'MMMD':
        mid, _, _ = convert(open(p, 'rb').read()); open(p, 'wb').write(mid); n += 1
print('converted', n, 'MMF -> MIDI')
