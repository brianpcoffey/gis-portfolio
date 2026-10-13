"""Count hard-coded color literals outside the base.css token blocks (read-only).
Usage (from the repo root): py -I tools/fe-audit/colors.py <repoRoot> <out.json>"""
import os, re, sys, json, collections

ROOT = sys.argv[1]
WEB = os.path.join(ROOT, "Portfolio.Web")
files = []
for sub, exts in (("wwwroot/css", (".css",)), ("wwwroot/js", (".js",)), ("Pages", (".cshtml",)), ("Areas", (".cshtml",))):
    for dp, dn, fn in os.walk(os.path.join(WEB, sub)):
        for f in fn:
            if f.endswith(exts):
                files.append(os.path.join(dp, f))
files.sort()

hex_re = re.compile(r'(?<![\w&#])#(?:[0-9a-fA-F]{8}|[0-9a-fA-F]{6}|[0-9a-fA-F]{4}|[0-9a-fA-F]{3})\b')
fn_re = re.compile(r'\b(?:rgba?|hsla?)\(\s*[0-9.]+%?\s*[, ]\s*[0-9.]+%?\s*[, ]\s*[0-9.]+%?(?:\s*[,/]\s*[0-9.]+%?)?\s*\)')
# named colours only when used as a value of a colour-ish property / attribute
named = r'(?:white|black|red|green|blue|yellow|orange|purple|gray|grey|silver|gold|navy|teal|maroon|lime|aqua|fuchsia|olive|pink|crimson|darkred|darkgreen|lightgray|lightgrey|darkgray|darkgrey|steelblue|tomato|orangered|firebrick)'
named_re = re.compile(r'(?:color|background(?:-color)?|fill|stroke|border(?:-[a-z]+)?-color|outline-color)\s*[:=]\s*["\']?' + named + r'\b', re.I)

def token_block_lines(path):
    """Line ranges in base.css that *define* tokens (:root / body.dark-mode)."""
    if not path.endswith(os.path.join("css", "base.css")):
        return set()
    lines = open(path, encoding="utf-8", errors="replace").read().split("\n")
    keep, inside = set(), False
    for i, l in enumerate(lines, 1):
        if re.match(r'\s*(:root|body\.dark-mode)\s*\{', l):
            inside = True
        if inside:
            keep.add(i)
            if "}" in l:
                inside = False
    return keep

results = {}
samples = collections.defaultdict(list)
for p in files:
    rel = os.path.relpath(p, WEB).replace(os.sep, "/")
    if "/lib/" in rel.replace("\\", "/"):
        continue
    skip = token_block_lines(p)
    txt = open(p, encoding="utf-8", errors="replace").read()
    lines = txt.split("\n")
    count = 0
    in_comment = False
    for i, l in enumerate(lines, 1):
        if i in skip:
            continue
        s = l
        # strip CSS/JS block comments crudely (per line), and // comments in JS
        if p.endswith((".css", ".js")):
            if in_comment:
                if "*/" in s:
                    s = s.split("*/", 1)[1]; in_comment = False
                else:
                    continue
            s = re.sub(r'/\*.*?\*/', '', s)
            if "/*" in s:
                s = s.split("/*", 1)[0]; in_comment = True
            if p.endswith(".js"):
                s = re.sub(r'(^|\s)//.*$', '', s)
        # ignore url(data:...) svg payloads and fa icon classes
        s2 = re.sub(r'url\("data:[^"]*"\)', '', s)
        hits = hex_re.findall(s2) + fn_re.findall(s2) + named_re.findall(s2)
        # drop html entities like &#8212; and anchors like href="#foo" handled by regex guard
        hits = [h for h in hits if not re.fullmatch(r'#(?:[0-9]{3,4})', h) or True]
        if hits:
            count += len(hits)
            samples[rel].append((i, l.strip()[:150], hits))
    results[rel] = count

total = sum(results.values())
print(f"TOTAL hard-coded colour literals (excluding base.css token blocks, vendored lib/): {total}")
for rel, c in sorted(results.items(), key=lambda kv: -kv[1]):
    if c:
        print(f"{c:5d}  {rel}")
json.dump({"counts": results, "samples": samples}, open(sys.argv[2], "w"), indent=1)
