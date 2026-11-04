"""기존 안드로이드 APK 의 매니페스트에 targetSdkVersion 을 넣고(없으면 최신 안드로이드가 설치를 거부), 다시 서명한다.
사용(함수): retarget(원본.apk, 출력.unsigned, target='28')"""
import zipfile,glob,os,sys,pyaxml,logging
logging.disable(logging.CRITICAL)
ANDROID='{http://schemas.android.com/apk/res/android}'
def retarget(src,dst,target='28'):
    z=zipfile.ZipFile(src)
    a=pyaxml.AXML.from_axml(z.read('AndroidManifest.xml')); x=a.to_xml()
    us=x.find('uses-sdk'); us.set(ANDROID+'targetSdkVersion',target)
    app=x.find('application')
    # targetSdk 28 부터는 옛 Apache HTTP 클래스가 기본 제공되지 않으므로, 있으면 쓸 수 있게 선언(없어도 설치/실행에 지장 없음)
    if not any(e.get(ANDROID+'name')=='org.apache.http.legacy' for e in app.findall('uses-library')):
        lib=type(app).makeelement(app,'uses-library',{}) if False else app.makeelement('uses-library',{})
        app.append(lib); lib.set(ANDROID+'name','org.apache.http.legacy'); lib.set(ANDROID+'required','false')
    b=pyaxml.AXML(); b.from_xml(x); new=b.pack()
    with zipfile.ZipFile(dst,'w') as o:
        for i in z.infolist():
            if i.filename.startswith('META-INF/'): continue
            data=new if i.filename=='AndroidManifest.xml' else z.read(i.filename)
            zi=zipfile.ZipInfo(i.filename,i.date_time); zi.compress_type=i.compress_type; zi.external_attr=i.external_attr
            o.writestr(zi,data)
