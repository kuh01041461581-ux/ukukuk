"""시청 메뉴 비서 이름표(54.m, 글자 8줄 x 3열, 한 글자 10x10): 줄 순서 = 김비서·노막힘·돈이면·부르리·푸르게·달리리·잘살리·머든지.
익산 이름으로 다시 찍는다(text_map 의 비서 이름과 같게): 노막힘→솜리길, 돈이면→보석왕, 부르리→서동이, 푸르게→맑은강, 달리리→마한이, 잘살리→쌀부자, 머든지→마룡이. 김비서는 그대로."""
import os
from PIL import Image, ImageDraw, ImageFont
root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
NAMES = ['김비서', '솜리길', '보석왕', '서동이', '맑은강', '마한이', '쌀부자', '마룡이']
def build():
    src = Image.open(root + '/work/st2/54.m').convert('RGBA'); out = Image.new('RGBA', src.size, (0, 0, 0, 0))
    f = ImageFont.truetype(root + '/android/assets/galmuri11.ttf', 10)                       # 도트 글꼴(원본 글자 그림과 같은 느낌)
    for r, nm in enumerate(NAMES):
        if r == 0: out.alpha_composite(src.crop((0, 0, 30, 10)), (0, 0)); continue
        for c, ch in enumerate(nm):
            big = Image.new('L', (60, 60), 0); fb = ImageFont.truetype(root + '/android/assets/galmuri11.ttf', 48); ImageDraw.Draw(big).text((6, -2), ch, font=fb, fill=255)
            bb = big.getbbox()
            if not bb: continue
            g = big.crop(bb).resize((9, 9), Image.LANCZOS).point(lambda v: 255 if v >= 110 else 0)
            col = Image.new('RGBA', (9, 9), (255, 255, 255, 255)); col.putalpha(g); out.alpha_composite(col, (c * 10 + 1, r * 10))
    return out
if __name__ == '__main__':
    im = build(); im.save(root + '/st2android/mod/54.m', format='PNG')
    bg = Image.new('RGBA', im.size, (60, 60, 90, 255)); bg.alpha_composite(im); bg.resize((im.width * 8, im.height * 8), Image.NEAREST).save('/tmp/claude-0/prev/m54new.png'); print('비서 이름표 완료')
