// 글자 크기 조절 (어르신용): 가− / 가+ 버튼. 선택은 이 휴대폰에 저장됩니다.
(function(){
  var L=[0.9,1,1.15,1.3],i=1;
  try{var v=parseInt(localStorage.getItem('fs'),10);if(v>=0&&v<L.length)i=v}catch(e){}
  function apply(){document.body.style.zoom=L[i];try{localStorage.setItem('fs',i)}catch(e){}
    var m=document.getElementById('fs-minus'),p=document.getElementById('fs-plus');if(m)m.disabled=i===0;if(p)p.disabled=i===L.length-1}
  function init(){
    var top=document.querySelector('.top');if(!top)return;
    var box=document.createElement('div');box.className='fsz';
    box.innerHTML='<button type="button" id="fs-minus" aria-label="글자 작게">가<small>−</small></button><button type="button" id="fs-plus" aria-label="글자 크게">가<small>+</small></button>';
    top.appendChild(box);
    box.querySelector('#fs-minus').onclick=function(){if(i>0){i--;apply()}};
    box.querySelector('#fs-plus').onclick=function(){if(i<L.length-1){i++;apply()}};
    apply()}
  if(document.readyState==='loading')document.addEventListener('DOMContentLoaded',init);else init();
})();
