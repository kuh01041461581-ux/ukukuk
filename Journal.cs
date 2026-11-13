using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

// 다중분개 도우미: 지급결의 내용(한 줄에 한 건)을 넣으면 e호조(지방재정관리시스템) 방식의 재무회계 분개를 알려 줘요
// 인터넷 없이 키워드로 분류해요. 계정과목 중 ※ 표시는 일반적인 명칭이라 e호조 계정 검색으로 꼭 확인해야 해요.
internal static class JournalGen
{
	public sealed class Ln { public bool Dr; public string Acct; public long Amt; }
	public sealed class St { public string Name; public List<Ln> L = new List<Ln>(); }
	private sealed class Rule
	{
		public string Name, Tong, Acct, Kind, Wh, Note; public string[] Kw;
		public Rule(string name, string kind, string tong, string acct, string wh, string note, params string[] kw) { Name = name; Kind = kind; Tong = tong; Acct = acct; Wh = wh; Note = note; Kw = kw; }
	}
	// Kind: E 비용 · A 자산취득 · W 공사(건설중) · P 인건비 · S 선급금 · D 일상경비 · I 이자 · R 원금상환
	// Wh: "" 없음 · "기타" 8.8% · "사업" 3.3% · "일용" 일용근로
	private static readonly Rule[] Rules = {
		// ── 다단계 거래 (선급·일상경비·차입·공사) ──
		new Rule("선급금(선금) 지급", "S", "", "선급금", "", "선금을 줄 때는 비용이 아니라 선급금(자산)으로 먼저 잡고, 나중에 검수할 때 정산해요", "선금", "선급금", "착수금", "계약금"),
		new Rule("일상경비 전도금", "D", "", "소모품비", "", "일상경비는 교부 → 사용 → 잔액 반납이 각각 다른 분개예요", "일상경비", "전도금", "정액자금"),
		new Rule("차입금 이자", "I", "", "차입금이자비용", "", "", "차입금이자", "대출이자", "지방채이자", "이자 지급"),
		new Rule("차입금 원금 상환", "R", "", "장기차입금", "", "만기 1년 이내로 바뀌면 유동성장기차입금으로 대체돼 있을 수 있어요", "차입금 상환", "원금상환", "원금 상환", "채무상환"),
		new Rule("공사·시설비(건설중)", "W", "401-01 시설비", "건설중인일반유형자산", "", "기성·준공 검수 때 자산(건설중인일반유형자산)을 선택·등록해야 해요. 준공되면 건물 등으로 대체해요", "공사", "기성", "준공", "설계", "감리", "시공", "철거", "토목", "신축", "증축", "포장공사", "시설비"),
		// ── 인건비 ──
		new Rule("기간제근로자등인건비", "P", "101-04 기간제근로자등 보수", "기간제근로자등인건비", "", "4대보험 사업주 부담분은 법정부담금으로 따로 지출결의해요", "기간제", "시간선택제"),
		new Rule("무기계약근로자인건비", "P", "", "무기계약근로자인건비", "", "환경미화원 및 기타 무기계약근로자에게 지급하는 급여와 복리후생비예요. ① 시가지·가로·공원청소·쓰레기 수거 등에 종사하는 환경미화원(정규시간에 근무하는 건물·청사 청소원은 제외) ② 지자체가 연중 상시 고용하는 기타 무기계약근로자의 임금이에요. 계속 지급하는 상여금과 근로기준법상 각종 수당(시간외근무수당·휴일근무수당·연차수당 등)을 포함해요. ※퇴직전환금 예치금액은 '퇴직금전환금 및 예치금'에 계상해요", "무기계약", "공무직", "환경미화"),
		new Rule("무기계약근로자 보험료 부담금 등", "E", "", "무기계약근로자 보험료 부담금 등", "", "무기계약근로자의 고용보험료 부담금, 산업재해보험료 등, 국민연금 부담금, 국민건강보험 부담금, 퇴직금을 포함해요", "무기계약 보험", "무기계약근로자 보험", "무기계약 부담금", "무기계약 퇴직금"),
		new Rule("일용직 임금", "P", "101-04 기간제근로자등 보수", "기간제근로자등인건비", "일용", "일용근로소득은 일급에서 15만원을 빼고 6%의 45%만 원천징수해요(지방소득세 10% 별도)", "일용", "일당", "노임", "아르바이트", "단기근로"),
		new Rule("공무원 보수·수당", "P", "101-01 보수", "급여", "", "소득세·4대보험 등 공제액은 예수금으로 나눠 기록돼요(공제 내역 입력 필요)", "급여", "보수", "상여", "연가보상", "성과급", "초과근무", "시간외"),
		new Rule("통장·이장 수당", "E", "301-07 통장·이장·반장활동보상금", "기타운영비※", "", "", "통장", "이장", "반장수당"),
		// ── 일반운영비: 사무관리비(201-01) ──
		new Rule("일숙직비", "E", "201-01 사무관리비(운영수당-일·숙직비)", "일숙직비", "", "일·숙직 명을 받고 근무하는 자에 대한 수당이에요. 당직업무 부서에서 일정 기간 개산급(정액)으로 인출해 당일 일·숙직자에게 줄 수 있어요(일상경비)", "일숙직", "일·숙직", "숙직비", "일직비", "당직수당", "당직비", "숙직수당", "일직수당"),
		new Rule("공무원피복비", "E", "201-01 사무관리비(피복비)", "공무원피복비", "", "① 공무원 또는 무기계약근로자에게 지급·대여하는 상시착용(작업복 포함) 피복비 ② 침구 및 개인장구 구입비(교육기관에서 공무원에게 지급·대여하는 피복비) ③ 상시피복을 직접 제조·지급할 때 소요되는 재료비·노임·운반비·기타 제경비예요. ※무기계약근로자의 피복비는 무기계약근로자인건비에 계상해야 해요. 예) 신규직원 피복구입, 소방령·경 승진자 정모 구입, 소방공무원 단화·넥타이 구입", "피복", "작업복", "정모", "단화", "넥타이", "근무복", "제복", "안전화", "방한복", "하계복", "동계복", "정복", "개인장구"),
		new Rule("공무원급식비", "E", "201-01 사무관리비(급식비)", "공무원급식비", "", "공무원·무기계약에게 지급하는 ① 주식대·부식대, 주·부식 취사에 필요한 연료대·연초대 ② 그 부대경비(운반비·공고료 등) ③ 주·부식물 생산 제경비와 소모성 소도구 구입비 ④ 매식비: 기본업무 수행을 위한 특근급식비 / 각종 훈련 참여 직원 매식비 / 취사시설 없어 매식하는 경우 / 소방공무원 화재진압 출동 간식비 / 야간근무자 등 급식비(정규시간 전 출근·종료 후 근무자, 을지연습 비상훈련 참가자) / 휴일특별근무자(경보통제소·재해대책·소방상황실 교대근무자) 급식비예요", "급식", "급량", "야근 식", "야근식", "석식", "중식", "초과근무 식", "시간외 식", "주식대", "부식대", "매식", "특근 식", "간식", "식대"),
		new Rule("도서구입및인쇄비", "E", "201-01 사무관리비(일반수용비)", "도서구입및인쇄비", "", "① 관보구독료·법령 추록 구입비·리플릿 ② 자료·보고서·책자·각종 양식·전단 등 업무수행에 따른 일체의 인쇄물·유인물 제작비 ③ 신문·잡지·관보·팸플릿·포스터·마이크로필름 구입비 ④ 각종 대장·민원서식·고지서·행정봉투·조직기구표·스티커 제작비예요. ※도서관에 비치하는 장서 구입비는 도서관 집기비품(도서관 시설에 한함)이에요", "인쇄", "간행물", "신문", "구독", "책자", "리플릿", "팸플릿", "양식 제작", "고지서", "관보", "법령 추록", "스티커"),
		new Rule("소모품비", "E", "201-01 사무관리비(일반수용비)", "소모품비", "", "① 당직용 침구 구입 ② 부서 내 일반사무비와 기타용품비(필기구·용지대 등 잡품비) ③ 재물조사 대상이 아니라 집기비품으로 계상하기 부적합한 소모성 물품 구입비 ④ 범용 소프트웨어(한글·엑셀 등) 구입비 ⑤ 표창 관련 감사패·상패·위촉장 표지·내지 구입 제작, 명함, 고무인 제작 ⑥ 병원·수용기관·의료기관 시약대 ⑦ 수술 관계 약품·소모품비 ⑧ 소모성 의료기구·집기·기타 용품대 ⑨ 환자 관계 제용지(약포지·봉투카드대·진단서·체온표·처방전 등)예요. 자산수선유지 관련 비용은 해당 자산 수선유지비, 유산자산 복제품 제작·취득은 소모품비예요. ※현수막·배너 제작 구입은 홍보및광고비예요", "소모품", "사무용품", "문구", "필기구", "복사용지", "용지", "토너", "잉크", "복사", "청소용품", "당직용 침구", "감사패", "상패", "위촉장", "명함", "고무인", "범용 소프트웨어", "약품", "시약", "의료소모"),
		new Rule("홍보및광고비", "E", "201-01 사무관리비(일반수용비)", "홍보및광고비", "", "① 현수막·간판 등 안내홍보물 제작비와 기관(관서)의 간판·명패 제작비 ② TV·신문·잡지 등에 의한 공고료·광고료예요. ※물품·용역 등의 구입에 소요되는 공고료는 해당 자산의 취득 부대비용, 임차에 소요되는 공고료는 홍보및광고비, 홍보책자 인쇄비는 도서구입및인쇄비예요", "홍보", "광고", "공고료", "현수막", "배너", "간판", "명패", "방송 광고"),
		new Rule("지급수수료", "E", "201-01 사무관리비(일반수용비)", "지급수수료", "", "① 물품위탁취급수수료·업무대행수수료·등기 및 소송료 ② 소규모 용역제공 수수료(무인경비·청사관리대행·냉온수기소독료 등) ③ 전문검정기관 검정료, 토지·건물 감정료, 수질검사·콘크리트 강도시험 등 시험료 ④ 국선변호사·수임·고문변호사 변호료·수임료·보수, 외국어통역비 ⑤ 속기료·원고료·측량수수료 등 각종 용역의 반대급부적 수수료 ⑥ 주차료·차고료, 물품 운송대·포장비·하차료 ⑦ 외국환 대체송금·전송금·우편송금 수수료와 공모지방채 발행 제경비 ⑧ 세탁비·도메인등록수수료·온비드시스템 사용료 ⑨ 자동차(정기·종합)검사 수수료·세차비예요. ※정수기·비데 등 임차료·유지관리비는 임차료, 우체국택배요금은 지급수수료예요", "수수료", "무인경비", "청사관리", "소독", "검정료", "감정료", "시험료", "변호사", "수임료", "통역", "속기", "측량", "주차료", "차고료", "운송", "세탁", "도메인", "온비드", "정기검사", "종합검사", "세차", "택배", "송금", "등기", "소송료"),
		new Rule("임차료", "E", "201-01 사무관리비(임차료)", "임차료", "", "① 장소·건물 등의 일시 임차료와 무상 임차 시 청소비 ② 물건 보관용 간단한 창고 이용료 ③ 예비군 훈련·식목·권농·육림 등 각종 행사에 쓰는 버스·승용차 임차료 ④ 시설·장비 리스료(운용리스에 한함) ⑤ 임대차계약에 의한 토지·건물·시설·장비·물품 임차료예요. 주요 대상: 공기청정기 임차, 정수기·비데 유지관리비, 복사기 임차 등. ※임차보증금·전세보증금은 '보증금', 재해복구에 투입되는 장비 임차료는 복구 대상 자산의 수선유지비(예 도로복구 장비 임차료 → 도로수선유지비)예요", "임차", "임차료", "임대", "렌탈", "복합기", "복사기 임차", "대여", "리스", "정수기", "비데", "공기청정기", "창고 이용"),
		new Rule("직원교육훈련비", "E", "201-01 사무관리비(위탁교육비·운영수당)", "직원교육훈련비", "", "① 소속 공무원·직원의 교육·훈련을 위한 참석수당·강사수당·교통비·숙박비 등 실비변상적 지출(교육참석·워크샵(연찬회) 참석도 직원교육훈련비) ② 공무원 위탁교육 관련 경비(교육비·교통비·숙박비) ③ 지방공무원의 국가기관·타 지자체 등 공공 교육기관 위탁교육 경비 ④ 공무원교육에 출강하는 외래강사료(교통비·숙박비 포함) ⑤ 자체교육에 초빙한 외래강사 강사료예요", "직원교육", "공무원교육", "공무원 교육", "위탁교육", "교육훈련", "수강료", "연수", "워크샵", "연찬회", "외래강사", "직원 교육"),
		new Rule("기타교육훈련비", "E", "201-01 사무관리비", "기타교육훈련비", "", "① 민간인 교육·훈련을 위한 참석수당·강사수당·교통비·숙박비 등 실비변상적 지출 ② 시·도립대학의 전임 이외 외래강사료·시험관리비 ③ 민간 대상 교육(요가교실·노래교실 등) 초빙 강사료예요", "민간교육", "민간 교육", "요가교실", "노래교실", "시험관리", "주민교육", "주민 교육", "시민교육"),
		new Rule("위원회운영비", "E", "201-01 사무관리비(운영수당)", "위원회운영비", "기타", "법령·조례·사업추진을 위해 설치된 위원회 운영 제 경비예요. 위원회참석수당(교통비·숙박비 포함)과 심사수당(지방세 이의신청 심의·투융자심사 수당 등)이 해당돼요. 외부 위원은 기타소득 원천징수 대상이에요", "위원회", "참석수당", "심사수당", "심의", "투융자심사", "위원수당", "회의수당", "평가위원", "면접수당", "평가수당", "심사"),
		// ── 공공운영비(201-02) ──
		new Rule("공무원의료비", "E", "201-02 공공운영비(공무원 의료비)", "공무원의료비", "", "① 공무원·무기계약근로자 대상 의료비 ② 의무실·양호실 등 자체 의료시설의 의약품·소모성 의료기구 구입비 ③ 공상치료비 등이에요", "의료비", "건강검진", "공상치료", "의무실", "양호실", "예방접종"),
		new Rule("전기요금", "E", "201-02 공공운영비(공공요금 및 제세)", "전기요금", "", "지방자치단체가 사용한 전기요금이에요. ※고지서에 통합 부과되는 전력산업기반기금부담금·TV시청료를 포함해요", "전기요금", "전기료", "전력", "한전", "한국전력", "전기 요금", "전기세"),
		new Rule("통신요금", "E", "201-02 공공운영비(공공요금 및 제세)", "통신요금", "", "지방자치단체가 사용한 이동전화료·LM통화료·시내전화료·기본전화료·공중전화료·유선방송비예요. ※결합상품을 감안해 유·무선전화요금 외에 인터넷사용료·IPTV요금 등을 포함해요", "전화", "통신", "인터넷", "회선", "이동전화", "휴대폰", "핸드폰", "IPTV", "유선방송", "통화료"),
		new Rule("우편요금", "E", "201-02 공공운영비(공공요금 및 제세)", "우편요금", "", "지방자치단체가 사용한 국내우편료·국제우편료·전보료 등이에요. ★우체국택배요금은 지급수수료예요", "우편", "우체국 우편", "국제우편", "전보", "우표"),
		new Rule("상·하수도요금", "E", "201-02 공공운영비(공공요금 및 제세)", "상·하수도요금", "", "지방자치단체가 사용한 상수도료·하수도료예요. ※고지서에 통합 부과되는 물이용부담금 등을 포함해요", "상하수도", "상·하수도", "수도요금", "수도 요금", "하수도", "상수도", "수도료", "하수도료", "물이용부담금"),
		new Rule("제세", "E", "201-02 공공운영비(공공요금 및 제세)", "제세", "", "자동차세 등 법령 및 조례에 의하여 지불하는 세금이에요", "자동차세", "재산세", "취득세", "등록면허세", "세금 납부", "제세 "),
		new Rule("기타제세공과금", "E", "201-02 공공운영비(공공요금 및 제세)", "기타제세공과금", "", "① 정화조청소료·쓰레기봉투료·폐기물 스티커 ② 국가 등 정부에서만 제공하는 서비스 사용 대가인 수수료 성격의 비용 ③ 자산취득 부대비용으로 처리되지 않는 조달수수료 ④ 송달료 등 법원에 지급하는 각종 수수료 ⑤ 위에서 별도로 열거되지 않은 제세공과금이에요. 하이패스 충전은 기타제세공과금이에요. 주) 난방 도시가스요금은 난방연료비, 관용차 LPG·CNG 가스요금은 자동차연료비, 소유자산 가동·취사 목적 도시가스요금은 기타연료비, 출장비에 포함된 기차요금 등 교통요금은 출장비(포함되지 않는 교통요금만 기타제세공과금), 수입물품 취득 과정에서 세관장에게 낸 부가가치세·관세는 해당 물품의 취득 부대비용이에요", "공과금", "정화조", "쓰레기봉투", "폐기물 스티커", "송달료", "하이패스", "조달수수료", "공과금 납부", "인지"),
		new Rule("건물보험료및공제료", "E", "", "건물보험료및공제료", "", "지자체가 사용 중인 건물에 대한 보험료·공제료와 지방재정공제회의 건물·시설물재해복구공제 관련 지출이에요", "건물보험", "건물 보험", "화재보험", "시설물재해", "재해복구공제"),
		new Rule("자동차보험료및공제료", "E", "", "자동차보험료및공제료", "", "지자체가 사용 중인 자동차에 대한 보험료 및 공제료예요", "자동차보험", "차량보험", "자동차 보험", "차량 보험"),
		new Rule("기타보험료및공제료", "E", "", "기타보험료및공제료", "", "지방재정공제회의 지방관공선(선박) 재해복구공제·손해배상공제, 가스사고배상책임보험료, 대여유물 종합보험료 등 별도로 열거되지 않은 보험료·공제료예요. 주) 자산 취득 시 운송과정의 해상보험료 등은 자산의 부대비용, 근로자 퇴직금 적립 목적의 퇴직보험료는 퇴직금전환금 및 예치금이에요", "보험료", "공제료", "배상책임보험", "손해배상공제", "유물 보험"),
		new Rule("난방연료비", "E", "201-02 공공운영비(연료비)", "난방연료비", "", "건물 등의 난방 목적으로 지출하는 유류대·도시가스요금이에요", "난방", "보일러", "등유", "난방유", "도시가스 난방", "난방 도시가스", "난방비"),
		new Rule("자동차연료비", "E", "201-02 공공운영비(차량·선박비)", "자동차연료비", "", "관용차량의 운행을 위해 지출하는 유류대·LPG가스요금·CNG가스요금이에요", "주유", "휘발유", "경유", "차량 유류", "LPG", "CNG", "관용차 유류", "차량 연료", "자동차연료"),
		new Rule("기타연료비", "E", "201-02 공공운영비(연료비)", "기타연료비", "", "① 취사 목적 도시가스요금 등 ② 원동기 등 동력장치·중장비·항공기에 소요되는 유류대 관련 비용(건설기계는 기계장치로 분류하고 그 가동 유류비는 기타연료비) ③ 위에서 별도로 열거되지 않은 연료비예요", "취사", "도시가스", "중장비 유류", "연료비", "연료대", "발전기 유류"),
		new Rule("협회비및부담금", "E", "", "협회비및부담금", "", "① 의료원 공공진료 부담금 등 법령·조례에 의한 부담금 ② 법령 또는 협약에 의한 협회비 등의 부담금 ③ 환경개선부담금 등 시설 사용에 따른 각종 법정 부담금이에요. 지방자치단체협의회 부담금, 민간에 대한 부담금도 같은 계정이에요", "협회비", "부담금", "회비", "협의회", "환경개선부담금", "분담금"),
		new Rule("행사운영비", "E", "201-03 행사운영비", "행사운영비", "", "① 행사운영을 위한 일체의 일반운영비 ② 초청장·홍보유인물·현수막·상패 제작 등 행사개최에 따른 각종 일반수용비 ③ 행사 개최를 위한 시설·장비·물품의 임차료 ④ 행사 지원을 위한 강사료예요. (현수막만 제작이면 홍보및광고비와 구분해요)", "행사", "초청장", "축제", "기념식", "체육대회", "개최", "강사료", "강의료", "원고료", "자문료", "사례금"),
		new Rule("기타운영비", "E", "", "기타운영비", "", "① 망실·도난·미회수금의 보전금 ② 법령에 의하여 증인·감정인·참고인·공술인에 대한 실비변상금 등이에요", "망실", "도난", "미회수금", "증인", "참고인", "감정인", "공술인", "실비변상"),
		new Rule("원·정수구입비", "E", "", "원·정수구입비", "", "지방자치단체가 상수도 공급을 위해 수자원공사 등으로부터 구매하는 용수·원수·정수의 구입비용이에요", "원수", "정수 구입", "용수 구입", "수자원공사", "원·정수"),
		// ── 여비·업무추진비 ──
		new Rule("국내출장여비", "E", "202-01 국내여비", "국내출장여비", "", "① 기본업무 수행을 위해 지급되는 관할구역 내 출장여비 ② 공사현장·사업장 감독관의 현장체제비 ③ 공무원 인사이동에 따른 이전경비예요. ※시설부대비(401-03)에 포함되는 여비는 자산가액에 가산해요", "출장", "여비", "숙박", "교통비", "국내여비", "KTX", "항공", "시외버스", "이전경비", "현장체제비"),
		new Rule("청원경찰등국내출장여비", "E", "202-01 국내여비", "청원경찰등국내출장여비", "", "「공무원여비규정」에 의한 공무원이 아닌 자(청원경찰·무기계약근로자 등)의 국내출장여비예요", "청원경찰", "무기계약 출장", "무기계약근로자 출장", "청원경찰 출장"),
		new Rule("시책추진업무추진비", "E", "203-03 시책추진업무추진비", "시책추진업무추진비", "", "식사는 1인 1회 5만원 이하, 현금 사용 금지, 건당 50만원 이상은 증빙에 상대방 소속·성명을 적어요", "시책추진", "시책 추진", "시책업무추진"),
		new Rule("부서추진업무추진비", "E", "203-04 부서운영 업무추진비", "부서추진업무추진비", "", "식사는 1인 1회 5만원 이하, 현금 사용 금지, 건당 50만원 이상은 증빙에 상대방 소속·성명을 적어요", "부서추진", "부서운영", "부서 업무추진", "부서 추진", "부서 간담"),
		new Rule("업무추진비(간담회·격려)", "E", "203 업무추진비", "업무추진비※", "", "기관운영/시책추진/부서운영 중 사업 성격에 맞는 통계목(시책추진·부서추진 등)을 골라요. 식사는 1인 1회 5만원 이하, 현금 사용 금지, 건당 50만원 이상은 증빙에 상대방 소속·성명을 적어요", "간담회", "격려", "축의", "부의", "업무추진", "회식", "오찬", "만찬", "조의", "식사비", "다과비"),
		// ── 용역·보조금·기타 ──
		new Rule("연구·전산개발 용역", "E", "207 연구개발비", "연구개발비", "사업", "연구용역은 207-01, 정보화·시스템 개발은 207-02예요. 개인 대상이면 사업소득 3.3%를 떼요", "연구용역", "정보화", "전산개발", "시스템 개발", "소프트웨어 개발", "프로그램 개발"),
		new Rule("개인 용역·번역·촬영 등", "E", "201-01 사무관리비(소규모 용역비)", "지급수수료", "", "속기료·원고료·번역·통역·측량 등 각종 용역의 대가는 지급수수료예요. 개인에게 주면 사업소득 3.3%(일시적이면 기타소득 8.8%), 사업자·법인이면 원천징수가 없어요. 금액 앞에 '3.3' 또는 '8.8'을 쓰면 계산해요", "용역", "번역", "촬영", "디자인", "컨설팅", "대행", "녹취"),
		new Rule("민간의료보조금", "E", "307 민간이전", "민간의료보조금", "", "민간에 대한 의료비 보조금(의료비·장제비·해산비·출산보조금·재활치료비·진단비·재활보조기구 구입비·저소득층 건강보험료 등)이에요", "의료비 보조", "장제비", "해산비", "출산보조", "재활치료", "진단비", "재활보조기구", "건강보험료 지원"),
		new Rule("민간생계지원보조금", "E", "307 민간이전", "민간생계지원보조금", "", "민간에 생계비 지원을 위한 보조금(각종 생계비·주거비·급식지원·양곡구입보조금·양육수당·가정위탁아동 양육비·기초노령연금·장애수당·난방비·연료비·전기료·자활장려금·우유급식 보조 등)이에요. 시설급여 중 생계비도 개인에게 지급되는 성격이라 포함해요", "생계", "주거비", "양곡", "양육수당", "기초노령연금", "장애수당", "자활장려금", "우유급식", "가정위탁"),
		new Rule("민간복지시설보조금", "E", "307 민간이전", "민간복지시설보조금", "", "민간복지시설(사회복지단체)에 대한 보조금(시설 운영비·인건비 보조금, 복지시설 고유사업 보조금, 시설 난방보조금, 입양수수료 등)이에요", "복지시설", "시설 운영비 보조", "시설 인건비 보조", "입양수수료", "시설 난방"),
		new Rule("민간사회단체보조금", "E", "307 민간이전", "민간사회단체보조금", "", "민간사회단체에 대한 보조금이에요", "사회단체", "민간단체 보조", "단체 보조금"),
		new Rule("기타민간보조금", "E", "307 민간이전", "기타민간보조금", "", "의료·생계·복지시설 보조금에 포함되지 않는 보조금이에요. 기타 사회단체 보조금, 장수수당, 교통이용보조금, 행려자 귀향여비, 교육비, 교복비, 학습재료비, 신문구독비, 직업훈련비, 보육료, 가정보육교사 이용료, 소년소녀가정 위문금 등이에요", "보조금", "지원금", "위탁금", "장수수당", "교통이용", "교복", "학습재료", "보육료", "위문금"),
		new Rule("시·도비 보조금 등(정부간 이전)", "E", "308 자치단체등 이전", "시·도비보조금", "", "", "시도비", "시·도비", "시비 보조", "도비 보조", "자치단체 부담금"),
		new Rule("손해배상금", "E", "", "손해배상금및국가배상금", "", "소송이면 확정 시점에 우발손실 부채를 먼저 잡았는지 확인해요", "배상", "손해배상", "소송", "합의금"),
		new Rule("포상금·상금", "E", "303 포상금", "포상금※", "기타", "상금·부상은 기타소득 원천징수 대상이 될 수 있어요", "포상금", "성과금", "상금", "부상"),
		new Rule("비품·장비 구입(자산)", "A", "405-01 자산 및 물품취득비", "집기비품", "", "단가가 기준금액 미만이면 소모품비로 비용처리하는 지자체가 많아요(재무회계 운영규정 확인)", "비품", "컴퓨터", "노트북", "모니터", "프린터", "책상", "의자", "캐비닛", "에어컨", "냉장고", "복사기", "TV", "카메라", "서버", "장비", "기계", "자동차", "차량", "집기"),
		new Rule("도서 구입(자산)", "A", "405-02 도서구입비", "집기비품", "", "도서관에 비치하는 장서 구입비는 도서관 집기비품(도서관 시설에 한함)이에요. 일반 간행물·신문은 도서구입및인쇄비예요", "도서구입", "도서 구입", "도서관", "서적", "장서"),
	};

	// 수선유지비: 시설 종류별로 계정이 나뉘어요 (수선·수리·정비 같은 말이 있을 때만 이 표를 먼저 봐요)
	private const string RepNote = "수선유지비는 시설물의 정상적 기능수행을 위한 원상 회복·능률 유지 수선을 말해요. 시설 자체의 내용연수를 늘리거나 효익을 극대화하는 지출은 자본적 지출이라 해당 자산의 장부가액에 가산해요. 물리적 기능 유지와 관련 없는 일반운영비(제세공과금 등)는 해당 과목으로 분류해요";
	private static Rule Rp(string acct, string desc, params string[] kw) { return new Rule(acct, "E", "", acct, "", desc.Length > 0 ? desc + ". " + RepNote : RepNote, kw); }
	private static readonly Rule[] Repair = {
		Rp("건물수선유지비", "일반유형자산(청사 등)에 포함되는 건물과 건물 내외에 부착·부속되어 건물 효용을 증대시키는 부속시설물의 수선유지비", "건물", "청사", "사무실", "지붕", "외벽", "도배", "창호", "배관", "보일러", "승강기", "엘리베이터", "전기시설", "냉난방"),
		Rp("구축물수선유지비", "일반유형자산에 포함되는 구축물(에너지공급시설·홍보 및 안내시설·방송중계탑·민방위급수시설·옥외저유시설·굴뚝·담장 등) 수선유지비", "구축물", "담장", "굴뚝", "중계탑", "급수시설", "옥외저유"),
		Rp("기계장치수선유지비", "!!건설과·건축과 이외 부서 선택금지!! 일반유형자산에 포함되는 기계장치(제조설비·건설기계·농업기계·군수차량·궤도 또는 공중선 운행차량 등)의 수선유지비", "기계장치", "건설기계", "굴삭기", "제조설비", "농업기계"),
		Rp("차량운반구수선유지비", "일반유형자산에 포함되는 차량운반구(자동차·이륜차 포함, 선박, 항공기)의 수선유지에 소요되는 금액", "차량", "자동차", "승용차", "선박", "항공기", "이륜차", "엔진오일", "타이어", "차량운반구"),
		Rp("집기비품수선유지비", "", "집기", "비품", "복사기 수리", "컴퓨터", "프린터", "에어컨", "책상 수리"),
		Rp("임차개량자산수선유지비", "일반유형자산에 포함되는 임차개량자산(임차한 자산을 증설·구조적 개선 등으로 가치증가·성능개선한 자산)의 수선유지에 소요되는 비용", "임차개량"),
		Rp("입목수선유지비", "일반유형자산에 포함되는 입목의 수선유지비", "입목", "수목", "가로수", "전정"),
		Rp("기타비유동자산수선유지비", "기타비유동자산 중 전용소프트웨어(Oracle·DB·인터넷홈페이지·전자도서관운영시스템 등) 등의 기능유지를 위해 소요되는 비용", "소프트웨어", "홈페이지", "시스템 유지", "전산 유지", "DB", "전용소프트웨어", "전산시스템"),
		Rp("도로수선유지비", "도로를 구성하는 입목·건물·구축물·기계장치·차량운반구·집기비품·기타일반유형자산의 수선유지비", "도로", "포장", "보도", "가로등", "교량", "터널", "신호등"),
		Rp("도시철도수선유지비", "도시철도를 구성하는 시설물의 수선유지비", "도시철도", "지하철"),
		Rp("상수도시설수선유지비", "상수도시설을 구성하는 시설물의 수선유지비", "상수도시설", "정수장", "배수지", "상수관", "급수관"),
		Rp("수질정화시설수선유지비", "수질정화시설을 구성하는 시설물의 수선유지비", "수질정화", "하수처리", "하수관", "정화시설"),
		Rp("하천부속시설수선유지비", "하천부속시설을 구성하는 시설물의 수선유지비", "하천", "제방", "수문"),
		Rp("폐기물처리시설수선유지비", "폐기물처리시설을 구성하는 시설물의 수선유지비", "폐기물처리", "소각장", "매립장"),
		Rp("재활용시설수선유지비", "재활용시설을 구성하는 시설물의 수선유지비", "재활용시설", "재활용선별"),
		Rp("농수산기반시설수선유지비", "농수산기반시설을 구성하는 시설물의 수선유지비", "농수산", "농로", "저수지", "배수로", "수산"),
		Rp("댐수선유지비", "댐을 구성하는 시설물의 수선유지비", "댐"),
		Rp("어항및항만시설수선유지비", "", "어항", "항만", "방파제", "부두"),
		Rp("기타사회기반시설수선유지비", "앞에서 열거하지 않은 기타 사회기반시설을 구성하는 시설물의 수선유지비", "사회기반시설"),
		Rp("도서관수선유지비", "도서관을 구성하는 시설물의 수선유지비", "도서관"),
		Rp("주차장수선유지비", "주차장을 구성하는 시설물의 수선유지비", "주차장"),
		Rp("공원수선유지비", "공원을 구성하는 시설물의 수선유지비", "공원", "놀이터", "산책로"),
		Rp("박물관및미술관수선유지비", "박물관 및 미술관을 구성하는 시설물의 수선유지비", "박물관", "미술관"),
		Rp("동물원수선유지비", "동물원을 구성하는 시설물의 수선유지비", "동물원"),
		Rp("수목원및휴양림수선유지비", "", "수목원", "휴양림"),
		Rp("문화및관광시설수선유지비", "문화 및 관광시설을 구성하는 시설물의 수선유지비", "문화시설", "관광", "문화회관", "공연장"),
		Rp("체육시설수선유지비", "체육시설을 구성하는 시설물의 수선유지비", "체육", "운동장", "수영장", "체육관"),
		Rp("사회복지시설수선유지비", "사회복지시설을 구성하는 시설물의 수선유지비", "복지시설", "복지관", "경로당", "어린이집", "보육시설"),
		Rp("의료시설수선유지비", "의료시설을 구성하는 시설물의 수선유지비", "의료시설", "보건소", "병원", "보건지소"),
		Rp("교육시설수선유지비", "교육시설을 구성하는 시설물의 수선유지비", "교육시설", "학교", "교육관"),
		Rp("기타주민편의시설수선유지비", "위에서 열거하지 않은 기타 주민편의시설을 구성하는 시설물의 수선유지비", "주민편의", "마을회관", "주민센터", "공용화장실"),
		Rp("유산자산수선유지비", "관리책임자산 중 유산자산(문화재·예술작품 등)의 수선유지비", "유산자산", "문화재", "예술작품", "사적"),
		Rp("자연자원수선유지비", "", "자연자원"),
		Rp("국공유재산수선유지비", "", "국공유재산"),
		Rp("기타유산자산수선유지비", "", "기타유산"),
		Rp("기타생물자산수선유지비", "", "생물자산"),
	};
	private static readonly string[] RepWords = { "수선", "수리", "유지보수", "보수공사", "정비", "도색", "방수", "누수", "수선유지" };

	private static readonly Regex Amt = new Regex(@"(\d{1,3}(?:,\d{3})+|\d+)\s*(만\s*원|천\s*원|만|원)", RegexOptions.Compiled);
	public static long ParseAmt(string s)
	{
		long v = 0; foreach (Match m in Amt.Matches(s)) { long n; string d = m.Groups[1].Value.Replace(",", ""); if (!long.TryParse(d, out n)) continue; string u = m.Groups[2].Value.Replace(" ", ""); v = u.StartsWith("만") ? n * 10000 : u.StartsWith("천") ? n * 1000 : n; }
		return v;
	}
	private static Rule Pick(string t)
	{
		Rule best = null; int bs = 0;
		bool repOk = Has(t, RepWords);
		if (!repOk && Has(t, "보수")) foreach (Rule r in Repair) foreach (string k in r.Kw) if (r.Acct != "건물수선유지비" && t.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) repOk = true;
		if (repOk)
		{
			foreach (Rule r in Repair) { int sc = 0; foreach (string k in r.Kw) if (t.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) sc += 1 + k.Length / 3; if (sc > bs) { bs = sc; best = r; } }
			return best != null ? best : Repair[0];
		}
		foreach (Rule r in Rules) { int sc = 0; foreach (string k in r.Kw) if (t.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) sc += 1 + k.Length / 3; if (sc > bs) { bs = sc; best = r; } }
		return best;
	}
	private static bool Has(string t, params string[] ks) { foreach (string k in ks) if (t.IndexOf(k, StringComparison.OrdinalIgnoreCase) >= 0) return true; return false; }
	// 원천징수액: [소득세, 지방소득세, 설명]
	public static long[] Withhold(string kind, long amt, out string desc)
	{
		desc = ""; long tax = 0;
		if (amt <= 0) { desc = "금액을 쓰면 원천징수액도 계산해 드려요"; return new long[] { 0, 0 }; }
		if (kind == "기타") { if (amt <= 125000) { desc = "지급액 125,000원 이하(기타소득금액 5만원 이하)는 원천징수 없음"; return new long[] { 0, 0 }; } tax = (long)Math.Floor(amt * 0.08); desc = "기타소득 8.8% (필요경비 60% 의제: 소득세 8% + 지방소득세 0.8%)"; }
		else if (kind == "사업") { tax = (long)Math.Floor(amt * 0.03); desc = "사업소득 3.3% (소득세 3% + 지방소득세 0.3%)"; }
		else if (kind == "일용") { long b = Math.Max(0, amt - 150000); tax = (long)Math.Floor(b * 0.06 * 0.45); if (tax < 1000) { tax = 0; desc = "일용근로 소득세 1,000원 미만은 소액부징수"; return new long[] { 0, 0 }; } desc = "일용근로 (일급-15만원)×6%×45% + 지방소득세 10%"; }
		long loc = (long)Math.Floor(tax * 0.1 / 10) * 10; return new long[] { tax, loc };
	}
	private static string Won(long v) { return v.ToString("N0") + "원"; }

	public sealed class Item { public string Text, Title, Tong, Note, WhDesc; public long Amt, Wh; public bool Matched; public List<St> S = new List<St>(); }

	public static Item Make(string line)
	{
		Item it = new Item { Text = line.Trim() }; long a = ParseAmt(line); it.Amt = a;
		Rule r = Pick(line);
		if (Has(line, "피복", "작업복", "정모", "단화", "안전화", "근무복", "제복", "방한복", "하계복", "동계복", "정복"))
		{
			if (Has(line, "무기계약", "공무직")) r = new Rule("무기계약근로자 피복비", "P", "", "무기계약근로자인건비", "", "무기계약근로자의 피복비는 피복비 계정이 아니라 무기계약근로자인건비에 계상해야 해요", "");
			else if (Has(line, "기간제")) r = new Rule("기간제근로자 피복비", "P", "101-04 기간제근로자등 보수", "기간제근로자등인건비", "", "기간제근로자의 피복비는 피복비 계정이 아니라 인건비로 잡아요", "");
			else if (Has(line, "감독")) r = new Rule("현장감독공무원 피복비", "E", "401-03 시설부대비", "공무원피복비", "", "공사기간·내용연수·실제 필요성·가격 적정성을 보고 조달청을 통해 우선 구매해요. 고가 등산용품은 살 수 없어요", "");
			else for (int q = 0; q < Rules.Length; q++) if (Rules[q].Acct == "공무원피복비") r = Rules[q];
		}
		if (r == null) { it.Title = "분류하지 못했어요"; it.Note = "내용을 조금 더 구체적으로 써 주세요 (예: 사무용품 구입, 강사료 지급, 출장비, 전기요금, 공사 기성금)"; return it; }
		it.Matched = true; it.Title = r.Name; it.Tong = r.Tong; it.Note = r.Note;
		string wh = r.Wh;
		if (wh == "" && r.Kind == "E" && Has(line, "강사료", "강의료", "원고료", "자문료", "사례금", "위원수당", "참석수당", "심사수당")) wh = "기타";
		if (Has(line, "3.3", "사업소득")) wh = "사업"; else if (Has(line, "8.8", "기타소득")) wh = "기타"; else if (Has(line, "일용", "일당")) wh = "일용";
		if (Has(line, "(주)", "㈜", "주식회사", "업체", "세금계산서", "법인", "사업자", "카드", "원천징수 없음", "원천 없음")) wh = "";
		if (r.Kind == "P" && r.Wh == "" && !Has(line, "3.3", "8.8")) wh = "";
		if ((r.Acct == "직원교육훈련비" || r.Acct == "기타교육훈련비") && wh == "" && Has(line, "강사료", "강사수당", "강의료", "원고료") && !Has(line, "(주)", "㈜", "주식회사", "업체", "세금계산서", "법인", "사업자", "카드")) wh = "기타";
		if (r.Kind == "S" || r.Kind == "D" || r.Kind == "I" || r.Kind == "R" || r.Kind == "W" || r.Kind == "A") wh = "";
		string dd = ""; long[] w = new long[] { 0, 0 }; if (wh != "") w = Withhold(wh, a, out dd); long wsum = w[0] + w[1]; it.Wh = wsum; it.WhDesc = wh == "" ? "" : dd + (wsum > 0 ? " → 소득세 " + Won(w[0]) + " + 지방소득세 " + Won(w[1]) : "");
		Func<bool, string, long, Ln> L = delegate(bool dr, string ac, long m) { return new Ln { Dr = dr, Acct = ac, Amt = m }; };
		string kind = r.Kind;
		if (kind == "E" || kind == "A" || kind == "P")
		{
			string pay = kind == "A" ? "일반미지급금" : "일반미지급비용";
			St s1 = new St { Name = "① 지출결의(검수)" }; s1.L.Add(L(true, r.Acct, a)); s1.L.Add(L(false, pay, a)); it.S.Add(s1);
			St s2 = new St { Name = "② 지급명령(이체)" }; s2.L.Add(L(true, pay, a)); if (wsum > 0) s2.L.Add(L(false, "예수금(원천징수)", wsum)); s2.L.Add(L(false, "현금과예금", a - wsum)); it.S.Add(s2);
			if (wsum > 0) { St s3 = new St { Name = "③ 원천세 납부(다음 달 10일까지)" }; s3.L.Add(L(true, "예수금(원천징수)", wsum)); s3.L.Add(L(false, "현금과예금", wsum)); it.S.Add(s3); }
		}
		else if (kind == "W")
		{
			St s1 = new St { Name = "① 선금·기성금 검수" }; s1.L.Add(L(true, r.Acct, a)); s1.L.Add(L(false, "일반미지급금", a)); it.S.Add(s1);
			St s2 = new St { Name = "② 지급명령(이체)" }; s2.L.Add(L(true, "일반미지급금", a)); s2.L.Add(L(false, "현금과예금", a)); it.S.Add(s2);
			St s3 = new St { Name = "③ 준공 시 자산 대체(별도 분개)" }; s3.L.Add(L(true, "건물 등 해당 자산※", 0)); s3.L.Add(L(false, "건설중인일반유형자산", 0)); it.S.Add(s3);
		}
		else if (kind == "S")
		{
			St s1 = new St { Name = "① 선급금 지급" }; s1.L.Add(L(true, "선급금", a)); s1.L.Add(L(false, "현금과예금", a)); it.S.Add(s1);
			St s2 = new St { Name = "② 정산(검수·지급 때, 별도 분개)" }; s2.L.Add(L(true, "해당 자산·비용 계정", 0)); s2.L.Add(L(false, "선급금", 0)); s2.L.Add(L(false, "현금과예금(잔액 지급분)", 0)); it.S.Add(s2);
		}
		else if (kind == "D")
		{
			St s1 = new St { Name = "① 전도금 교부" }; s1.L.Add(L(true, "일상경비전도금", a)); s1.L.Add(L(false, "현금과예금", a)); it.S.Add(s1);
			St s2 = new St { Name = "② 경비 사용(별도 분개)" }; s2.L.Add(L(true, "소모품비 등 해당 비용", 0)); s2.L.Add(L(false, "일상경비전도금", 0)); it.S.Add(s2);
			St s3 = new St { Name = "③ 잔액 반납(별도 분개)" }; s3.L.Add(L(true, "현금과예금", 0)); s3.L.Add(L(false, "일상경비전도금", 0)); it.S.Add(s3);
		}
		else if (kind == "I" || kind == "R")
		{
			St s1 = new St { Name = "① 지급" }; s1.L.Add(L(true, r.Acct, a)); s1.L.Add(L(false, "현금과예금", a)); it.S.Add(s1);
		}
		return it;
	}

	// 분개 사전: 모든 분류를 "지출 종류 → (차변)계정 / (대변)미지급" 형식으로 보여 줘요
	public static string Catalog()
	{
		StringBuilder sb = new StringBuilder("══ 분개 사전 (지출 종류별 · 지출결의 단계) ══\r\n\r\n");
		foreach (Rule r in Rules)
		{
			string cr = r.Kind == "A" || r.Kind == "W" ? "일반미지급금" : r.Kind == "S" || r.Kind == "D" || r.Kind == "I" || r.Kind == "R" ? "현금과예금" : "일반미지급비용";
			sb.Append("■ ").Append(r.Name); if (r.Tong.Length > 0) sb.Append("  [").Append(r.Tong).Append("]"); sb.Append("\r\n   (차) ").Append(r.Acct).Append(" / (대) ").Append(cr).Append("\r\n");
			sb.Append("   예: ").Append(string.Join(", ", r.Kw, 0, Math.Min(5, r.Kw.Length))).Append("\r\n"); if (r.Note.Length > 0) sb.Append("   ").Append(r.Note.Replace("\n", "\r\n   ")).Append("\r\n"); sb.Append("\r\n");
		}
		sb.Append("══ 수선유지비 (시설 종류별 계정 · 수선/수리/정비 등을 쓰면 먼저 적용) ══\r\n\r\n");
		foreach (Rule r in Repair) sb.Append("■ ").Append(r.Acct).Append("   (차) ").Append(r.Acct).Append(" / (대) 일반미지급비용   예: ").Append(string.Join(", ", r.Kw, 0, Math.Min(4, r.Kw.Length))).Append("\r\n");
		sb.Append("\r\n").Append(RepNote).Append("\r\n\r\n※ 표시는 계정명이나 연결이 확실하지 않은 항목이에요. e호조 계정과목 검색에서 확인하세요.");
		return sb.ToString();
	}

	public static string Run(string input)
	{
		string[] lines = input.Replace("\r", "").Split('\n'); List<Item> items = new List<Item>();
		foreach (string ln in lines) { if (ln.Trim().Length == 0) continue; items.Add(Make(ln)); }
		if (items.Count == 0) return "";
		StringBuilder sb = new StringBuilder(); int no = 0; bool qm = false; long total = 0;
		foreach (Item it in items)
		{
			no++; sb.Append("■ ").Append(no).Append(". ").Append(it.Text).Append("\r\n");
			if (!it.Matched) { sb.Append("   ※ ").Append(it.Title).Append(" — ").Append(it.Note).Append("\r\n\r\n"); continue; }
			total += it.Amt;
			sb.Append("   분류: ").Append(it.Title); if (it.Tong.Length > 0) sb.Append("  /  세출 통계목: ").Append(it.Tong); sb.Append("\r\n");
			if (it.WhDesc.Length > 0) sb.Append("   원천징수: ").Append(it.WhDesc).Append("\r\n");
			foreach (St s in it.S) { sb.Append("   ").Append(s.Name).Append("\r\n"); foreach (Ln l in s.L) { if (l.Acct.IndexOf('※') >= 0) qm = true; sb.Append("      ").Append(l.Dr ? "(차) " : "(대) ").Append(l.Acct).Append(l.Amt > 0 ? "  " + l.Amt.ToString("N0") : "").Append("\r\n"); } }
			if (it.Note.Length > 0) sb.Append("   ※ 참고: ").Append(it.Note.Replace("\n", "\r\n      ")).Append("\r\n");
			sb.Append("\r\n");
		}
		// 다중분개 합계: 같은 단계끼리 계정별로 합쳐요 (금액이 있는 건만)
		Dictionary<string, Dictionary<string, long>> agg = new Dictionary<string, Dictionary<string, long>>(); List<string> order = new List<string>(); int cnt = 0;
		foreach (Item it in items) { if (!it.Matched) continue; bool any = false; foreach (St s in it.S) { foreach (Ln l in s.L) if (l.Amt > 0) any = true; } if (any) cnt++; }
		if (cnt >= 2)
		{
			sb.Append("══ 묶어서 한 번에 결의할 때 (다중분개 합계) ══\r\n");
			foreach (Item it in items) foreach (St s in it.S)
			{
				string key = s.Name; bool has = false; foreach (Ln l in s.L) if (l.Amt > 0) has = true; if (!has) continue;
				if (!agg.ContainsKey(key)) { agg[key] = new Dictionary<string, long>(); order.Add(key); }
				foreach (Ln l in s.L) { if (l.Amt <= 0) continue; string k = (l.Dr ? "D|" : "C|") + l.Acct; long o; agg[key].TryGetValue(k, out o); agg[key][k] = o + l.Amt; }
			}
			order.Sort(StringComparer.Ordinal);
			foreach (string key in order)
			{
				sb.Append(key).Append("\r\n"); long dr = 0, cr = 0;
				foreach (string pass in new[] { "D|", "C|" }) foreach (KeyValuePair<string, long> kv in agg[key]) if (kv.Key.StartsWith(pass)) { sb.Append("      ").Append(pass == "D|" ? "(차) " : "(대) ").Append(kv.Key.Substring(2)).Append("  ").Append(kv.Value.ToString("N0")).Append("\r\n"); if (pass == "D|") dr += kv.Value; else cr += kv.Value; }
				sb.Append("      차변 합계 ").Append(dr.ToString("N0")).Append(" / 대변 합계 ").Append(cr.ToString("N0")).Append(dr == cr ? "  ✔ 일치" : "  ✘ 불일치").Append("\r\n");
			}
			sb.Append("\r\n");
		}
		if (qm) sb.Append("※ 표시는 계정명이나 연결이 확실하지 않은 항목이에요. e호조 계정과목 검색에서 확인하세요.\r\n");
		sb.Append("※ 참고용이에요. 실제 분개는 e호조가 세출 과목과 연계해 자동 생성하며, 건설중 자산·예산외 거래 등은 직접 입력해야 해요. 확정은 회계 담당자와 재무회계 운영규정으로 확인하세요.");
		return sb.ToString();
	}
}

internal sealed class JournalForm : Form
{
	private static JournalForm inst;
	public static void Open() { try { if (inst != null && !inst.IsDisposed) { inst.Activate(); return; } inst = new JournalForm(); inst.Show(); } catch (Exception ex) { Program.LogErr(ex); } }
	private readonly TextBox tIn = new TextBox(), tOut = new TextBox(); private readonly Label lSt = new Label();
	public JournalForm()
	{
		float s = Math.Min(1.2f, FUi.Scale(560, 560)); Text = "다중분개 도우미 (e호조 지급결의)"; MaximizeBox = true; StartPosition = FormStartPosition.CenterScreen; ClientSize = new Size((int)(560 * s), (int)(560 * s)); MinimumSize = new Size((int)(420 * s), (int)(420 * s));
		Font = new Font("맑은 고딕", 8.5f * s); BackColor = Color.FromArgb(246, 249, 252);
		Label l1 = new Label { Text = "지출 내용 (한 줄에 한 건 · 금액을 같이 쓰면 계산해요)", Left = (int)(12 * s), Top = (int)(10 * s), Width = (int)(430 * s), Height = (int)(20 * s), Anchor = AnchorStyles.Top | AnchorStyles.Left };
		tIn.Left = (int)(12 * s); tIn.Top = (int)(32 * s); tIn.Width = (int)(536 * s); tIn.Height = (int)(86 * s); tIn.Multiline = true; tIn.ScrollBars = ScrollBars.Vertical; tIn.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
		tIn.Text = "사무용품 구입 350,000원\r\n외부강사 강사료 200,000원\r\n컴퓨터 구입 1,200,000원";
		Button go = new Button { Text = "분개 만들기", Left = (int)(12 * s), Top = (int)(124 * s), Width = (int)(100 * s), Height = (int)(28 * s), FlatStyle = FlatStyle.System };
		Button cp = new Button { Text = "결과 복사", Left = (int)(120 * s), Top = (int)(124 * s), Width = (int)(90 * s), Height = (int)(28 * s), FlatStyle = FlatStyle.System };
		Button ex = new Button { Text = "예시 보기", Left = (int)(218 * s), Top = (int)(124 * s), Width = (int)(70 * s), Height = (int)(28 * s), FlatStyle = FlatStyle.System };
		Button cat = new Button { Text = "분개 사전", Left = (int)(294 * s), Top = (int)(124 * s), Width = (int)(80 * s), Height = (int)(28 * s), FlatStyle = FlatStyle.System };
		lSt.Left = (int)(382 * s); lSt.Top = (int)(130 * s); lSt.Width = (int)(166 * s); lSt.Height = (int)(20 * s); lSt.ForeColor = Color.FromArgb(80, 110, 150);
		tOut.Left = (int)(12 * s); tOut.Top = (int)(160 * s); tOut.Width = (int)(536 * s); tOut.Height = (int)(388 * s); tOut.Multiline = true; tOut.ReadOnly = true; tOut.ScrollBars = ScrollBars.Vertical; tOut.BackColor = Color.White; tOut.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
		Action make = delegate { try { tOut.Text = JournalGen.Run(tIn.Text); lSt.Text = tOut.Text.Length == 0 ? "내용을 입력해 주세요" : "만들었어요"; } catch (Exception e) { Program.LogErr(e); lSt.Text = "오류가 났어요"; } };
		go.Click += delegate { make(); };
		cat.Click += delegate { tOut.Text = JournalGen.Catalog(); tOut.SelectionStart = 0; tOut.SelectionLength = 0; lSt.Text = "분개 사전이에요"; };
		cp.Click += delegate { try { if (tOut.Text.Length > 0) { Clipboard.SetText(tOut.Text); lSt.Text = "복사했어요"; } } catch { } };
		string[] exs = { "사무용품 구입 350,000원\r\n외부강사 강사료 200,000원\r\n컴퓨터 구입 1,200,000원", "도로 포장공사 기성금 25,000,000원\r\n공사 감리비 2,000,000원", "직원 국내 출장비 80,000원\r\n간담회 식대 120,000원\r\n전기요금 540,000원", "일용직 노임 220,000원\r\n(주)OO 청소용역 선금 1,000,000원", "일상경비 전도금 500,000원\r\n전화요금 70,000원" }; int ei = 0;
		ex.Click += delegate { tIn.Text = exs[ei++ % exs.Length]; make(); };
		tIn.KeyDown += delegate(object o, KeyEventArgs e) { if (e.KeyCode == Keys.Enter && e.Control) { e.SuppressKeyPress = true; make(); } };
		Controls.AddRange(new Control[] { l1, tIn, go, cp, ex, cat, lSt, tOut }); make(); Shown += delegate { tIn.SelectionStart = tIn.Text.Length; tIn.SelectionLength = 0; };
	}
}
