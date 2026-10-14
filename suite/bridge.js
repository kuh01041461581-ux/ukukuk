/* 도구 페이지 ↔ 셸 연결 (빌드 때 각 도구 안에 끼워 넣습니다)
   · 내용 높이를 셸에 알려 iframe 높이를 맞춘다
   · 셸의 다크 모드 / 보고 있는 화면 영역(모달 위치)을 받아 적용한다
   · 셸 '설정'에서 저장한 API 키를 도구가 읽을 수 있게 한다  → window.suiteKey('kakao') */
(function(){
  var TOOL = (document.querySelector('meta[name="tool-id"]') || {}).content || '';
  var root = document.documentElement;
  document.body.classList.add('tool');

  function applyTheme(dark){ root.classList.toggle('dark', !!dark); }
  function applyViewport(v){
    if(!v) return;
    root.style.setProperty('--vp-top', Math.max(0, v.top) + 'px');
    root.style.setProperty('--vp-h', Math.max(200, v.height) + 'px');
  }
  try{ applyTheme(window.parent.document.documentElement.classList.contains('dark')); }catch(e){}

  window.addEventListener('message', function(e){
    var d = e.data;
    if(!d) return;
    if('__theme' in d) applyTheme(d.__theme === 'dark');
    if(d.__viewport) applyViewport(d.__viewport);
  });

  /* ---- 높이 알림 ---- */
  var last = -1, raf = 0;
  function post(){
    raf = 0;
    var b = document.body;
    var h = Math.ceil(b.getBoundingClientRect().height + parseFloat(getComputedStyle(b).marginTop || 0) + parseFloat(getComputedStyle(b).marginBottom || 0));
    if(h === last) return;
    last = h;
    try{ window.parent.postMessage({__tabHeight:h, __tabId:TOOL}, '*'); }catch(e){}
  }
  function schedule(){ if(!raf) raf = requestAnimationFrame(post); }
  window.addEventListener('load', function(){ schedule(); try{ window.parent.postMessage({__ready:true, __tabId:TOOL}, '*'); }catch(e){} });
  window.addEventListener('resize', schedule);
  if(window.ResizeObserver) new ResizeObserver(schedule).observe(document.body);
  if(window.MutationObserver) new MutationObserver(schedule).observe(document.body, {childList:true, subtree:true, attributes:true, characterData:true});

  /* ---- 셸 단축키(Ctrl+K, Alt+숫자)는 도구 화면 안에서 눌러도 동작 ---- */
  document.addEventListener('keydown', function(e){
    try{ if(window.parent.__suiteKey) window.parent.__suiteKey(e); }catch(err){}
  });

  /* ---- 공용 설정(API 키, 기본 정보) ---- */
  window.suiteInfo = function(name){
    try{
      var o = JSON.parse(localStorage.getItem('suite.info') || '{}');
      return (o && o[name]) || '';
    }catch(e){ return ''; }
  };
  window.suiteKey = function(name){
    try{
      var o = JSON.parse(localStorage.getItem('suite.keys') || '{}');
      return (o && o[name]) || '';
    }catch(e){ return ''; }
  };
})();
