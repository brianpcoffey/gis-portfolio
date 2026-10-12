"""Parse clang-tidy output (readability-function-cognitive-complexity + readability-function-size).
Usage (from the repo root):
  bash tools/metrics/cpp-complexity.sh . clang-tidy.log
  py -I tools/metrics/parse_clang_tidy.py clang-tidy.log"""
import re, sys, collections

BS = chr(92)
fn = {}
cog_re = re.compile(r"^(?P<file>.+?):(?P<line>\d+):\d+: warning: function '(?P<name>[^']+)' has cognitive complexity of (?P<n>\d+)")
size_re = re.compile(r"^(?P<file>.+?):(?P<line>\d+):\d+: note: (?P<n>\d+) (?P<what>lines including whitespace and comments|branches)")
for raw in open(sys.argv[1], encoding='utf-8', errors='replace'):
    raw = raw.strip()
    m = cog_re.match(raw)
    if m:
        f = m.group('file').replace(BS, '/')
        f = f[f.find('native/'):]
        fn[(f, int(m.group('line')))] = dict(name=m.group('name'), cog=int(m.group('n')), branches=0, lines=0, file=f, line=int(m.group('line')))
        continue
    m = size_re.match(raw)
    if m:
        f = m.group('file').replace(BS, '/')
        f = f[f.find('native/'):]
        k = (f, int(m.group('line')))
        if k in fn:
            if m.group('what') == 'branches':
                fn[k]['branches'] = int(m.group('n'))
            else:
                fn[k]['lines'] = int(m.group('n'))
rows = sorted(fn.values(), key=lambda r: -r['cog'])
print(f"functions with cognitive > 0: {len(rows)}")
for t in (10, 15, 25):
    print(f"  cognitive > {t}: {sum(r['cog'] > t for r in rows)}   branches+1 > {t}: {sum(r['branches'] + 1 > t for r in rows)}")
print("\n| # | function | cognitive | branches (if/loop/case) | lines | file:line |")
print("|---|---|---|---|---|---|")
for i, r in enumerate(rows[:15], 1):
    print(f"| {i} | `{r['name']}` | {r['cog']} | {r['branches']} | {r['lines']} | `{r['file']}:{r['line']}` |")
