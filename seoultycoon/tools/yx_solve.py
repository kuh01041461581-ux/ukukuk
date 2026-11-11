import numpy as np, itertools
from PIL import Image
d=open('/home/user/ukukuk/seoultycoon/work/st2/yx.d','rb').read()
rects=[tuple(d[132+4*i:136+4*i]) for i in range((len(d)-132)//4)]
groups=[]
for r in rects:
    if r[0]==0 and r[2]==96 and r[3]>=19: groups.append([r])
    elif groups: groups[-1].append(r)
def solve(k, sheetpath='/tmp/pk/b%d.d'):
    S=np.asarray(Image.open(sheetpath%k).convert('RGBA')).astype(float)
    g=groups[k]; main=g[0]; pcs=[p for p in g[1:] if p[2]>=4 and p[3]>=4]
    def crop(r): x,y,w,h=r; return S[y:y+h,x:x+w]
    # 합성: 위로 쌓는다. 현재 맨 윗줄 = (xoff, row array)
    stack_top_row=crop(main)[0]; top_x=0   # main 첫줄(시트 y=main.y), 합성 x0=0
    placed=[(main,0,0)]; ycur=0; remaining=pcs[:]
    while remaining:
        best=None
        for p in remaining:
            c=crop(p); bot=c[-1]  # 조각 맨 아랫줄
            opq=np.where(bot[:,3]>0)[0]
            if len(opq)==0: continue
            for dx in range(-40,70):
                sc=0;n=0;bad=0
                for i in opq:
                    xx=dx+i-top_x
                    if 0<=xx<len(stack_top_row) and stack_top_row[xx,3]>0:
                        diff=np.abs(bot[i,:3]-stack_top_row[xx,:3]).sum(); sc+=1/(1+diff/48); n+=1
                    else: bad+=1
                score=sc-2*bad
                if best is None or score>best[0]: best=(score,p,dx,n,bad)
        if best is None: break
        score,p,dx,n,bad=best
        ycur-=p[3]; placed.append((p,dx,ycur))   # dx: 합성 x (메인 왼쪽=0 기준) = dx
        stack_top_row=crop(p)[0]; top_x=dx
        remaining.remove(p)
    return placed
if __name__=='__main__':
    import sys
    for k in map(int,sys.argv[1:]):
        print(k,groups[k][:1]); 
        for p,dx,y in solve(k)[1:]: print('   ',p,'dx',dx,'y',y)
