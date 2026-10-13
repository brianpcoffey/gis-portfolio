// jsmetrics — per-function cyclomatic + cognitive complexity for browser JS, using the acorn parser that
// ships inside Node (no npm install). Run with:
//   node --expose-internals tools/metrics/jsmetrics.js [repoRoot=.] [out.csv=js-metrics.csv]
// (from the repo root; then: py -I tools/metrics/summarize_js.py js-metrics.csv)
// Scope: <repoRoot>/Portfolio.Web/wwwroot/js/**/*.js, excluding any lib/ directory and *.min.js.
//
// Granularity (ESLint `complexity` convention): every FunctionDeclaration / FunctionExpression /
// ArrowFunctionExpression / class method is its OWN unit; nested functions are measured separately and do
// NOT add to the enclosing function. Code outside any function is the unit "<top-level>".
//
// Cyclomatic: 1 + if, ?:, for, for-in, for-of, while, do-while, case (with test), catch, &&, ||, ??,
//             &&=, ||=, ??=, optional chaining link (?.)
// Cognitive (SonarSource rules, per function, nesting starts at 0 in each function):
//   +1+nesting : if (not else-if), ?:, switch, for, for-in, for-of, while, do-while, catch
//   +1         : else-if, else, labeled break/continue, each new sequence of like logical operators (&&, ||, ??)
//   nesting+1  : bodies of the above
'use strict';
const fs = require('fs');
const path = require('path');
const acorn = require('internal/deps/acorn/acorn/dist/acorn');

const root = path.resolve(process.argv[2] || '.');
const outPath = path.resolve(process.argv[3] || 'js-metrics.csv');
const jsRoot = path.join(root, 'Portfolio.Web', 'wwwroot', 'js');

function listJs(dir) {
  const out = [];
  for (const ent of fs.readdirSync(dir, { withFileTypes: true })) {
    const p = path.join(dir, ent.name);
    if (ent.isDirectory()) { if (ent.name !== 'lib') out.push(...listJs(p)); }
    else if (ent.name.endsWith('.js') && !ent.name.endsWith('.min.js')) out.push(p);
  }
  return out;
}

const isFn = n => n && (n.type === 'FunctionDeclaration' || n.type === 'FunctionExpression' || n.type === 'ArrowFunctionExpression');

function children(node) {
  const out = [];
  for (const key of Object.keys(node)) {
    if (key === 'parent' || key === 'loc' || key === 'start' || key === 'end' || key === 'range') continue;
    const v = node[key];
    if (Array.isArray(v)) { for (const c of v) if (c && typeof c.type === 'string') out.push([key, c]); }
    else if (v && typeof v.type === 'string') out.push([key, v]);
  }
  return out;
}

function setParents(node, parent) {
  node.parent = parent;
  for (const [, c] of children(node)) setParents(c, node);
}

function memberName(n) {
  if (!n) return '?';
  if (n.type === 'Identifier') return n.name;
  if (n.type === 'ThisExpression') return 'this';
  if (n.type === 'Literal') return String(n.value);
  if (n.type === 'MemberExpression') return memberName(n.object) + '.' + (n.computed ? '[' + memberName(n.property) + ']' : memberName(n.property));
  if (n.type === 'PrivateIdentifier') return '#' + n.name;
  return '?';
}

function fnName(fn) {
  if (fn.id && fn.id.name) return fn.id.name;
  const p = fn.parent;
  if (!p) return '<anon>';
  if (p.type === 'VariableDeclarator') return memberName(p.id);
  if (p.type === 'AssignmentExpression') return memberName(p.left);
  if (p.type === 'Property' || p.type === 'MethodDefinition' || p.type === 'PropertyDefinition') return memberName(p.key);
  if (p.type === 'CallExpression' || p.type === 'NewExpression') {
    const callee = memberName(p.callee);
    const idx = p.arguments.indexOf(fn);
    if (p.callee === fn) return '<IIFE>';
    return `<${callee} arg${idx}>`;
  }
  return '<anon:' + p.type + '>';
}

function measure(unitNode, isTopLevel) {
  // Walk the unit's own body; do not descend into nested functions.
  let cyc = 1, cog = 0;
  const ignoreLogical = new Set();
  function walk(node, nesting) {
    if (!node) return;
    if (node !== unitNode && isFn(node)) return; // nested function: separate unit
    switch (node.type) {
      case 'IfStatement': {
        const isElseIf = node.parent && node.parent.type === 'IfStatement' && node.parent.alternate === node;
        cyc++;
        cog += isElseIf ? 1 : 1 + nesting;
        walk(node.test, nesting);
        walk(node.consequent, nesting + 1);
        if (node.alternate) {
          if (node.alternate.type === 'IfStatement') walk(node.alternate, nesting); // else-if at chain level
          else { cog += 1; walk(node.alternate, nesting + 1); }
        }
        return;
      }
      case 'ConditionalExpression':
        cyc++; cog += 1 + nesting;
        for (const [, c] of children(node)) walk(c, nesting + 1);
        return;
      case 'SwitchStatement':
        cog += 1 + nesting;
        for (const [, c] of children(node)) walk(c, nesting + 1);
        return;
      case 'SwitchCase':
        if (node.test) cyc++;
        for (const [, c] of children(node)) walk(c, nesting);
        return;
      case 'ForStatement': case 'ForInStatement': case 'ForOfStatement': case 'WhileStatement': case 'DoWhileStatement':
        cyc++; cog += 1 + nesting;
        for (const [, c] of children(node)) walk(c, nesting + 1);
        return;
      case 'CatchClause':
        cyc++; cog += 1 + nesting;
        for (const [, c] of children(node)) walk(c, nesting + 1);
        return;
      case 'BreakStatement': case 'ContinueStatement':
        if (node.label) cog += 1;
        return;
      case 'LogicalExpression': {
        cyc++;
        if (!ignoreLogical.has(node)) {
          if (!(node.left.type === 'LogicalExpression' && node.left.operator === node.operator)) cog += 1;
          if (node.right.type === 'LogicalExpression' && node.right.operator === node.operator) ignoreLogical.add(node.right);
        }
        break;
      }
      case 'AssignmentExpression':
        if (node.operator === '&&=' || node.operator === '||=' || node.operator === '??=') cyc++;
        break;
      case 'MemberExpression': case 'CallExpression':
        if (node.optional) cyc++;
        break;
    }
    for (const [, c] of children(node)) walk(c, nesting);
  }
  if (isTopLevel) { for (const s of unitNode.body) walk(s, 0); }
  else walk(unitNode.body, 0);
  return { cyc, cog };
}

const rows = [];
const files = listJs(jsRoot).sort();
for (const file of files) {
  const rel = path.relative(root, file).split(path.sep).join('/');
  const src = fs.readFileSync(file, 'utf8');
  let ast;
  for (const sourceType of ['script', 'module']) {
    try { ast = acorn.parse(src, { ecmaVersion: 'latest', sourceType, locations: true, allowHashBang: true }); break; }
    catch (e) { if (sourceType === 'module') { console.error('PARSE FAIL', rel, e.message); } }
  }
  if (!ast) continue;
  setParents(ast, null);
  const fileLines = src.split(/\r?\n/).length;
  // top-level unit
  const top = measure(ast, true);
  rows.push([rel, 1, fileLines, fileLines, '<top-level>', top.cyc, top.cog]);
  (function visit(n) {
    if (isFn(n)) {
      const m = measure(n, false);
      rows.push([rel, n.loc.start.line, n.loc.end.line, n.loc.end.line - n.loc.start.line + 1, fnName(n), m.cyc, m.cog]);
    }
    for (const [, c] of children(n)) visit(c);
  })(ast);
}
const q = s => /[",]/.test(String(s)) ? '"' + String(s).replace(/"/g, '""') + '"' : String(s);
fs.mkdirSync(path.dirname(outPath), { recursive: true });
fs.writeFileSync(outPath, 'file,line,endLine,loc,function,cyclomatic,cognitive\n' + rows.map(r => r.map(q).join(',')).join('\n') + '\n');
console.log(`files=${files.length} functions=${rows.length} -> ${outPath}`);
