// Temporary generator: builds dark-mode overrides from inline <style> blocks.
// Run: node Client/tools/gen-dark-css.mjs
import fs from 'node:fs';
import path from 'node:path';

const root = path.resolve(process.argv[1], '../../../');
const clientDir = path.join(root, 'Client');

function listRazor(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name === 'obj' || entry.name === 'bin') continue;
      listRazor(p, out);
    } else if (entry.name.endsWith('.razor')) {
      out.push(p);
    }
  }
  return out;
}

function listRazorCss(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (entry.name === 'obj' || entry.name === 'bin') continue;
      listRazorCss(p, out);
    } else if (entry.name.endsWith('.razor.css')) {
      out.push(p);
    }
  }
  return out;
}

const adminSet = [
  ...listRazor(path.join(clientDir, 'Pages', 'Admin')),
  'Shared/AdminLayout.razor',
  'Shared/ConfirmDialog.razor',
  'Shared/UserDeleteConfirmDialog.razor',
  'Shared/CustomerSearchModal.razor',
  'Shared/SearchableSelect.razor',
  'Shared/StringTypeahead.razor',
  'Shared/StatusPage.razor',
  'Shared/ToastContainer.razor',
  'Shared/PwaUpdateDialog.razor',
  'Shared/Components/ImageUploader.razor',
  'Shared/Components/FileUploader.razor',
  'Shared/Components/AdminNotesList.razor',
  'Shared/Components/MonitoringDetailsView.razor',
  'Shared/Components/OrderStageStepper.razor',
].map(f => path.isAbsolute(f) ? f : path.join(clientDir, f));

const siteSet = listRazor(path.join(clientDir, 'Pages')).filter(f => !f.includes(path.join('Pages', 'Admin')));

function stripComments(css) {
  return css.replace(/\/\*[\s\S]*?\*\//g, '');
}

function extractStyles(text) {
  const out = [];
  const re = /<style\b[^>]*>([\s\S]*?)<\/style>/gi;
  let m;
  while ((m = re.exec(text))) out.push(m[1].replace(/@@/g, '@'));
  return out;
}

// raw=true: rule body already contains dark-appropriate values (source used
// @media (prefers-color-scheme: dark)), so keep declarations verbatim.
// Split a declaration block on ';' but never inside parentheses or quotes —
// data-URI backgrounds (url("data:image/svg+xml;...")) contain both.
function splitDecls(body) {
  const out = [];
  let cur = '';
  let depth = 0;
  let quote = null;
  for (let i = 0; i < body.length; i++) {
    const ch = body[i];
    if (quote) {
      cur += ch;
      if (ch === '\\') { cur += body[++i] ?? ''; continue; }
      if (ch === quote) quote = null;
      continue;
    }
    if (ch === '"' || ch === "'") { quote = ch; cur += ch; continue; }
    if (ch === '(') depth++;
    else if (ch === ')') depth = Math.max(0, depth - 1);
    if (ch === ';' && depth === 0) { out.push(cur); cur = ''; continue; }
    cur += ch;
  }
  if (cur.trim()) out.push(cur);
  return out;
}

function parseBlock(css, wrapper, rules, raw = false) {
  let i = 0;
  const n = css.length;
  while (i < n) {
    while (i < n && /\s/.test(css[i])) i++;
    if (i >= n) break;
    const selStart = i;
    while (i < n && css[i] !== '{' && css[i] !== ';') i++;
    if (i >= n) break;
    if (css[i] === ';') { i++; continue; }
    const selector = css.slice(selStart, i).trim();
    i++;
    let depth = 1;
    const bodyStart = i;
    while (i < n && depth > 0) {
      if (css[i] === '{') depth++;
      else if (css[i] === '}') depth--;
      i++;
    }
    const body = css.slice(bodyStart, i - 1);
    if (selector.startsWith('@')) {
      const at = selector.toLowerCase();
      if (at.startsWith('@keyframes') || at.startsWith('@-webkit-keyframes') ||
          at.startsWith('@font-face') || at.startsWith('@page') || at.startsWith('@property')) continue;
      if (at.startsWith('@media')) {
        if (/print/.test(at)) continue;
        if (/prefers-color-scheme/.test(at)) {
          // Theme is app-controlled, so OS preference is irrelevant: unwrap the
          // dark branch into plain rules and drop the light branch.
          if (/prefers-color-scheme\s*:\s*dark/.test(at)) parseBlock(body, wrapper, rules, true);
          continue;
        }
      }
      parseBlock(body, wrapper ? wrapper + '\n' + selector : selector, rules, raw);
    } else {
      const decls = splitDecls(body).map(d => d.trim()).filter(Boolean).map(d => {
        const idx = d.indexOf(':');
        return idx < 0 ? null : { prop: d.slice(0, idx).trim().toLowerCase(), value: d.slice(idx + 1).trim() };
      }).filter(Boolean);
      rules.push({ selector: selector.replace(/\s+/g, ' '), wrapper, decls, raw });
    }
  }
}

function loadRules(files) {
  const rules = [];
  for (const file of files) {
    const text = fs.readFileSync(file, 'utf8');
    for (const style of extractStyles(text)) parseBlock(stripComments(style), null, rules);
  }
  return rules;
}

// Blazor isolated CSS (.razor.css) is plain CSS: parse the whole file.
function loadCssRules(files) {
  const rules = [];
  for (const file of files) {
    parseBlock(stripComments(fs.readFileSync(file, 'utf8')), null, rules);
  }
  return rules;
}

// ── color helpers ──────────────────────────────────────────────
const NAMED = { white: '#ffffff', black: '#000000' };
function parseColor(tok) {
  if (!tok) return null;
  tok = tok.trim().toLowerCase();
  if (tok in NAMED) tok = NAMED[tok];
  if (tok === 'transparent' || tok === 'inherit' || tok === 'currentcolor' || tok === 'none') return null;
  let m;
  if ((m = /^#([0-9a-f]{3,4})$/.exec(tok))) {
    const h = m[1];
    return { rgb: [parseInt(h[0] + h[0], 16), parseInt(h[1] + h[1], 16), parseInt(h[2] + h[2], 16)], alpha: 1 };
  }
  if ((m = /^#([0-9a-f]{6})([0-9a-f]{2})?$/.exec(tok))) {
    return {
      rgb: [parseInt(m[1].slice(0, 2), 16), parseInt(m[1].slice(2, 4), 16), parseInt(m[1].slice(4, 6), 16)],
      alpha: m[2] ? parseInt(m[2], 16) / 255 : 1,
    };
  }
  if ((m = /^rgba?\(([^)]+)\)$/.exec(tok))) {
    const parts = m[1].split(',').map(s => parseFloat(s));
    if (parts.length >= 3) return { rgb: [parts[0] | 0, parts[1] | 0, parts[2] | 0], alpha: parts.length > 3 ? parts[3] : 1 };
  }
  return null;
}
function rgbToHsl([r, g, b]) {
  r /= 255; g /= 255; b /= 255;
  const max = Math.max(r, g, b), min = Math.min(r, g, b);
  const l = (max + min) / 2;
  let h = 0, s = 0;
  if (max !== min) {
    const d = max - min;
    s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
    if (max === r) h = ((g - b) / d + (g < b ? 6 : 0)) / 6;
    else if (max === g) h = ((b - r) / d + 2) / 6;
    else h = ((r - g) / d + 4) / 6;
  }
  return [h * 360, s, l];
}
function hslCss(h, s, l) {
  return `hsl(${Math.round((h + 360) % 360)} ${Math.round(s * 100)}% ${Math.round(l * 100)}%)`;
}
function isFlat([r, g, b]) { return Math.max(r, g, b) - Math.min(r, g, b) <= 28; }
function spread([r, g, b]) { return Math.max(r, g, b) - Math.min(r, g, b); }
function withAlpha(color, alpha) {
  if (alpha >= 0.99) return color;
  if (color.startsWith('#')) {
    const v = parseColor(color).rgb;
    return `rgba(${v[0]}, ${v[1]}, ${v[2]}, ${alpha.toFixed(2)})`;
  }
  if (color.startsWith('rgba')) return color.replace(/,\s*[\d.]+\)$/, `, ${alpha.toFixed(2)})`);
  if (color.startsWith('hsl')) return color.replace('hsl(', 'hsla(').replace(')', ` / ${alpha.toFixed(2)})`);
  return color;
}

function mapBackground(tok) {
  const c = parseColor(tok);
  if (!c) return null;
  const [h, s, l] = rgbToHsl(c.rgb);
  // A translucent near-black wash (rgba(15,23,42,.06)) is an overlay on a light
  // surface, not a dark fill: flip it to a translucent light wash. Saturated
  // low-alpha tints (#00c853 at 10%) keep their hue.
  if (c.alpha <= 0.2 && l < 0.2) {
    return `rgba(255, 255, 255, ${Math.min(0.14, Math.max(0.05, c.alpha * 1.4)).toFixed(2)})`;
  }
  if (spread(c.rgb) <= 28) {
    if (l < 0.62) return null;
    if (l > 0.99) return withAlpha('#1e293b', c.alpha);
    if (l > 0.93) return withAlpha('#0f172a', c.alpha);
    // Muted greys (#e2e8f0 chips, #cbd5e1 switch tracks, #d1d5db avatars).
    return withAlpha('#334155', c.alpha);
  }
  if (l > 0.86) return withAlpha(pickTint(h, 0.14), c.alpha);
  return null;
}
function pickTint(h, alpha) {
  if (h < 20 || h >= 340) return `rgba(248, 113, 113, ${alpha})`;
  if (h < 45) return `rgba(251, 146, 60, ${alpha})`;
  if (h < 68) return `rgba(251, 191, 36, ${alpha})`;
  if (h < 165) return `rgba(52, 211, 153, ${alpha})`;
  if (h < 195) return `rgba(34, 211, 238, ${alpha})`;
  if (h < 255) return `rgba(96, 165, 250, ${alpha})`;
  if (h < 300) return `rgba(167, 139, 250, ${alpha})`;
  return `rgba(244, 114, 182, ${alpha})`;
}
function mapText(tok) {
  const c = parseColor(tok);
  if (!c) return null;
  const [h, s, l] = rgbToHsl(c.rgb);
  if (isFlat(c.rgb) || s < 0.5) {
    if (l < 0.24) return withAlpha('#f1f5f9', c.alpha);
    if (l < 0.44) return withAlpha('#cbd5e1', c.alpha);
    if (l < 0.6) return withAlpha('#94a3b8', c.alpha);
    return null;
  }
  // Brand accents as text (#4f46e5, #2563eb, #db2777 …) sit at mid lightness:
  // readable on white, invisible on a dark card. Lift them.
  if (l < 0.8) return withAlpha(hslCss(h, Math.max(0.55, s), 0.72), c.alpha);
  return null;
}
function mapBorder(tok) {
  const c = parseColor(tok);
  if (!c) return null;
  const [h, s, l] = rgbToHsl(c.rgb);
  if (l < 0.3) {
    const a = c.alpha < 0.5 ? Math.min(0.18, Math.max(0.08, c.alpha * 2)) : 0.18;
    return `rgba(255, 255, 255, ${a.toFixed(2)})`;
  }
  // Near-neutral greys (#e2e8f0 has a 14-point channel spread) stay neutral;
  // only genuinely tinted colors become tinted dark borders.
  if (s < 0.15 || isFlat(c.rgb)) {
    if (l > 0.86) return 'rgba(255, 255, 255, 0.10)';
    if (l > 0.55) return 'rgba(255, 255, 255, 0.16)';
    return null;
  }
  if (l > 0.72) return pickTint(h, 0.35);
  return null;
}

function rewriteColors(value, mapper) {
  let changed = false;
  const out = value.replace(/#[0-9a-fA-F]{3,8}\b|\brgba?\([^)]*\)|\bwhite\b/gi, tok => {
    const mapped = mapper(tok);
    if (mapped) { changed = true; return mapped; }
    return tok;
  });
  return changed ? out : null;
}

// Data URIs contain ';', '"', '#' and ')' — rewriting colors inside them would
// corrupt the asset, so only the parts outside url(...) are color-mapped.
function findParenEnd(s, openIdx) {
  let depth = 0;
  let quote = null;
  for (let i = openIdx; i < s.length; i++) {
    const ch = s[i];
    if (quote) {
      if (ch === '\\') { i++; continue; }
      if (ch === quote) quote = null;
      continue;
    }
    if (ch === '"' || ch === "'") { quote = ch; continue; }
    if (ch === '(') depth++;
    else if (ch === ')') { depth--; if (depth === 0) return i + 1; }
  }
  return s.length;
}

function rewriteOutsideUrl(value, mapper) {
  let out = '';
  let changed = false;
  let plainStart = 0;
  let i = 0;
  const flush = end => {
    const seg = value.slice(plainStart, end);
    const r = rewriteColors(seg, mapper);
    if (r) { changed = true; out += r; } else out += seg;
  };
  while (i < value.length) {
    if (/^url\(/i.test(value.slice(i, i + 4))) {
      flush(i);
      const stop = findParenEnd(value, i + 3);
      out += value.slice(i, stop);
      i = stop;
      plainStart = i;
      continue;
    }
    i++;
  }
  flush(value.length);
  return changed ? out : null;
}

// ── design-token (custom property) mapping ────────────────────
// Pages declare their own palettes on :root (e.g. --rl-bg, --wr-surface).
// Those tokens are consumed via var(), so the literal-value mapper never sees
// the real colors; map the token values themselves instead.
const TOKEN_TEXT = /text|color|fg|ink|label|title|muted|foreground/;
const TOKEN_BORDER = /border|line|divider|stroke|outline|separator/;
const TOKEN_BG = /bg|background|surface|card|panel|sheet|box|chip|track|hover|shade|fill|input|field|row|layer/;
const TOKEN_INVERSE = /active|selected|inverse|solid|strong/;

function darkenShadow(raw) {
  const shadow = (_, a) => `rgba(0, 0, 0, ${Math.min(0.7, Math.max(0.25, parseFloat(a) * 4)).toFixed(2)})`;
  const out = raw
    .replace(/rgba?\(\s*15\s*,\s*23\s*,\s*42\s*,\s*([\d.]+)\s*\)/g, shadow)
    .replace(/rgba?\(\s*2\s*,\s*6\s*,\s*23\s*,\s*([\d.]+)\s*\)/g, shadow)
    .replace(/rgba?\(\s*0\s*,\s*0\s*,\s*0\s*,\s*([\d.]+)\s*\)/g, shadow);
  return out !== raw ? out : null;
}

function mapToken(name, raw) {
  const n = name.toLowerCase().replace(/^--/, '');
  if (/shadow/.test(n)) return darkenShadow(raw);
  if (TOKEN_BORDER.test(n) && !TOKEN_BG.test(n)) {
    return mapBorder(raw);
  }
  const c = parseColor(raw);
  if (!c) {
    if (TOKEN_BG.test(n) && /color-mix|gradient/i.test(raw)) return rewriteColors(raw, mapBackground);
    return null;
  }
  const [h, s, l] = rgbToHsl(c.rgb);
  if (TOKEN_BORDER.test(n)) {
    if (l < 0.3 && c.alpha < 0.5) return `rgba(255, 255, 255, ${Math.min(0.2, Math.max(0.09, c.alpha * 2.2)).toFixed(2)})`;
    if (l < 0.6) return null;
    return `rgba(255, 255, 255, ${c.alpha >= 0.99 ? 0.12 : Math.max(0.1, c.alpha).toFixed(2)})`;
  }
  if (TOKEN_TEXT.test(n) && !/bg|background/.test(n)) {
    if (TOKEN_INVERSE.test(n) && l > 0.9) return '#0f172a';
    return mapText(raw);
  }
  if (TOKEN_BG.test(n)) {
    // translucent dark overlay on a light surface (rgba(15,23,42,.03)) becomes a
    // translucent light overlay on the dark surface
    if (c.alpha < 0.35 && l < 0.35) return `rgba(255, 255, 255, ${Math.max(0.06, Math.min(0.12, c.alpha * 1.8)).toFixed(2)})`;
    if (isFlat(c.rgb)) {
      if (l > 0.99) return '#1e293b';
      if (l > 0.93) return '#0f172a';
      if (l > 0.62) return '#334155';
      if (l < 0.35 && TOKEN_INVERSE.test(n)) return '#e2e8f0';
      return null;
    }
    if (TOKEN_INVERSE.test(n) && l < 0.42) return pickTint(h, 0.85);
    if (l > 0.86) return withAlpha(pickTint(h, 0.16), c.alpha);
    return null;
  }
  return null;
}

function splitImportant(value) {
  const important = /!important\s*$/i.test(value);
  return { value: value.replace(/\s*!important\s*$/i, '').trim(), important };
}

function transformDecls(decls, selector = '', tokenMap = new Map()) {
  const res = [];
  let saturatedBg = false;
  for (const d of decls) {
    if (/^(background|background-color)$/.test(d.prop)) {
      const { value } = splitImportant(d.value);
      const c = parseColor(value);
      if (c) {
        const [,, l] = rgbToHsl(c.rgb);
        // Only an opaque fill counts as "text is already light"; a translucent
        // wash (rgba(15,23,42,.06)) must not suppress text lightening.
        if (c.alpha >= 0.35) {
          if (!isFlat(c.rgb) && l > 0.2 && l < 0.86) saturatedBg = true;
          if (isFlat(c.rgb) && l < 0.35) saturatedBg = true; // dark bg => light text already
        }
      }
    }
  }
  const isKnob = /::(after|before)\b/.test(selector);
  for (const d of decls) {
    const p = d.prop;
    const { value: raw, important } = splitImportant(d.value);
    const suffix = important ? ' !important' : '';
    if (p.startsWith('--')) {
      const mapped = mapToken(p, raw);
      if (mapped) res.push({ prop: p, value: mapped + suffix });
      continue;
    }
    if (/^(background|background-color)$/.test(p)) {
      const c = parseColor(raw);
      if (c) {
        // Toggle knobs must stay light on a dark track, not inherit the card tone.
        if (isKnob && isFlat(c.rgb) && rgbToHsl(c.rgb)[2] > 0.9) {
          res.push({ prop: p, value: 'rgba(255, 255, 255, 0.92)' + suffix });
          continue;
        }
        const mapped = mapBackground(raw);
        if (mapped) res.push({ prop: p, value: mapped + suffix });
      } else if (/url\(/i.test(raw)) {
        const out = rewriteOutsideUrl(raw, mapBackground);
        if (out) res.push({ prop: p, value: out + suffix });
      } else if (/gradient|color-mix/i.test(raw)) {
        const out = rewriteColors(raw, mapBackground);
        if (out) res.push({ prop: p, value: out + suffix });
      }
      continue;
    }
    if (p === 'color' || p === 'fill' || p === 'stroke') {
      if (saturatedBg) continue;
      // color: var(--brand-accent) can't be resolved here; if that token stays
      // dark in dark mode the text disappears, so lift it with color-mix.
      const vm = /^var\(\s*(--[\w-]+)\s*(?:,[^)]*)?\)$/.exec(raw);
      if (vm) {
        const tv = tokenMap.get(vm[1]);
        const tc = tv ? parseColor(tv) : null;
        if (tc && rgbToHsl(tc.rgb)[2] < 0.66) {
          res.push({ prop: p, value: `color-mix(in srgb, var(${vm[1]}) 55%, #e2e8f0)` + suffix });
        }
        continue;
      }
      const mapped = mapText(raw);
      if (mapped) res.push({ prop: p, value: mapped + suffix });
      continue;
    }
    if (/^border(-(top|right|bottom|left|color))?$/.test(p)) {
      const out = rewriteColors(raw, mapBorder);
      if (out) res.push({ prop: p, value: out + suffix });
      continue;
    }
    if (p === 'box-shadow') {
      const out = raw.replace(/rgba?\(\s*15\s*,\s*23\s*,\s*42\s*,\s*([\d.]+)\s*\)/g,
        (_, a) => `rgba(0, 0, 0, ${Math.min(0.6, Math.round(parseFloat(a) * 300) / 100).toFixed(2)})`);
      if (out !== raw) res.push({ prop: p, value: out + suffix });
    }
  }
  return res;
}

// Framework class names are already handled by theme.css; page-level styles that
// redefine them are noise, so skip those selectors. Everything else is kept —
// project classes are often single words (.ptable, .wbox, .seg).
const GENERIC_CLASSES = new Set([
  'active', 'alert', 'badge', 'btn', 'card', 'col', 'container', 'disabled',
  'dropdown', 'dropdown-menu', 'form-control', 'form-select', 'input-group',
  'list-group', 'list-group-item', 'modal', 'modal-content', 'nav', 'nav-link',
  'nav-tabs', 'row', 'show', 'small', 'smaller', 'table', 'ltr', 'rtl',
  'dark', 'light',
]);

const DARK_ANCHOR = /(^|[\s>+~,(])\.dark(?![\w-])/;

function keepSelector(sel) {
  const s = sel.trim();
  if (!s) return false;
  if (/^(html|body|\*)/i.test(s)) return false;
  if (/prefers-|reduced-motion/.test(s)) return false;
  // Hand-authored dark blocks (.dark { --token: … }) are gold: keep verbatim.
  if (DARK_ANCHOR.test(s)) return true;
  if (!/\.[a-zA-Z_][\w-]*/.test(s) && !/#[a-zA-Z_][\w-]*/.test(s) && !/:root/.test(s)) return false;
  const classes = [...s.matchAll(/\.([a-zA-Z_][\w-]*)/g)].map(m => m[1]);
  if (classes.length && classes.every(c => GENERIC_CLASSES.has(c))) return false;
  return true;
}

const THEME_PREFIX = 'html:root[data-theme="dark"]';

function generate(rules) {
  // Resolved token values first: var()-based colors need to know how dark the
  // token ends up in dark mode before deciding to lift the text color.
  const tokenMap = new Map();
  for (const r of rules) {
    const authoredDark = DARK_ANCHOR.test(r.selector);
    if (r.raw && !authoredDark) continue;
    for (const d of r.decls) {
      if (!d.prop.startsWith('--')) continue;
      const { value } = splitImportant(d.value);
      tokenMap.set(d.prop, authoredDark ? value : (mapToken(d.prop, value) || value));
    }
  }
  const out = [];
  const seen = new Map();
  for (const r of rules) {
    const kept = r.selector.split(',')
      .map(s => s.replace(/::deep\s*/g, '').trim())
      .filter(s => keepSelector(s));
    if (!kept.length) continue;
    const authoredDark = DARK_ANCHOR.test(r.selector);
    const decls = r.raw || authoredDark ? r.decls : transformDecls(r.decls, r.selector, tokenMap);
    if (!decls.length) continue;
    const selector = kept.map(s => {
      // `.dark` is only a marker for the author's own dark palette: drop it so
      // the rule lands at html[data-theme="dark"] and wins over the light rule.
      const stripped = s.replace(new RegExp(DARK_ANCHOR.source, 'g'), '$1').trim();
      const base = stripped.replace(/:root\b/g, '').trim();
      // :root keeps one extra class-level of specificity so the layer also wins
      // over Blazor's scoped (.razor.css → .a[b-x] .b[b-y]) descendant rules.
      return base ? `${THEME_PREFIX} ${base}` : THEME_PREFIX;
    }).join(',\n');
    const key = (r.wrapper || '') + '|' + selector;
    if (seen.has(key)) {
      // Merge instead of replace: the same selector can be declared by several
      // blocks (e.g. two :root blocks in one page); later props must win per prop.
      const target = out[seen.get(key)];
      for (const d of decls) {
        const i = target.decls.findIndex(x => x.prop === d.prop);
        if (i >= 0) target.decls[i] = d;
        else target.decls.push(d);
      }
    }
    else { seen.set(key, out.length); out.push({ selector, wrapper: r.wrapper, decls, raw: r.raw || authoredDark }); }
  }
  return out;
}

// Report anything the mappers failed to lift: dark text and light fills that
// would still render badly on a dark surface. Raw/author-dark rules are exempt.
function lintRules(rules, label) {
  const bad = [];
  for (const r of rules) {
    if (r.raw) continue;
    for (const d of r.decls) {
      const c = parseColor(d.value);
      if (!c || c.alpha < 0.5) continue;
      const [h, s, l] = rgbToHsl(c.rgb);
      if (/^(color|fill|stroke)$/.test(d.prop) && l < 0.42) {
        bad.push(`${r.selector.split('\n')[0]}  ${d.prop}: ${d.value}`);
      } else if (/^(background|background-color)$/.test(d.prop) && l > 0.62 && spread(c.rgb) > 4) {
        bad.push(`${r.selector.split('\n')[0]}  ${d.prop}: ${d.value}`);
      }
    }
  }
  if (bad.length) {
    console.log(`\n[lint ${label}] ${bad.length} suspect declaration(s):`);
    for (const b of bad.slice(0, 40)) console.log('  ' + b);
    if (bad.length > 40) console.log(`  … +${bad.length - 40} more`);
  } else {
    console.log(`[lint ${label}] clean`);
  }
  return bad.length;
}

// A single malformed string/brace makes the browser drop every following rule,
// which silently kills half the dark layer — fail the build instead.
function validateCss(css, label) {
  let depth = 0, paren = 0, quote = null;
  for (let i = 0; i < css.length; i++) {
    const ch = css[i];
    if (quote) {
      if (ch === '\\') { i++; continue; }
      if (ch === quote) quote = null;
      continue;
    }
    if (ch === '/' && css[i + 1] === '*') {
      const end = css.indexOf('*/', i + 2);
      i = end < 0 ? css.length : end + 1;
      continue;
    }
    if (ch === '"' || ch === "'") { quote = ch; continue; }
    if (ch === '{') depth++;
    else if (ch === '}') { depth--; if (depth < 0) throw new Error(`${label}: unmatched '}' at ${i}`); }
    else if (ch === '(') paren++;
    else if (ch === ')') { paren--; if (paren < 0) throw new Error(`${label}: unmatched ')' at ${i}`); }
  }
  if (quote) throw new Error(`${label}: unterminated ${quote} string`);
  if (depth !== 0) throw new Error(`${label}: unbalanced braces (${depth})`);
  if (paren !== 0) throw new Error(`${label}: unbalanced parentheses (${paren})`);
}

function render(rules) {
  return rules.map(r => {
    const lines = [`${r.selector} {`, ...r.decls.map(d => `    ${d.prop}: ${d.value};`), '}'];
    if (!r.wrapper) return lines.join('\n');
    return `${r.wrapper} {\n${lines.map(l => '    ' + l).join('\n')}\n}`;
  }).join('\n\n');
}

const banner = (name) => `/* ==========================================================================
   ErrorService — ${name}
   Generated by Client/tools/gen-dark-css.mjs — regenerate instead of editing.
   All rules are scoped to html[data-theme="dark"].
   ========================================================================== */

`;
const adminRules = loadRules(adminSet);
// Shared components render in both layouts; dark-site.css is loaded on every
// page in dark mode, so their inline styles belong to the public layer.
// AdminLayout is excluded because dark-admin.css already owns the admin shell.
const sharedRazor = listRazor(path.join(clientDir, 'Shared'))
  .filter(f => !f.endsWith('AdminLayout.razor'));
const siteRules = loadRules([...siteSet, ...sharedRazor]);
const adminScoped = listRazorCss(path.join(clientDir, 'Pages', 'Admin'));
const publicScoped = listRazorCss(path.join(clientDir, 'Pages')).filter(f => !f.includes(path.join('Pages', 'Admin')));

// Shared components render in both layouts; their isolated CSS is part of the
// public layer because dark-site.css is loaded on every page in dark mode.
const sharedScoped = listRazorCss(path.join(clientDir, 'Shared'));

const adminGen = generate([...adminRules, ...loadCssRules(adminScoped)]);
const siteGen = generate([...siteRules, ...loadCssRules([...publicScoped, ...sharedScoped])]);
const adminCss = banner('dark layer — admin pages') + render(adminGen);
const siteCss = banner('dark layer — public pages') + render(siteGen);

lintRules(adminGen, 'admin');
lintRules(siteGen, 'site');

validateCss(adminCss, 'dark-admin.css');
validateCss(siteCss, 'dark-site.css');

fs.writeFileSync(path.join(clientDir, 'wwwroot', 'css', 'dark-admin.css'), adminCss, 'utf8');
fs.writeFileSync(path.join(clientDir, 'wwwroot', 'css', 'dark-site.css'), siteCss, 'utf8');
console.log('admin:', adminSet.length + adminScoped.length, 'files,', adminCss.length, 'bytes');
console.log('site :', siteSet.length + sharedRazor.length + publicScoped.length + sharedScoped.length, 'files,', siteCss.length, 'bytes');
