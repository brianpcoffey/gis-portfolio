"""Per-page asset inventory for Portfolio.Web Razor pages (read-only).
Usage (from the repo root): py -I tools/fe-audit/inventory.py <repoRoot> <out.json>"""
import os, re, sys, json

ROOT = sys.argv[1]  # repo root
WEB = os.path.join(ROOT, "Portfolio.Web")
WWW = os.path.join(WEB, "wwwroot")

pages = []
for base in ("Pages", os.path.join("Areas", "Account", "Pages")):
    for dp, dn, fn in os.walk(os.path.join(WEB, base)):
        for f in fn:
            if f.endswith(".cshtml") and not f.startswith("_"):
                pages.append(os.path.join(dp, f))
pages.sort()

link_re = re.compile(r'<link\b[^>]*>', re.I | re.S)
script_re = re.compile(r'<script\b([^>]*)>(.*?)</script\b[^>]*>', re.I | re.S)
style_block_re = re.compile(r'<style\b[^>]*>(.*?)</style\b[^>]*>', re.I | re.S)
style_attr_re = re.compile(r'\bstyle\s*=\s*"[^"]*"', re.I)
onattr_re = re.compile(r'\son[a-z]+\s*=\s*"', re.I)
href_re = re.compile(r'href\s*=\s*"([^"]+)"', re.I)
src_re = re.compile(r'src\s*=\s*"([^"]+)"', re.I)

def local_size(url):
    if url.startswith("~/"):
        p = os.path.join(WWW, url[2:].split("?")[0].replace("/", os.sep))
    elif url.startswith("/"):
        p = os.path.join(WWW, url[1:].split("?")[0].replace("/", os.sep))
    else:
        return None
    return os.path.getsize(p) if os.path.exists(p) else -1

out = []
for p in pages:
    rel = os.path.relpath(p, WEB).replace(os.sep, "/")
    txt = open(p, encoding="utf-8").read()
    lines = txt.split("\n")
    def lineno(idx):
        return txt.count("\n", 0, idx) + 1
    css, js, inline_js, cdn = [], [], [], []
    for m in link_re.finditer(txt):
        tag = m.group(0)
        if "stylesheet" not in tag:
            continue
        h = href_re.search(tag)
        if not h: continue
        url = h.group(1)
        size = local_size(url)
        integ = "integrity=" in tag
        css.append({"url": url, "line": lineno(m.start()), "bytes": size, "sri": integ,
                    "append_version": "asp-append-version" in tag})
        if url.startswith("http"):
            cdn.append(url)
    for m in script_re.finditer(txt):
        attrs, body = m.group(1), m.group(2)
        s = src_re.search(attrs)
        if s:
            url = s.group(1)
            js.append({"url": url, "line": lineno(m.start()), "bytes": local_size(url),
                       "defer": " defer" in attrs, "async": " async" in attrs,
                       "module": 'type="module"' in attrs, "sri": "integrity=" in attrs,
                       "append_version": "asp-append-version" in attrs})
            if url.startswith("http"):
                cdn.append(url)
        else:
            body_lines = body.strip().count("\n") + 1 if body.strip() else 0
            inline_js.append({"line": lineno(m.start()), "lines": body_lines,
                              "type_module": 'type="module"' in attrs,
                              "json": "application/json" in attrs or "importmap" in attrs})
    style_blocks = [{"line": lineno(m.start()), "lines": m.group(1).count("\n")} for m in style_block_re.finditer(txt)]
    style_attrs = len(style_attr_re.findall(txt))
    on_attrs = len(onattr_re.findall(txt))
    out.append({"page": rel, "loc": len(lines), "css": css, "js": js, "inline_js": inline_js,
                "style_blocks": style_blocks, "style_attrs": style_attrs, "on_handlers": on_attrs,
                "cdn": cdn})

json.dump(out, open(sys.argv[2], "w"), indent=1)
for o in out:
    css = ", ".join(f"{c['url'].split('/')[-1]}({c['bytes']})" for c in o["css"])
    js = ", ".join(f"{j['url'].split('/')[-1]}({j['bytes']}){'[defer]' if j['defer'] else ''}{'[async]' if j['async'] else ''}{'[module]' if j['module'] else ''}{'[sri]' if j['sri'] else ''}" for j in o["js"])
    inl = sum(i["lines"] for i in o["inline_js"])
    print(f"{o['page']} loc={o['loc']} | CSS: {css or '-'} | JS: {js or '-'} | inline<script>: {len(o['inline_js'])} ({inl} lines) | <style>: {len(o['style_blocks'])} | style=: {o['style_attrs']} | on*=: {o['on_handlers']}")
