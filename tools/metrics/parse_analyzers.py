"""Parse a MSBuild log for CA1502 / IDE0005 / IDE0051 / IDE0052, dedupe, and calibrate csmetrics vs CA1502.
Usage (from the repo root):
  py -I tools/metrics/parse_analyzers.py <build.log> <cs-metrics.csv> <out-dir> [repoRoot=.]
build.log is the output of a build in a THROWAWAY clone (never in the repo) where CA1502 is enabled at
threshold 1 (a CodeMetricsConfig.txt containing "CA1502: 1" included as an AdditionalFiles item, plus an
.editorconfig severity for CA1502) and IDE0005/IDE0051/IDE0052 are warnings, built with
  dotnet build -c Release --no-incremental -p:ContinuousIntegrationBuild=false > build.log
repoRoot is the directory the build ran in; it is stripped from diagnostic paths so they match csmetrics rows."""
import re, sys, csv, collections, os

log, metrics_csv, outdir = sys.argv[1], sys.argv[2], sys.argv[3]
repo_root = os.path.abspath(sys.argv[4] if len(sys.argv) > 4 else '.').replace(chr(92), '/').rstrip('/') + '/'
os.makedirs(outdir, exist_ok=True)
BS = chr(92)
pat = re.compile(r'^(?P<file>[A-Za-z]:.+?)\((?P<line>\d+),(?P<col>\d+)\): warning (?P<id>CA1502|IDE0005|IDE0051|IDE0052): (?P<msg>.*?) \[(?P<proj>[^\]]+)\]\s*$')
seen = set()
items = []
for raw in open(log, encoding='utf-8', errors='replace'):
    m = pat.match(raw.strip())
    if not m:
        continue
    f = m.group('file').replace(BS, '/')
    rel = f[len(repo_root):] if f.lower().startswith(repo_root.lower()) else f
    key = (m.group('id'), rel, int(m.group('line')), int(m.group('col')))
    if key in seen:
        continue
    seen.add(key)
    items.append(dict(id=m.group('id'), file=rel, line=int(m.group('line')), col=int(m.group('col')), msg=m.group('msg')))

by_id = collections.Counter(x['id'] for x in items)
print('unique diagnostics:', dict(by_id))


def proj(rel):
    return rel.split('/')[0]


for diag in ('IDE0005', 'IDE0051', 'IDE0052'):
    sel = [x for x in items if x['id'] == diag]
    c = collections.Counter(proj(x['file']) for x in sel)
    print(f'{diag} by project:', dict(sorted(c.items())))
    with open(os.path.join(outdir, f'{diag}.txt'), 'w', encoding='utf-8') as fh:
        for x in sorted(sel, key=lambda x: (x['file'], x['line'])):
            fh.write(f"{x['file']}:{x['line']}  {x['msg']}\n")

# CA1502 values
ca = []
for x in items:
    if x['id'] != 'CA1502':
        continue
    m = re.match(r"'(?P<name>.+?)' has a cyclomatic complexity of '(?P<n>\d+)'", x['msg'])
    if m:
        ca.append(dict(file=x['file'], line=x['line'], name=m.group('name'), cc=int(m.group('n'))))
with open(os.path.join(outdir, 'CA1502.csv'), 'w', encoding='utf-8', newline='') as fh:
    w = csv.writer(fh)
    w.writerow(['file', 'line', 'name', 'ca1502'])
    for r in sorted(ca, key=lambda r: -r['cc']):
        w.writerow([r['file'], r['line'], r['name'], r['cc']])


def cat(rel):
    if '/Migrations/' in rel:
        return 'migrations'
    if rel.endswith('RedlandsRoadNetwork.cs'):
        return 'generated'
    if rel.startswith('Portfolio.Tests/'):
        return 'tests'
    if rel.startswith('Portfolio.Benchmarks/'):
        return 'benchmarks'
    return 'prod'


print('\nCA1502 counts ABOVE threshold (CA1502 fires when value > threshold):')
for scope in ('prod', 'tests', 'benchmarks', 'migrations', 'generated'):
    sel = [r for r in ca if cat(r['file']) == scope]
    print(f"  {scope:11} >10: {sum(r['cc'] > 10 for r in sel):3}  >15: {sum(r['cc'] > 15 for r in sel):3}  >20: {sum(r['cc'] > 20 for r in sel):3}  >25: {sum(r['cc'] > 25 for r in sel):3}  (methods with cc>=2 reported: {len(sel)})")

print('\nTop 25 prod by CA1502:')
for r in sorted([r for r in ca if cat(r['file']) == 'prod'], key=lambda r: -r['cc'])[:25]:
    print(f"  {r['cc']:4}  {r['name']:40} {r['file']}:{r['line']}")

# calibration join with csmetrics on (file, line)
mine = {}
for r in csv.DictReader(open(metrics_csv, encoding='utf-8')):
    mine.setdefault((r['file'], int(r['line'])), []).append(r)
matched = diff = 0
diffs = []
for r in ca:
    cands = mine.get((r['file'], r['line']))
    if not cands:
        continue
    matched += 1
    m = cands[0]
    if int(m['cyclomatic']) != r['cc']:
        diff += 1
        diffs.append((r['cc'] - int(m['cyclomatic']), r['cc'], int(m['cyclomatic']), r['file'], r['line'], r['name']))
print(f'\nCalibration: CA1502 rows joined to csmetrics by file:line = {matched} of {len(ca)}; values differ on {diff}')
for d in sorted(diffs, key=lambda d: -abs(d[0]))[:40]:
    print(f"  CA1502={d[1]:4} mine={d[2]:4} delta={d[0]:+d}  {d[5]}  {d[3]}:{d[4]}")
unmatched = [r for r in ca if (r['file'], r['line']) not in mine]
print(f'\nCA1502 rows not joined ({len(unmatched)}), first 25:')
for r in sorted(unmatched, key=lambda r: -r['cc'])[:25]:
    print(f"  {r['cc']:4}  {r['name']:40} {r['file']}:{r['line']}")
