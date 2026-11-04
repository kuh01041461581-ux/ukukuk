/* 서무 종합프로그램 공통 스크립트 (모든 도구 화면에 같이 들어간다) */
(function(){
  /* 1) 셸의 다크모드 신호 받기 */
  window.addEventListener('message',function(e){
    var d=e.data;if(!d||!d.__seomuTheme)return;
    document.documentElement.classList.toggle('seomu-dark',d.__seomuTheme==='dark');
    /* 자체 data-theme 를 쓰는 도구(수료증 취합)도 같이 맞춘다 */
    if(document.documentElement.hasAttribute('data-theme'))document.documentElement.setAttribute('data-theme',d.__seomuTheme==='dark'?'dark':'light');
  });
  try{window.parent.postMessage({__seomuReady:1},'*');}catch(e){}

  /* 2) 큰 라이브러리는 필요할 때만 내려받는다 (첫 화면이 CDN 때문에 멈추지 않게)
        같은 주소는 도구끼리 브라우저 캐시를 같이 쓴다 */
  var CD='https://cdnjs.cloudflare.com/ajax/libs/';
  var LIBS={
    xlsx:{u:CD+'xlsx/0.18.5/xlsx.full.min.js',g:'XLSX'},
    jszip:{u:CD+'jszip/3.10.1/jszip.min.js',g:'JSZip'},
    exceljs:{u:CD+'exceljs/4.4.0/exceljs.min.js',g:'ExcelJS'},
    pdf:{u:CD+'pdf.js/3.11.174/pdf.min.js',g:'pdfjsLib',after:function(){
      try{window.pdfjsLib.GlobalWorkerOptions.workerSrc=CD+'pdf.js/3.11.174/pdf.worker.min.js';}catch(e){}
    }}
  };
  var pending={};
  window.seomuLoad=function(name){
    var L=LIBS[name];
    if(!L)return Promise.resolve(false);
    if(window[L.g]){if(L.after)L.after();return Promise.resolve(true);}
    if(pending[name])return pending[name];
    pending[name]=new Promise(function(res){
      var s=document.createElement('script');
      s.src=L.u;s.async=true;
      s.onload=function(){if(L.after)L.after();res(!!window[L.g]);};
      s.onerror=function(){delete pending[name];res(false);};
      document.head.appendChild(s);
    });
    return pending[name];
  };

  /* 3) 화면 밖에 파일을 놓아도 브라우저가 파일을 열어 버려 작업이 날아가지 않게 */
  ['dragover','drop'].forEach(function(t){
    window.addEventListener(t,function(e){
      if(e.dataTransfer&&Array.prototype.indexOf.call(e.dataTransfer.types||[],'Files')>=0)e.preventDefault();
    });
  });

  /* 4) 글꼴(Pretendard, 필요한 글자만 조각으로 받음)은 첫 그림을 막지 않게 뒤늦게 불러온다 (그 전엔 윈도우 기본 한글 글꼴) */
  var f=document.createElement('link');
  f.rel='stylesheet';f.media='print';
  f.href='https://cdn.jsdelivr.net/gh/orioncactus/pretendard@v1.3.9/dist/web/variable/pretendardvariable-dynamic-subset.min.css';
  f.onload=function(){f.media='all';};
  document.head.appendChild(f);

  /* 5) 단계가 있는 도구(.tabbtn 단계 + .tab 화면)에는 아래쪽에 '이전 · 다음' 단추를 붙인다 */
  window.seomuStepNav=function(opt){
    var btns=[].slice.call(document.querySelectorAll(opt.btns));
    if(btns.length<2)return;
    var host=document.querySelector(opt.host);if(!host)return;
    var bar=document.createElement('div');bar.className='seomu-stepnav';
    bar.innerHTML='<button type="button" class="btn outline" data-d="-1">← 이전 단계</button><span class="mid"></span><button type="button" class="btn" data-d="1">다음 단계 →</button>';
    host.appendChild(bar);
    var prev=bar.querySelector('[data-d="-1"]'),next=bar.querySelector('[data-d="1"]'),mid=bar.querySelector('.mid');
    function cur(){for(var i=0;i<btns.length;i++)if(btns[i].classList.contains('active'))return i;return 0;}
    function sync(){
      var i=cur(),b=btns[i];
      prev.style.visibility=i===0?'hidden':'visible';
      next.style.visibility=i===btns.length-1?'hidden':'visible';
      mid.textContent=(i+1)+' / '+btns.length+' 단계';
      if(i>0)prev.textContent='← '+(btns[i-1].getAttribute('data-title')||btns[i-1].textContent.trim());
      if(i<btns.length-1)next.textContent=(btns[i+1].getAttribute('data-title')||btns[i+1].textContent.trim())+' →';
      var sc=document.scrollingElement||document.documentElement;
      if(opt.scroller)opt.scroller.scrollTop=0;else sc.scrollTop=0;
    }
    bar.addEventListener('click',function(e){
      var t=e.target.closest('button');if(!t)return;
      var j=cur()+parseInt(t.getAttribute('data-d'),10);
      if(btns[j])btns[j].click();
    });
    btns.forEach(function(b){b.addEventListener('click',function(){setTimeout(sync,0);});});
    sync();
  };
})();
