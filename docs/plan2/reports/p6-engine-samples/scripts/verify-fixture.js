// Independent re-transcription of rows 1-43 of the merchant's sample and comparison with
// tests/fixtures/sample-rows-1-43.expected.json. Read-only on both. usage: node verify-fixture.js <unzippedSampleDir> <fixture.json>
const fs = require('fs');
const path = require('path');
const [dir, fixturePath] = process.argv.slice(2);
const read = p => fs.readFileSync(path.join(dir, p), 'utf8');
const fixture = JSON.parse(fs.readFileSync(fixturePath, 'utf8'));

const attr = (tag, name) => { const m = tag.match(new RegExp('\\s' + name + '="([^"]*)"')); return m ? m[1] : undefined; };
const unesc = s => s.replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&quot;/g, '"').replace(/&apos;/g, "'").replace(/&amp;/g, '&');

// shared strings
const sst = [...read('xl/sharedStrings.xml').matchAll(/<si>([\s\S]*?)<\/si>/g)].map(m => [...m[1].matchAll(/<t[^>]*>([\s\S]*?)<\/t>/g)].map(t => unesc(t[1])).join(''));

// styles
const styles = read('xl/styles.xml');
const section = name => (styles.match(new RegExp('<' + name + '[^>]*>([\\s\\S]*?)</' + name + '>')) || [, ''])[1];
const fonts = [...section('fonts').matchAll(/<font>([\s\S]*?)<\/font>/g)].map(m => {
  const f = m[1];
  const color = (f.match(/<color[^>]*\/>/) || [''])[0];
  let rgb;
  if (attr(color, 'rgb')) rgb = attr(color, 'rgb');
  else if (attr(color, 'theme') === '1') rgb = 'FF000000';
  else if (attr(color, 'theme') === '2' && attr(color, 'tint') && Math.abs(+attr(color, 'tint') + 0.5) < 0.001) rgb = 'FF767171';
  else rgb = 'UNKNOWN:' + color;
  return { name: attr(f.match(/<name[^>]*\/>/)[0], 'val'), size: +attr(f.match(/<sz[^>]*\/>/)[0], 'val'), color: rgb };
});
const fills = [...section('fills').matchAll(/<fill>([\s\S]*?)<\/fill>/g)].map(m => {
  const p = m[1];
  if (/patternType="solid"/.test(p)) return attr(p.match(/<fgColor[^>]*\/>/)[0], 'rgb');
  return null;
});
const borders = [...section('borders').matchAll(/<border>([\s\S]*?)<\/border>/g)].map(m => {
  const b = m[1];
  const side = n => { const t = b.match(new RegExp('<' + n + '(\\s[^>]*)?/?>')); return t && attr(t[0], 'style') ? attr(t[0], 'style') : 'none'; };
  return { left: side('left'), right: side('right'), top: side('top'), bottom: side('bottom') };
});
const xfs = [...section('cellXfs').matchAll(/<xf\s[^>]*?(?:\/>|>([\s\S]*?)<\/xf>)/g)].map(m => {
  const tag = m[0];
  const align = (tag.match(/<alignment[^>]*\/>/) || [''])[0];
  return {
    font: fonts[+attr(tag, 'fontId')], fill: fills[+attr(tag, 'fillId')], border: borders[+attr(tag, 'borderId')], numFmtId: +attr(tag, 'numFmtId'),
    align: { h: attr(align, 'horizontal') || 'general', v: attr(align, 'vertical') || 'bottom', shrinkToFit: attr(align, 'shrinkToFit') === '1' }
  };
});

// sheet
const sheet = read('xl/worksheets/sheet1.xml');
const colIndex = l => [...l].reduce((n, c) => n * 26 + c.charCodeAt(0) - 64, 0);
const colLetter = n => { let s = ''; while (n > 0) { const r = (n - 1) % 26; s = String.fromCharCode(65 + r) + s; n = Math.floor((n - 1) / 26); } return s; };
const shared = {};
const cells = [];
for (const rm of sheet.matchAll(/<row\s[^>]*?(?:\/>|>([\s\S]*?)<\/row>)/g)) {
  const r = +attr(rm[0], 'r');
  if (r > 43 || !rm[1]) continue;
  for (const cm of rm[1].matchAll(/<c\s[^>]*?(?:\/>|>([\s\S]*?)<\/c>)/g)) {
    const tag = cm[0].match(/<c\s[^>]*?\/?>/)[0];
    const ref = attr(tag, 'r'), s = +(attr(tag, 's') || 0), t = attr(tag, 't');
    const inner = cm[1] || '';
    const fTag = inner.match(/<f[^>]*?(?:\/>|>([\s\S]*?)<\/f>)/);
    let formula;
    if (fTag) {
      const ft = fTag[0].match(/<f[^>]*?\/?>/)[0];
      if (attr(ft, 't') === 'shared') {
        const si = attr(ft, 'si');
        if (fTag[1]) { shared[si] = { text: unesc(fTag[1]), ref }; formula = unesc(fTag[1]); }
        else {
          const master = shared[si];
          const [, mc, mr] = master.ref.match(/([A-Z]+)(\d+)/);
          const [, cc, cr] = ref.match(/([A-Z]+)(\d+)/);
          const dr = +cr - +mr, dc = colIndex(cc) - colIndex(mc);
          formula = master.text.replace(/(\$?)([A-Z]+)(\$?)(\d+)/g, (_, a, c, b, n) => a + (a ? c : colLetter(colIndex(c) + dc)) + b + (b ? n : (+n + dr)));
        }
      } else formula = unesc(fTag[1] || '');
    }
    const v = (inner.match(/<v>([\s\S]*?)<\/v>/) || [])[1];
    let type = 'blank', value;
    if (v !== undefined) {
      if (t === 's') { type = 's'; value = sst[+v]; }
      else if (t === 'str') { type = 's'; value = unesc(v); }
      else { type = 'n'; value = +(+v).toPrecision(15); }
    }
    cells.push({ ref, type, value, formula, style: xfs[s] });
  }
}

const problems = [];
const want = new Map(fixture.cells.map(c => [c.ref, c]));
const got = new Map(cells.map(c => [c.ref, c]));
for (const ref of new Set([...want.keys(), ...got.keys()])) {
  const a = got.get(ref), e = want.get(ref);
  if (!a) { problems.push(`${ref}: in fixture, not in sample`); continue; }
  if (!e) { problems.push(`${ref}: in sample, not in fixture`); continue; }
  if (a.type !== e.type) problems.push(`${ref}: type ${a.type} vs ${e.type}`);
  if (a.type !== 'blank' && a.value !== e.value) problems.push(`${ref}: value ${a.value} vs ${e.value}`);
  const isEFooter = /^E/.test(ref) && e.formula === undefined;
  if ((a.formula || null) !== (e.formula || null)) problems.push(`${ref}: formula ${a.formula} vs ${e.formula}`);
  const sa = JSON.stringify(a.style), se = JSON.stringify(e.style);
  if (sa !== se) problems.push(`${ref}: style\n   sample  ${sa}\n   fixture ${se}`);
}

// merges in rows 1-43
const merges = [...sheet.matchAll(/<mergeCell ref="([^"]+)"\/>/g)].map(m => m[1]).filter(r => +r.match(/\d+/g).pop() <= 43).sort();
if (JSON.stringify(merges) !== JSON.stringify([...fixture.merges].sort())) problems.push('merges differ: ' + merges.join(','));

// cols
const cols = [...sheet.matchAll(/<col\s[^>]*\/>/g)].map(m => ({ min: +attr(m[0], 'min'), max: +attr(m[0], 'max'), width: +attr(m[0], 'width'), customWidth: attr(m[0], 'customWidth') === '1', style: xfs[+attr(m[0], 'style')] }));
if (JSON.stringify(cols) !== JSON.stringify(fixture.columns)) problems.push('columns differ:\n' + JSON.stringify(cols) + '\n' + JSON.stringify(fixture.columns));

// view, format, page
const view = sheet.match(/<sheetView\s[^>]*>/)[0];
const sv = { rightToLeft: attr(view, 'rightToLeft') === '1', tabSelected: attr(view, 'tabSelected') === '1', zoomScale: +attr(view, 'zoomScale'), zoomScaleNormal: +attr(view, 'zoomScaleNormal') };
if (JSON.stringify(sv) !== JSON.stringify(fixture.sheetView)) problems.push('sheetView ' + JSON.stringify(sv));
const fmt = sheet.match(/<sheetFormatPr\s[^>]*>/)[0];
const sf = { defaultColWidth: +attr(fmt, 'defaultColWidth'), defaultRowHeight: +attr(fmt, 'defaultRowHeight') };
if (JSON.stringify(sf) !== JSON.stringify(fixture.sheetFormat)) problems.push('sheetFormat ' + JSON.stringify(sf));
const ps = sheet.match(/<pageSetup\s[^>]*>/)[0];
const pageSetup = { paperSize: +attr(ps, 'paperSize'), orientation: attr(ps, 'orientation'), fitToPage: /fitToPage="1"/.test(sheet), fitToWidth: +(attr(ps, 'fitToWidth') || 1), fitToHeight: +(attr(ps, 'fitToHeight') ?? 1), scale: +attr(ps, 'scale') };
if (JSON.stringify(pageSetup) !== JSON.stringify(fixture.pageSetup)) problems.push('pageSetup ' + JSON.stringify(pageSetup));
const pm = sheet.match(/<pageMargins\s[^>]*>/)[0];
const margins = Object.fromEntries(['left', 'right', 'top', 'bottom', 'header', 'footer'].map(k => [k, +attr(pm, k)]));
if (JSON.stringify(margins) !== JSON.stringify(fixture.pageMargins)) problems.push('pageMargins ' + JSON.stringify(margins));

// merge outer borders: an edge counts when the cell or the facing neighbour has a border
const styleAt = new Map(cells.map(c => [c.ref, c.style]));
const sideOf = (col, row, side) => { const s = styleAt.get(colLetter(col) + row); return s ? s.border[side] : 'none'; };
for (const m of merges) {
  const [a, b] = m.split(':');
  const [, c1, r1] = a.match(/([A-Z]+)(\d+)/), [, c2, r2] = b.match(/([A-Z]+)(\d+)/);
  const x1 = colIndex(c1), x2 = colIndex(c2), y1 = +r1, y2 = +r2;
  const edge = (cellsOnEdge, own, facing) => cellsOnEdge.every(([x, y, nx, ny]) => sideOf(x, y, own) === 'thin' || sideOf(nx, ny, facing) === 'thin') ? 'thin' : 'none';
  const range = (f, t) => Array.from({ length: t - f + 1 }, (_, i) => f + i);
  const outer = {
    left: edge(range(y1, y2).map(y => [x1, y, x1 - 1, y]), 'left', 'right'),
    right: edge(range(y1, y2).map(y => [x2, y, x2 + 1, y]), 'right', 'left'),
    top: edge(range(x1, x2).map(x => [x, y1, x, y1 - 1]), 'top', 'bottom'),
    bottom: edge(range(x1, x2).map(x => [x, y2, x, y2 + 1]), 'bottom', 'top')
  };
  if (JSON.stringify(outer) !== JSON.stringify(fixture.mergeOuterBorders[m])) problems.push(`merge ${m} outer ${JSON.stringify(outer)} vs ${JSON.stringify(fixture.mergeOuterBorders[m])}`);
}

console.log(`sample cells rows 1-43: ${cells.length}; fixture cells: ${fixture.cells.length}; merges ${merges.length}`);
console.log(problems.length ? problems.join('\n') : 'NO DIFFERENCES');
