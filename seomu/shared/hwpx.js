/* ============================================================
   HWPX(한글) 서류 만들기
   한글이 만든 빈 문서 골격(shared/skeleton.hwpx, python-hwpx 의 Skeleton.hwpx · Apache-2.0)을 바탕으로
   header.xml 에 테두리·글자·문단 모양만 덧붙이고, 본문(section0.xml)을 표와 글자로 채운다.
   쓰는 법:  SEOMU_HWPX.build(pages, JSZip, skeletonBase64, 제목) → Promise<Uint8Array>
     pages: [[블록,…], …]  (쪽마다 블록 목록)
     블록:  {k:'p', text:'글', al:'CENTER', cs:CS.BOLD}
            {k:'tbl', al:'CENTER', widths:[mm,…], rows:[{h:mm, cells:[{lines:[{t:'글', cs:0}], al:'CENTER', bf:BF.THIN}]}]}
   ============================================================ */
var SEOMU_HWPX = (function () {
  'use strict';
  /* 골격에 이미 있는 것: 테두리 1(없음)·2, 글자 0~6, 문단 0~19, 글꼴 0=함초롬돋움 1=함초롬바탕. 여기서 덧붙인 번호는 아래. */
  var CS = { NORMAL: 0, BOLD: 7, TITLE: 8, BODY12: 9, BIG: 10, BOLD13: 11, SMALL: 12 };
  var PA = { LEFT: 20, CENTER: 21, RIGHT: 22 };
  var BF = { NONE: 1, THIN: 3, SHADE: 4, THICK: 5, DIAG: 6 };
  var MM = 283.465, PAGE_W = 59528, MARGIN = 4252;
  var hu = function (mm) { return Math.round(mm * MM); };
  var esc = function (s) { return String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;'); };

  function bfill(id, o) {
    var t = o.line ? 'SOLID' : 'NONE', w = o.w || '0.12 mm';
    var b = function (n) { return '<hh:' + n + ' type="' + t + '" width="' + (o.line ? w : '0.1 mm') + '" color="#000000"/>'; };
    return '<hh:borderFill id="' + id + '" threeD="0" shadow="0" centerLine="NONE" breakCellSeparateLine="0">'
      + '<hh:slash type="' + (o.slash ? 'CENTER' : 'NONE') + '" Crooked="0" isCounter="0"/><hh:backSlash type="NONE" Crooked="0" isCounter="0"/>'
      + b('leftBorder') + b('rightBorder') + b('topBorder') + b('bottomBorder') + '<hh:diagonal type="' + (o.slash ? 'SOLID' : 'SOLID') + '" width="0.12 mm" color="#000000"/>'
      + '<hc:fillBrush><hc:winBrush faceColor="' + (o.fill || 'none') + '" hatchColor="#999999" alpha="0"/></hc:fillBrush></hh:borderFill>';
  }
  function charPr(id, height, face, bold) {
    var all = function (tag, v) { return '<hh:' + tag + ' hangul="' + v + '" latin="' + v + '" hanja="' + v + '" japanese="' + v + '" other="' + v + '" symbol="' + v + '" user="' + v + '"/>'; };
    return '<hh:charPr id="' + id + '" height="' + height + '" textColor="#000000" shadeColor="none" useFontSpace="0" useKerning="0" symMark="NONE" borderFillIDRef="2">'
      + all('fontRef', face) + all('ratio', 100) + all('spacing', 0) + all('relSz', 100) + all('offset', 0)
      + (bold ? '<hh:bold/>' : '') + '<hh:underline type="NONE" shape="SOLID" color="#000000"/><hh:strikeout shape="NONE" color="#000000"/><hh:outline type="NONE"/><hh:shadow type="NONE" color="#C0C0C0" offsetX="10" offsetY="10"/></hh:charPr>';
  }
  function patchHeader(x) {
    var addTo = function (tag, items, count) {
      var re = new RegExp('(<hh:' + tag + ' itemCnt=")(\\d+)(">[\\s\\S]*?)(</hh:' + tag + '>)');
      if (!re.test(x)) throw new Error('header.xml 에서 ' + tag + ' 를 찾지 못했습니다');
      x = x.replace(re, function (m, a, n, body, end) { return a + (+n + count) + body.slice(0) + items + end; });
    };
    var bfs = bfill(3, { line: 1 }) + bfill(4, { line: 1, fill: '#E8EEF6' }) + bfill(5, { line: 1, w: '0.4 mm' }) + bfill(6, { line: 1, slash: 1 });
    addTo('borderFills', bfs, 4);
    // 글자: 글꼴 0=돋움 계열, 1=바탕 계열
    var cps = charPr(7, 1000, 0, 1) + charPr(8, 1700, 0, 1) + charPr(9, 1200, 1, 0) + charPr(10, 2600, 0, 1) + charPr(11, 1300, 0, 1) + charPr(12, 950, 0, 1);
    addTo('charProperties', cps, 6);
    // 문단: 골격의 0번(양쪽 정렬)을 본떠 왼쪽/가운데/오른쪽 + 줄 간격 130%
    var m = /<hh:paraPr id="0"[\s\S]*?<\/hh:paraPr>/.exec(x);
    if (!m) throw new Error('header.xml 에서 문단 모양을 찾지 못했습니다');
    var mk = function (id, al) { return m[0].replace('id="0"', 'id="' + id + '"').replace('horizontal="JUSTIFY"', 'horizontal="' + al + '"').replace(/<hh:lineSpacing type="PERCENT" value="\d+"/g, '<hh:lineSpacing type="PERCENT" value="130"'); };
    addTo('paraProperties', mk(20, 'LEFT') + mk(21, 'CENTER') + mk(22, 'RIGHT'), 3);
    return x;
  }

  var uid = 2000000;
  function run(cs, text) { return '<hp:run charPrIDRef="' + cs + '"><hp:t>' + esc(text) + '</hp:t></hp:run>'; }
  function para(inner, pa, pb) { return '<hp:p id="' + (uid++) + '" paraPrIDRef="' + pa + '" styleIDRef="0" pageBreak="' + (pb ? 1 : 0) + '" columnBreak="0" merged="0">' + inner + '</hp:p>'; }
  function cellXml(c, col, row, w, h) {
    var lines = (c.lines && c.lines.length ? c.lines : [{ t: '', cs: 0 }]), pa = PA[c.al || 'CENTER'];
    var ps = lines.map(function (l) { return para(run(l.cs || 0, l.t), pa, 0); }).join('');
    return '<hp:tc name="" header="0" hasMargin="0" protect="0" editable="0" dirty="0" borderFillIDRef="' + (c.bf || BF.THIN) + '">'
      + '<hp:subList id="" textDirection="HORIZONTAL" lineWrap="BREAK" vertAlign="CENTER" linkListIDRef="0" linkListNextIDRef="0" textWidth="0" textHeight="0" hasTextRef="0" hasNumRef="0">' + ps + '</hp:subList>'
      + '<hp:cellAddr colAddr="' + col + '" rowAddr="' + row + '"/><hp:cellSpan colSpan="1" rowSpan="1"/><hp:cellSz width="' + w + '" height="' + h + '"/><hp:cellMargin left="141" right="141" top="141" bottom="141"/></hp:tc>';
  }
  function table(b, pb) {
    var ws = b.widths.map(hu), total = ws.reduce(function (a, x) { return a + x; }, 0), hs = b.rows.map(function (r) { return hu(r.h); }), th = hs.reduce(function (a, x) { return a + x; }, 0);
    var trs = b.rows.map(function (r, ri) { return '<hp:tr>' + r.cells.map(function (c, ci) { return cellXml(c, ci, ri, ws[ci], hs[ri]); }).join('') + '</hp:tr>'; }).join('');
    var tbl = '<hp:tbl id="' + (uid++) + '" zOrder="0" numberingType="TABLE" textWrap="TOP_AND_BOTTOM" textFlow="BOTH_SIDES" lock="0" dropcapstyle="None" pageBreak="CELL" repeatHeader="0" rowCnt="' + b.rows.length + '" colCnt="' + ws.length + '" cellSpacing="0" borderFillIDRef="' + (b.bf || BF.THIN) + '" noAdjust="0">'
      + '<hp:sz width="' + total + '" widthRelTo="ABSOLUTE" height="' + th + '" heightRelTo="ABSOLUTE" protect="0"/>'
      + '<hp:pos treatAsChar="1" affectLSpacing="0" flowWithText="1" allowOverlap="0" holdAnchorAndSO="0" vertRelTo="PARA" horzRelTo="PARA" vertAlign="TOP" horzAlign="LEFT" vertOffset="0" horzOffset="0"/>'
      + '<hp:outMargin left="0" right="0" top="0" bottom="0"/><hp:inMargin left="141" right="141" top="141" bottom="141"/>' + trs + '</hp:tbl>';
    return para('<hp:run charPrIDRef="0">' + tbl + '<hp:t/></hp:run>', PA[b.al || 'LEFT'], pb);
  }
  /* 골격 본문 첫 문단의 쪽 설정(secPr)+단 설정(colPr)을 그대로 가져와 여백만 바꾼다 */
  function sectionXml(skelSection, pages) {
    var open = skelSection.slice(0, skelSection.indexOf('<hp:p '));
    var m = /<hp:run charPrIDRef="0">(<hp:secPr[\s\S]*?<\/hp:secPr>)(<hp:ctrl>[\s\S]*?<\/hp:ctrl>)<\/hp:run>/.exec(skelSection);
    if (!m) throw new Error('골격 본문에서 쪽 설정을 찾지 못했습니다');
    var sec = m[1].replace(/<hp:margin [^>]*\/>/, '<hp:margin header="4252" footer="4252" gutter="0" left="' + MARGIN + '" right="' + MARGIN + '" top="' + MARGIN + '" bottom="' + MARGIN + '"/>');
    var secRun = '<hp:run charPrIDRef="0">' + sec + m[2] + '</hp:run>';
    var body = '', first = true;
    pages.forEach(function (blocks, pi) {
      blocks.forEach(function (b, bi) {
        var pb = pi > 0 && bi === 0, x = b.k === 'tbl' ? table(b, pb) : para(run(b.cs || 0, b.text || ''), PA[b.al || 'LEFT'], pb);
        if (first) { x = x.replace(/(<hp:p [^>]*>)/, '$1' + secRun); first = false; }
        body += x;
      });
    });
    return open + body + '</hs:sec>';
  }
  async function build(pages, JSZipRef, skeletonB64, title) {
    uid = 2000000;
    var z = await JSZipRef.loadAsync(skeletonB64, { base64: true });
    var hdr = await z.file('Contents/header.xml').async('string');
    z.file('Contents/header.xml', patchHeader(hdr));
    var sec = await z.file('Contents/section0.xml').async('string');
    z.file('Contents/section0.xml', sectionXml(sec, pages));
    var hpf = await z.file('Contents/content.hpf').async('string');
    hpf = hpf.replace(/<opf:title\s*\/>|<opf:title>[\s\S]*?<\/opf:title>/, '<opf:title>' + esc(title || '') + '</opf:title>').replace(/synthetic-fixture-author/g, '');
    z.file('Contents/content.hpf', hpf);
    if (z.file('Preview/PrvText.txt')) z.file('Preview/PrvText.txt', title || '');
    var out = new JSZipRef();          // mimetype 은 맨 앞에 압축 없이 넣어야 한다
    out.file('mimetype', await z.file('mimetype').async('string'), { compression: 'STORE' });
    var names = Object.keys(z.files).filter(function (n) { return n !== 'mimetype' && !z.files[n].dir; });
    for (var i = 0; i < names.length; i++) out.file(names[i], await z.file(names[i]).async('uint8array'));
    return out.generateAsync({ type: 'uint8array', mimeType: 'application/hwp+zip', compression: 'DEFLATE' });
  }
  return { build: build, CS: CS, PA: PA, BF: BF };
})();
