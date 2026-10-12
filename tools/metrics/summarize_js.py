"""Summarize jsmetrics CSV as markdown tables.
Usage (from the repo root; the Node flag is required because jsmetrics uses the acorn bundled in Node):
  node --expose-internals tools/metrics/jsmetrics.js . js-metrics.csv
  py -I tools/metrics/summarize_js.py js-metrics.csv"""
import csv, sys, collections
rows = list(csv.DictReader(open(sys.argv[1], encoding='utf-8')))
for r in rows:
    for k in ('cyclomatic','cognitive','loc','line'): r[k] = int(r[k])
fns = [r for r in rows if r['function'] != '<top-level>']
print(f"functions measured: {len(fns)} (plus {len(rows)-len(fns)} top-level units)")
print("| Scope | functions | cyclo>10 | cyclo>15 | cyclo>25 | cog>10 | cog>15 | cog>25 |")
print("|---|---|---|---|---|---|---|---|")
def d(sel, label):
    v=[label,str(len(sel))]
    for k in ('cyclomatic','cognitive'):
        for t in (10,15,25): v.append(str(sum(1 for r in sel if r[k]>t)))
    print("| "+" | ".join(v)+" |")
d(fns,'all wwwroot/js (excl. lib/)')
print("\n| # | function | cyclo | cognitive | LOC | file:line |\n|---|---|---|---|---|---|")
for i,r in enumerate(sorted(fns, key=lambda r:(-r['cognitive'],-r['cyclomatic']))[:25],1):
    print(f"| {i} | `{r['function']}` | {r['cyclomatic']} | {r['cognitive']} | {r['loc']} | `{r['file']}:{r['line']}` |")
top = [r for r in rows if r['function']=='<top-level>']
print("\n### top-level (outside any function) units with cyclo>1")
for r in sorted(top, key=lambda r:-r['cyclomatic']):
    if r['cyclomatic']>1: print(f"- `{r['file']}` top-level cyclo {r['cyclomatic']} cog {r['cognitive']}")
agg = collections.defaultdict(lambda:[0,0,0,0])
for r in fns:
    a=agg[r['file']]; a[0]+=1; a[1]+=r['cognitive']; a[2]=max(a[2],r['cognitive']); a[3]+= (1 if r['cognitive']>15 else 0)
print("\n| file | functions | sum cognitive | max cognitive | fns cog>15 |\n|---|---|---|---|---|")
for f,a in sorted(agg.items(), key=lambda kv:-kv[1][1])[:20]:
    print(f"| `{f}` | {a[0]} | {a[1]} | {a[2]} | {a[3]} |")
