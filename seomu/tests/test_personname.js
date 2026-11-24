// node seomu/tests/test_personname.js  — 이름 걸러내기 규칙 시험 (가짜 이름만 사용)
const assert = require('assert');
const P = require('../shared/personname.js');

const yes = ['홍길동', '김민지', '소성국', '박상구', '정원', '이원', '남궁민', '선우진', '김 철 수', '홍길동(주무관)', '최국', '한소', '오청', '김동'];
const no = ['문화유산과', '총무과', '세무팀', '주무관', '팀장', '문화유산보존계', '행정지원국', '기획예산담당관', '익산시', '합계', '소계',
  '홍길동 문화유산과', '', '12', 'ABC', '복지여성국', '환경녹지과', '시설6급', '도서관', '시청'];
yes.forEach(s => assert.ok(P.seomuLooksLikePerson(s), '사람이어야 함: ' + s));
no.forEach(s => assert.ok(!P.seomuLooksLikePerson(s), '사람이 아니어야 함: ' + JSON.stringify(s)));

// 한 칸 안: 이름 밑에 과 이름
let r = P.seomuExtractPerson('홍길동\n문화유산과');
assert.deepStrictEqual(r, { name: '홍길동', org: '문화유산과' });
r = P.seomuExtractPerson('홍길동\r\n주무관');
assert.strictEqual(r.name, '홍길동');
r = P.seomuExtractPerson('문화유산과');           // 이름 칸이 과 이름뿐인 행
assert.strictEqual(r.name, '');
r = P.seomuExtractPerson('주무관');
assert.strictEqual(r.name, '');
r = P.seomuExtractPerson('김영희(문화유산과)');   // 괄호 안 글은 버린다
assert.strictEqual(r.name, '김영희');

// 표에서 모은 부서 이름은 이름 칸에 나와도 사람이 아님
const rows = [['순번', '소속', '성명'], [1, '문화유산과', '홍길동'], [2, '문화재팀', '김영희']];
const orgs = P.seomuCollectOrgs(rows);
assert.ok(orgs.has('문화유산과') && orgs.has('문화재팀'));
assert.ok(!P.seomuLooksLikePerson('문화재팀', orgs));
assert.ok(P.seomuLooksLikePerson('홍길동', orgs));

console.log('personname OK  (사람 ' + yes.length + ' / 비사람 ' + no.length + ')');
