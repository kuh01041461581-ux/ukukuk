/* 우편 라벨지 앱 ↔ 서무 업무 자동화 셸: 다크 모드를 앱의 data-theme 로 전달 */
(function(){
  function apply(){
    var dark = document.documentElement.classList.contains('dark');
    document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
  }
  apply();
  new MutationObserver(apply).observe(document.documentElement, {attributes:true, attributeFilter:['class']});
  document.querySelectorAll('[data-char-src]').forEach(function(el){ el.src = window.CHARACTER_PNG; });
})();
