"""Génère les maquettes HTML statiques à partir de sources/.

  python3 build_maquettes.py <dossier icônes Material Symbols svg-300/outlined> [<dossier publication>]

- autoprint.src.css        -> maquettes/commun/autoprint.css (le bloc /*DARK*/ est doublé
                               dans un @media prefers-color-scheme pour le mode « Système »)
- maquettes/<dossier>--<nom>.src.html -> maquettes/<dossier>/<nom>.html
- maquettes/index.src.html -> maquettes/index.html
Gabarits : {{SPRITE}} (icônes utilisées, en ligne), {{> partiel clé=valeur}} (fichier maquettes/_partiel.html,
où {{clé}} est remplacé ; active=X marque data-nav="X" comme rubrique active).
Avec un dossier de publication : copie l'arborescence sous <publication>/livrables/ et produit
<publication>/index.html (page d'accueil, liens préfixés par livrables/maquettes/).
"""
import re, sys, shutil, pathlib
HERE = pathlib.Path(__file__).resolve().parent
LIV = HERE.parent
icons = pathlib.Path(sys.argv[1])
pub = pathlib.Path(sys.argv[2]) if len(sys.argv) > 2 else None

def build_css():
    css = (HERE / 'autoprint.src.css').read_text(encoding='utf-8')
    m = re.search(r'/\*DARK\*/[^{]+\{(.*?)\n\}\n', css, re.S)
    media = ('@media (prefers-color-scheme: dark) {\n  html:not([data-theme="light"]):has(#theme-auto:checked) {'
             + m.group(1) + '\n  }\n}\n')
    css = (css[:m.end()] + media + css[m.end():]).replace('/*DARK*/', '')
    out = LIV / 'maquettes/commun/autoprint.css'; out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(css, encoding='utf-8')
    return css

def partial(mo):
    name, args = mo.group(1), dict(re.findall(r'(\w+)="([^"]*)"', mo.group(2) or ''))
    txt = (HERE / f'maquettes/_{name}.html').read_text(encoding='utf-8')
    for k, v in args.items():
        txt = txt.replace('{{' + k + '}}', v)
    if 'active' in args:
        txt = txt.replace(f'data-nav="{args["active"]}"', f'data-nav="{args["active"]}" class="is-active" aria-current="page"')
    return re.sub(r'\{\{(?!>|SPRITE)\w+\}\}', '', txt)

def sprite(html):
    out = []
    for n in sorted(set(re.findall(r'#i-([a-z0-9_-]+)', html))):
        d = re.search(r'd="([^"]+)"', (icons / f'{n}.svg').read_text()).group(1)
        out.append(f'<symbol id="i-{n}" viewBox="0 -960 960 960"><path d="{d}"/></symbol>')
    return '\n'.join(out)

def render(src):
    html = src.read_text(encoding='utf-8')
    for _ in range(4):
        html = re.sub(r'\{\{>\s*(\w[\w-]*)((?:\s+\w+="[^"]*")*)\s*\}\}', partial, html)
    html = re.sub(r'\{\{(?!>|SPRITE)\w+\}\}', '', html)
    html = html.replace('{{SPRITE}}', sprite(html))
    own = src.name[:-len('.src.html')].split('--')[-1] + '.html'
    return re.sub(r'(<nav class="mq-screens"[^>]*>.*?</nav>)', lambda m: m.group(1).replace(f'href="{own}"', f'href="{own}" aria-current="page"'), html, flags=re.S)

build_css()
pages = []
for src in sorted((HERE / 'maquettes').glob('*.src.html')):
    stem = src.name[:-len('.src.html')]
    rel = 'index.html' if stem == 'index' else '/'.join(stem.split('--')) + '.html'
    out = LIV / 'maquettes' / rel; out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(render(src), encoding='utf-8'); pages.append(rel)
print('pages :', ', '.join(pages))

if pub:
    if pub.exists(): shutil.rmtree(pub)
    for sub in ['maquettes', 'ressources/polices', 'ressources/logo']:
        shutil.copytree(LIV / sub, pub / 'livrables' / sub)
    idx = (LIV / 'maquettes/index.html').read_text(encoding='utf-8')
    idx = re.sub(r'(href|src)="(?!https?:|#|data:)([^"]+)"', lambda m: f'{m.group(1)}="livrables/maquettes/{m.group(2)}"', idx)
    idx = re.sub(r'<!doctype html>\s*<html[^>]*>\s*<head>\s*|<meta charset="utf-8">\s*|<meta name="viewport"[^>]*>\s*', '', idx, flags=re.I)
    idx = re.sub(r'</head>\s*<body[^>]*>\s*', '', idx); idx = re.sub(r'</body>\s*</html>\s*$', '', idx)
    (pub / 'index.html').write_text(idx, encoding='utf-8')
    print('publication :', pub)
