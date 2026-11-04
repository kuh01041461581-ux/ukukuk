import zipfile,glob,os,sys,pyaxml,logging
logging.disable(logging.CRITICAL)
ANDROID='{http://schemas.android.com/apk/res/android}'
def retarget(src,dst,target='23'):
    z=zipfile.ZipFile(src)
    a=pyaxml.AXML.from_axml(z.read('AndroidManifest.xml')); x=a.to_xml()
    us=x.find('uses-sdk'); us.set(ANDROID+'targetSdkVersion',target)
    b=pyaxml.AXML(); b.from_xml(x if not isinstance(x,str) else x); new=b.pack()
    with zipfile.ZipFile(dst,'w') as o:
        for i in z.infolist():
            if i.filename.startswith('META-INF/'): continue   # 옛 서명 제거 (다시 서명)
            data=new if i.filename=='AndroidManifest.xml' else z.read(i.filename)
            zi=zipfile.ZipInfo(i.filename,i.date_time); zi.compress_type=i.compress_type; zi.external_attr=i.external_attr
            o.writestr(zi,data)
if __name__=='__main__': retarget(sys.argv[1],sys.argv[2])
