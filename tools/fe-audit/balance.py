"""Stack-based tag-balance check for Razor pages (read-only). Reports unclosed / mismatched tags.
Usage (from the repo root): py -I tools/fe-audit/balance.py <repoRoot>"""
import os, re, sys

ROOT = sys.argv[1]
WEB = os.path.join(ROOT, "Portfolio.Web")
TAGS = {"div", "section", "main", "article", "aside", "nav", "header", "footer", "ul", "ol", "li", "table", "thead",
        "tbody", "tr", "td", "th", "form", "label", "button", "select", "span", "p", "a", "h1", "h2", "h3", "h4", "h5",
        "h6", "pre", "textarea", "svg", "canvas", "fieldset", "legend", "small", "strong", "caption", "option", "output"}
tok = re.compile(r'<(/?)([a-zA-Z][a-zA-Z0-9]*)\b((?:[^>"\']|"[^"]*"|\'[^\']*\')*?)(/?)>', re.S)

for base in ("Pages", os.path.join("Areas", "Account", "Pages")):
    for dp, dn, fn in os.walk(os.path.join(WEB, base)):
        for f in sorted(fn):
            if not f.endswith(".cshtml"):
                continue
            p = os.path.join(dp, f)
            src = open(p, encoding="utf-8", errors="replace").read()
            # blank out Razor comments, script/style bodies, HTML comments (keep newlines for line numbers)
            def blank(m):
                return re.sub(r'[^\n]', ' ', m.group(0))
            src2 = re.sub(r'@\*.*?\*@', blank, src, flags=re.S)
            src2 = re.sub(r'<!--.*?-->', blank, src2, flags=re.S)
            src2 = re.sub(r'(<script\b[^>]*>)(.*?)(</script>)', lambda m: m.group(1) + blank(re.match(r'.*', m.group(2), re.S)) + m.group(3), src2, flags=re.S)
            src2 = re.sub(r'(<style\b[^>]*>)(.*?)(</style>)', lambda m: m.group(1) + blank(re.match(r'.*', m.group(2), re.S)) + m.group(3), src2, flags=re.S)
            stack, problems = [], []
            for m in tok.finditer(src2):
                closing, name, _, selfclose = m.group(1), m.group(2).lower(), m.group(3), m.group(4)
                if name not in TAGS or selfclose:
                    continue
                line = src2.count("\n", 0, m.start()) + 1
                if not closing:
                    stack.append((name, line))
                else:
                    if stack and stack[-1][0] == name:
                        stack.pop()
                    else:
                        # find nearest matching open
                        idx = next((i for i in range(len(stack) - 1, -1, -1) if stack[i][0] == name), None)
                        if idx is None:
                            problems.append(f"stray </{name}> at {line}")
                        else:
                            for nm, ln in stack[idx + 1:]:
                                problems.append(f"<{nm}> opened at {ln} closed implicitly by </{name}> at {line}")
                            del stack[idx:]
            for nm, ln in stack:
                problems.append(f"<{nm}> opened at {ln} never closed")
            rel = os.path.relpath(p, WEB).replace(os.sep, "/")
            if problems:
                print(rel)
                for pr in problems[:6]:
                    print("   ", pr)
print("done")
