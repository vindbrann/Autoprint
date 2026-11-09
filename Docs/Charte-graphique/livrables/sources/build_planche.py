import re, sys, pathlib, base64
src, icons_dir, out = map(pathlib.Path, sys.argv[1:4])
publish = len(sys.argv) > 4  # 4e argument : dossier des polices -> version publiable (polices inline, sans squelette)
html = src.read_text(encoding='utf-8')
names = sorted(set(re.findall(r'#i-([a-z0-9_]+)', html)))
syms = []
for n in names:
    d = re.search(r'd="([^"]+)"', (icons_dir / f'{n}.svg').read_text()).group(1)
    syms.append(f'    <symbol id="i-{n}" viewBox="0 -960 960 960"><path d="{d}"/></symbol>')
html = html.replace('{{SPRITE}}', '\n'.join(syms))
# bloc sombre : bascule explicite + suivi du système quand « Système » est choisi
m = re.search(r'/\*DARK\*/[^{]+\{(.*?)\n\}\n', html, re.S)
media = '@media (prefers-color-scheme: dark) {\n  html:not([data-theme="light"]):has(#theme-auto:checked) {' + m.group(1) + '\n  }\n}\n'
html = html[:m.end()] + media + html[m.end():]
html = html.replace('/*DARK*/', '')
if publish:
    fonts = pathlib.Path(sys.argv[4])
    def inline(mo):
        data = base64.b64encode((fonts / mo.group(1)).read_bytes()).decode()
        return f"url('data:font/woff2;base64,{data}')"
    html = re.sub(r"url\('\.\./\.\./ressources/polices/([^']+)'\)", inline, html)
    html = re.sub(r'<!doctype html>\s*<html[^>]*>\s*<head>\s*|<meta charset="utf-8">\s*|<meta name="viewport"[^>]*>\s*', '', html, flags=re.I)
    html = re.sub(r'</head>\s*<body>\s*', '', html); html = re.sub(r'</body>\s*</html>\s*$', '', html)
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(html, encoding='utf-8')
print(len(names), 'icônes,', len(html)//1024, 'Ko ->', out)
