"""LOC by project and language over git-tracked files.
Excludes bin/ obj/ (untracked anyway), Portfolio.Web/wwwroot/lib/, */Migrations/ and tools/ (the
measurement tools themselves).
Usage (from the repo root):  py -I tools/metrics/loc.py [repoRoot=.]
Reports: files, physical lines, non-blank lines, code lines (non-blank minus comment-only lines;
comment detection is a line-based heuristic: //, ///, /* ... */ blocks, @* *@, <!-- -->)."""
import subprocess, sys, os, re, collections
repo = sys.argv[1] if len(sys.argv) > 1 else '.'
files = subprocess.check_output(['git','-C',repo,'ls-files'], text=True, encoding='utf-8').splitlines()
LANG = {'.cs':'C#','.cshtml':'cshtml','.js':'JS','.css':'CSS','.cpp':'C++','.h':'C++','.hpp':'C++','.cc':'C++'}
GEN = 'Portfolio.Services/Data/RedlandsRoadNetwork.cs'
def excluded(p):
    return ('/bin/' in p or '/obj/' in p or p.startswith('Portfolio.Web/wwwroot/lib/') or p.startswith('tools/') or '/Migrations/' in p)
def count(path, lang):
    try:
        txt = open(os.path.join(repo,path), encoding='utf-8-sig', errors='replace').read()
    except Exception as e:
        return (0,0,0)
    lines = txt.splitlines()
    phys = len(lines); nonblank = 0; code = 0
    in_block = False; block_end = None
    for raw in lines:
        s = raw.strip()
        if not s: continue
        nonblank += 1
        if in_block:
            if block_end in s:
                in_block = False
                rest = s.split(block_end,1)[1].strip()
                if rest and not rest.startswith('//'): code += 1
            continue
        if s.startswith('//'): continue
        started = None
        for b,e in (('/*','*/'),('@*','*@'),('<!--','-->')):
            if s.startswith(b): started=(b,e); break
        if started:
            b,e = started
            if e in s[len(b):]:
                rest = s[len(b):].split(e,1)[1].strip()
                if rest: code += 1
            else:
                in_block = True; block_end = e
            continue
        code += 1
    return (phys, nonblank, code)
agg = collections.defaultdict(lambda: [0,0,0,0])
gen = None
for p in files:
    ext = os.path.splitext(p)[1].lower()
    if ext not in LANG or excluded(p): continue
    lang = LANG[ext]
    proj = p.split('/')[0] if '/' in p else '(root)'
    if proj == 'native': proj = 'native'
    c = count(p, lang)
    if p == GEN:
        gen = c; continue
    a = agg[(proj,lang)]; a[0]+=1; a[1]+=c[0]; a[2]+=c[1]; a[3]+=c[2]
print(f"{'Project':28} {'Lang':7} {'Files':>5} {'Physical':>9} {'NonBlank':>9} {'Code*':>9}")
tot = collections.defaultdict(lambda: [0,0,0,0])
for (proj,lang),a in sorted(agg.items()):
    print(f"{proj:28} {lang:7} {a[0]:5} {a[1]:9} {a[2]:9} {a[3]:9}")
    t = tot[lang]; 
    for i in range(4): t[i]+=a[i]
print('-'*72)
for lang,t in sorted(tot.items()):
    print(f"{'TOTAL':28} {lang:7} {t[0]:5} {t[1]:9} {t[2]:9} {t[3]:9}")
g = [sum(t[i] for t in tot.values()) for i in range(4)]
print(f"{'TOTAL (all langs)':28} {'':7} {g[0]:5} {g[1]:9} {g[2]:9} {g[3]:9}")
if gen: print(f"\nGenerated (excluded above): {GEN}: physical {gen[0]}, non-blank {gen[1]}, code* {gen[2]}")
