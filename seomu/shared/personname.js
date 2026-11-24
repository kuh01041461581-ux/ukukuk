/* ============================================================
   사람 이름 걸러내기 (관외출장 · 기간제 인부임 공용)
   표에서 "성명" 칸을 읽을 때, 사람 이름 밑에 붙어 있는 과·팀 이름이나 직급(주무관 …)을
   사람으로 잘못 읽지 않게 한다.
     - 한 칸 안에 줄바꿈으로 "홍길동 / 문화유산과" 가 들어 있는 경우 → 이름만 취한다(과 이름은 소속 힌트)
     - 이름 칸 자체가 "문화유산과"·"총무팀"·"주무관" 인 행 → 사람이 아니므로 건너뛴다
   사람 이름 판정: 한글 2~4글자, 알려진 직급이 아니고, 부서 이름 꼴(…과/…팀/…담당관/…센터 등)이 아닐 것.
   '소성국'·'정원'처럼 국·원·소·청으로 끝나는 사람 이름은 살리기 위해, 한 글자 끝말(국·실·계·소·청·처·부·단·반)은
   '총무·문화·복지' 같은 부서 말뿌리가 앞에 붙을 때만 부서로 본다.
   ============================================================ */
var SEOMU_TITLES = ['주무관','팀장','과장','국장','계장','담당','사무관','서기관','부시장','시장','부군수','군수','실장','센터장','소장','주사','주사보','서기','서기보','연구사','연구관','공무직','기간제','직원','담당자','출장자','동승자','운전자','공무원','합계','소계','총계','비고','성명','이름','직급','직위','소속','부서','구분','연번','순번','대상자','근로자','근로자명','인원','시청','군청','구청','도청','본청','의회','사업소'];
var SEOMU_ORG_STRONG = /(과|팀|담당관|센터|사업소|위원회|사무소|관리소|지원단|본부|보존계|사업단|추진단|의회|도서관|박물관|보건소|주민센터|시청|군청|구청|도청|[가-힣]시|[가-힣]군)$/;
var SEOMU_ORG_STEMS = '총무|기획|행정|문화|복지|환경|건설|안전|세무|민원|보건|농정|산업|교통|관광|체육|감사|예산|인사|교육|여성|건축|도시|공원|수도|경제|투자|홍보|정보|시설|재난|회계|자치|지원|유산|보존|기록|청소|위생|토지|지적|세정|징수|산림|녹지|축산|농업|해양|도로|하천|에너지|일자리|청년|노인|보훈|전략|운영|관리|개발|정책|협력|통합|혁신|법무|계약|공보|비서|전문|종합|공공|시민|건강|생활|주민|상하수도|재무|특별|사회|가정|부동산|재산|전산|스마트|디지털|공유재산|문화유산|문화관광|행정지원|기획예산|자치행정|복지여성|환경녹지|건설교통|도시건설|농업기술|정보통신|도시재생';
var SEOMU_ORG_WEAK = new RegExp('^(' + SEOMU_ORG_STEMS + ')+(국|실|계|소|청|처|부|단|반|원)$');

/* 괄호 안 글(직급 등) 제거, 글자마다 띄운 이름("김 철 수")은 붙인다 */
function seomuNormName(s){
  s = String(s == null ? '' : s).replace(/[\(（\[].*?[\)）\]]/g, '').replace(/ /g, ' ').trim();
  if (/^([가-힣]\s+){1,3}[가-힣]$/.test(s)) s = s.replace(/\s+/g, '');
  return s;
}
/* 이 글자가 사람 이름처럼 보이는가. knownOrgs: 표에서 이미 소속으로 나온 말(Set 또는 배열) */
function seomuLooksLikePerson(raw, knownOrgs) {
  var s = seomuNormName(raw);
  if (!/^[가-힣]{2,4}$/.test(s)) return false;
  if (SEOMU_TITLES.indexOf(s) >= 0) return false;
  if (knownOrgs) {
    var has = (knownOrgs instanceof Set) ? knownOrgs.has(s) : knownOrgs.indexOf(s) >= 0;
    if (has) return false;
  }
  if (s.length >= 3 && SEOMU_ORG_STRONG.test(s)) return false;
  if (s.length >= 3 && SEOMU_ORG_WEAK.test(s)) return false;
  return true;
}
/* 이 글자가 부서(과·팀) 이름처럼 보이는가 (이름 아래 줄에서 소속을 읽을 때) */
function seomuLooksLikeOrg(raw) {
  var s = String(raw == null ? '' : raw).replace(/\s+/g, '');
  if (s.length < 3) return false;
  return SEOMU_ORG_STRONG.test(s) || SEOMU_ORG_WEAK.test(s);
}
/* 한 칸(줄바꿈 가능)에서 사람 이름만 뽑는다. 못 찾으면 name 은 ''.  org 는 같은 칸에 있던 부서 이름(소속 힌트). */
function seomuExtractPerson(cell, knownOrgs) {
  var lines = String(cell == null ? '' : cell).split(/\r\n|\r|\n|_x000D_/).map(function (x) { return x.trim(); }).filter(Boolean);
  var name = '', org = '';
  for (var i = 0; i < lines.length; i++) {
    if (!name && seomuLooksLikePerson(lines[i], knownOrgs)) { name = seomuNormName(lines[i]); continue; }
    if (!org && seomuLooksLikeOrg(lines[i])) org = lines[i].replace(/\s+/g, ' ');
  }
  return { name: name, org: org };
}
/* 표 전체에서 부서(과·팀) 이름으로 보이는 글자를 모아 둔다 → 이름 칸에 그 글자가 나오면 사람이 아님 */
function seomuCollectOrgs(rows) {
  var set = new Set();
  (rows || []).forEach(function (r) {
    (r || []).forEach(function (c) {
      if (typeof c !== 'string') return;
      c.split(/\r\n|\r|\n|_x000D_|[\/·,]|\s{2,}/).forEach(function (t) {
        t = t.trim().replace(/\s+/g, '');
        if (seomuLooksLikeOrg(t)) set.add(t);
        (t.match(/[가-힣]{2,6}(과|팀|담당관|센터|사업소)/g) || []).forEach(function (m) { set.add(m); });
      });
    });
  });
  return set;
}
if (typeof module !== 'undefined') module.exports = { seomuNormName, seomuLooksLikePerson, seomuLooksLikeOrg, seomuExtractPerson, seomuCollectOrgs };
