"""Summarize csmetrics CSV.
Usage (from the repo root):
  dotnet run -c Release --project tools/metrics/csmetrics -- . cs-metrics.csv
  py -I tools/metrics/summarize_cs.py cs-metrics.csv [topN]
Prints markdown tables (distribution, top-N by cognitive and by CA1502, per-file sums, ceiling-15 list).
Only category "prod" (Portfolio.* projects) feeds the production tables; tools/ is a separate category.
Columns used: cyclomatic (syntax McCabe incl. catch / and-or patterns / switch-expression arms / ??=),
cyclomatic_ca1502 (exactly what the CA1502 analyzer reports), cognitive (SonarSource rules)."""
import csv, sys, collections

rows = list(csv.DictReader(open(sys.argv[1], encoding='utf-8')))
N = int(sys.argv[2]) if len(sys.argv) > 2 else 25
for r in rows:
    for k in ('cyclomatic', 'cognitive', 'cyclomatic_ca1502', 'loc', 'line'):
        r[k] = int(r[k])

prod = [r for r in rows if r['category'] == 'prod']
tests = [r for r in rows if r['category'] == 'tests']
bench = [r for r in rows if r['category'] == 'benchmarks']
print("members by category:", dict(collections.Counter(r['category'] for r in rows)))


def table(sel, key, title, n=N):
    print(f"\n### {title}")
    print("| # | Member | CA1502 cyclo | McCabe cyclo | Cognitive | LOC | file:line |")
    print("|---|---|---|---|---|---|---|")
    order = sorted(sel, key=lambda r: (-r[key], -r['cognitive'], -r['cyclomatic_ca1502'], r['file'], r['line']))
    for i, r in enumerate(order[:n], 1):
        print(f"| {i} | `{r['type']}.{r['member']}` | {r['cyclomatic_ca1502']} | {r['cyclomatic']} | {r['cognitive']} | {r['loc']} | `{r['file']}:{r['line']}` |")


def dist_header():
    print("| Scope | members | CA1502 >10 | >15 | >25 | McCabe >10 | >15 | >25 | cognitive >10 | >15 | >25 |")
    print("|---|---|---|---|---|---|---|---|---|---|---|")


def dist(sel, label):
    out = [label, str(len(sel))]
    for key in ('cyclomatic_ca1502', 'cyclomatic', 'cognitive'):
        for t in (10, 15, 25):
            out.append(str(sum(1 for r in sel if r[key] > t)))
    print("| " + " | ".join(out) + " |")


print("\n### Distribution (count of members ABOVE the threshold)")
dist_header()
dist(prod, 'prod (Common+Repositories+Services+Web)')
for p in sorted(set(r['project'] for r in prod)):
    dist([r for r in prod if r['project'] == p], '  ' + p)
dist(tests, 'tests')
dist(bench, 'benchmarks')
dist([r for r in rows if r['category'] in ('migrations', 'generated')], 'migrations + generated network')

print()
for t in (10, 15, 20, 25):
    print(f"prod members over {t}: CA1502 {sum(r['cyclomatic_ca1502'] > t for r in prod)}, "
          f"cognitive {sum(r['cognitive'] > t for r in prod)}, "
          f"either {sum(r['cyclomatic_ca1502'] > t or r['cognitive'] > t for r in prod)}")

table(prod, 'cognitive', f'Top {N} prod members by cognitive complexity')
table(prod, 'cyclomatic_ca1502', f'Top {N} prod members by CA1502 cyclomatic complexity')
table(tests + bench, 'cognitive', 'Top 10 test/benchmark members by cognitive', 10)

agg = collections.defaultdict(lambda: [0, 0, 0, 0])
for r in prod:
    a = agg[r['file']]
    a[0] += 1
    a[1] += r['cyclomatic_ca1502']
    a[2] += r['cognitive']
    a[3] = max(a[3], r['cognitive'])
print("\n### Prod files by summed cognitive complexity (top 20)")
print("| file | members | sum CA1502 | sum cognitive | max cognitive |")
print("|---|---|---|---|---|")
for f, a in sorted(agg.items(), key=lambda kv: -kv[1][2])[:20]:
    print(f"| `{f}` | {a[0]} | {a[1]} | {a[2]} | {a[3]} |")

print("\n### Every prod member with CA1502 > 15 or cognitive > 15 (the ceiling-15 violation list)")
print("| Member | CA1502 | Cognitive | LOC | file:line |")
print("|---|---|---|---|---|")
for r in sorted([r for r in prod if r['cyclomatic_ca1502'] > 15 or r['cognitive'] > 15], key=lambda r: (r['file'], r['line'])):
    print(f"| `{r['type']}.{r['member']}` | {r['cyclomatic_ca1502']} | {r['cognitive']} | {r['loc']} | `{r['file']}:{r['line']}` |")
