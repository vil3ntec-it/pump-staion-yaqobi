"""
🌐 نوشته‌های فارسیِ برنامه که هنوز در فرهنگِ پشتو نیستند (۱۴۰۵/۰۷/۲۸).

    python3 tools/lang-missing.py            ⇐ فهرستِ JSONِ نوشته‌های بی‌ترجمه (همراهِ فایلشان)

هر نوشتهٔ فارسیِ C# و AXAML را همان شکلی بیرون می‌کشد که <c>Lang.T</c> در لحظهٔ نمایش
می‌بیند (رشته‌ی ‎$"…{x}…"‎ ⇐ «…{0}…»، ‎StringFormat‎ ⇐ «{0} …»). ترجمه‌ها را در
‎PumpYaqobi.Domain/Lang/ps.json‎ بگذارید؛ آزمونِ ‎LangTests‎ نوشته‌های AXAML را می‌سنجد.
"""
import re, os, sys, json, html, collections
FA = re.compile(r'[\u0621-\u064A\u0671-\u06D3\u06FA-\u06FF]')
ESC = {'n':'\n','t':'\t','r':'\r','0':'\0','"':'"',"'":"'",'\\':'\\','a':'\a','b':'\b','f':'\f','v':'\v'}
out = collections.OrderedDict()
def add(s, where):
    if s is None: return
    if not FA.search(s): return
    s2 = s.strip(' ')
    if not s2: return
    out.setdefault(s, set()).add(where)

def unesc(body):
    r=[];i=0
    while i<len(body):
        c=body[i]
        if c=='\\' and i+1<len(body):
            n=body[i+1]
            if n=='u': r.append(chr(int(body[i+2:i+6],16))); i+=6; continue
            if n=='x':
                j=i+2; h=''
                while j<len(body) and len(h)<4 and body[j] in '0123456789abcdefABCDEF': h+=body[j]; j+=1
                r.append(chr(int(h,16))); i=j; continue
            r.append(ESC.get(n,n)); i+=2; continue
        r.append(c); i+=1
    return ''.join(r)

def lex_cs(src, where):
    i=0; L=len(src)
    def read_interp(i, verbatim, raw_n=0, dollars=1):
        # at position after opening quote(s); returns (template, end_index)
        parts=[]; buf=[]; hole=0; holes_src=[]
        while i<L:
            c=src[i]
            if raw_n:
                if src.startswith('"'*raw_n, i): return ''.join(buf), i+raw_n, holes_src
            elif c=='"':
                if verbatim and i+1<L and src[i+1]=='"': buf.append('"'); i+=2; continue
                return ''.join(buf), i+1, holes_src
            if not verbatim and not raw_n and c=='\\':
                j=i+2
                if src[i+1]=='u': j=i+6
                buf.append(unesc(src[i:j])); i=j; continue
            if c=='{':
                if not raw_n and i+1<L and src[i+1]=='{': buf.append('{'); i+=2; continue
                if raw_n and dollars>1:
                    if not src.startswith('{'*dollars, i): buf.append(c); i+=1; continue
                    i+=dollars-1
                # hole: scan to matching }
                depth=1; j=i+1; hs=j
                while j<L and depth>0:
                    d=src[j]
                    if d=='"' or (d=='@' and j+1<L and src[j+1]=='"') or (d=='$' and j+1<L and src[j+1] in '"@'):
                        j=lex_string_at(j)
                        continue
                    if d=="'":
                        k=j+1
                        while k<L and src[k]!="'":
                            k+= 2 if src[k]=='\\' else 1
                        j=k+1; continue
                    if d=='{': depth+=1
                    elif d=='}': depth-=1
                    j+=1
                holes_src.append(src[hs:j-1])
                buf.append('{%d}'%hole); hole+=1
                i=j
                if raw_n and dollars>1: i+=dollars-1
                continue
            if c=='}' and not raw_n and i+1<L and src[i+1]=='}': buf.append('}'); i+=2; continue
            buf.append(c); i+=1
        return ''.join(buf), i, holes_src
    def lex_string_at(j):
        # returns index after string starting at j; adds string
        k=j; interp=False; verb=False
        while src[k] in '$@':
            if src[k]=='$': interp=True
            else: verb=True
            k+=1
        dollars = src[j:k].count('$')
        # raw?
        if src.startswith('"""',k):
            n=0
            while k+n<L and src[k+n]=='"': n+=1
            body_start=k+n
            if interp:
                t,e,hs=read_interp(body_start, True, n, dollars)
            else:
                e=src.find('"'*n, body_start); t=src[body_start:e]; e+=n; hs=[]
            t2 = dedent_raw(t)
            add(t2, where)
            for h in hs: lex_cs_inner(h)
            return e
        k+=1
        if interp:
            t,e,hs=read_interp(k, verb)
            add(t, where)
            for h in hs: lex_cs_inner(h)
            return e
        if verb:
            buf=[]
            while k<L:
                if src[k]=='"':
                    if k+1<L and src[k+1]=='"': buf.append('"'); k+=2; continue
                    break
                buf.append(src[k]); k+=1
            add(''.join(buf), where); return k+1
        buf=[]
        while k<L and src[k]!='"':
            if src[k]=='\\': buf.append(src[k:k+2] if src[k+1]!='u' else src[k:k+6]); k+= 2 if src[k+1]!='u' else 6; continue
            if src[k]=='\n': break
            buf.append(src[k]); k+=1
        add(unesc(''.join(buf)), where); return k+1
    def lex_cs_inner(h):
        nonlocal_src = h
        sub(h, where)
    def dedent_raw(t):
        lines=t.split('\n')
        if len(lines)>1 and lines[0].strip()=='' :
            last=lines[-1]
            ind=len(last)-len(last.lstrip()) if last.strip()=='' else 0
            body=lines[1:-1] if last.strip()=='' else lines[1:]
            return '\n'.join(l[ind:] for l in body)
        return t
    while i<L:
        c=src[i]
        if src.startswith('//',i):
            e=src.find('\n',i); i = L if e<0 else e; continue
        if src.startswith('/*',i):
            e=src.find('*/',i+2); i = L if e<0 else e+2; continue
        if c=="'":
            k=i+1
            while k<L and src[k]!="'":
                k+= 2 if src[k]=='\\' else 1
            i=k+1; continue
        if c=='"' or (c in '$@' and i+1<L and (src[i+1]=='"' or (src[i+1] in '$@' and i+2<L and src[i+2] in '"$'))):
            # ensure not identifier like @class
            i=lex_string_at(i); continue
        i+=1
def sub(h, where):
    lex_cs(h, where)

def axaml(src, where):
    for m in re.finditer(r'([\w:.]+)="([^"]*)"', src):
        name, val = m.group(1), html.unescape(m.group(2))
        if val.startswith('{'):
            for sf in re.finditer(r"StringFormat=('([^']*)'|((?:\{\}|[^,}{])*(?:\{\d+(?::[^}]*)?\}(?:[^,}{])*)*))", val):
                f = sf.group(2) if sf.group(2) is not None else sf.group(3)
                if f is None: continue
                f = f.replace('{}','',1) if f.startswith('{}') else f
                f = re.sub(r'\{(\d+):[^}]*\}', r'{\1}', f)
                add(f, where)
            for q in re.finditer(r"'([^']*)'", val):
                add(q.group(1), where)
            continue
        if name.startswith('xmlns') or name in ('x:Name','Name','Classes','x:Class'): continue
        add(val, where)
    for m in re.finditer(r'>([^<>]+)<', src):
        add(html.unescape(m.group(1)).strip(), where)
    # comments contain persian: removed by not matching inside <!-- --> ... crude: strip comments first


here=os.path.dirname(os.path.abspath(__file__))
root=os.path.dirname(here)
projs=['PumpYaqobi.App','PumpYaqobi.Shell','PumpYaqobi.Reporting','PumpYaqobi.Services','PumpYaqobi.Application','PumpYaqobi.Domain','PumpYaqobi.Infrastructure','PumpYaqobi.Persistence']
for p in projs:
    for dp, dn, fn in os.walk(os.path.join(root,p)):
        if '/obj' in dp or '/bin' in dp: continue
        for f in sorted(fn):
            path=os.path.join(dp,f); rel=os.path.relpath(path,root)
            if f.endswith('.cs'):
                lex_cs(open(path,encoding='utf-8').read(), rel)
            elif f.endswith('.axaml'):
                s=open(path,encoding='utf-8').read()
                s=re.sub(r'<!--.*?-->','',s,flags=re.S)
                axaml(s, rel)
ps=json.load(open(os.path.join(root,'PumpYaqobi.Domain','Lang','ps.json'),encoding='utf-8'))
miss={k:sorted(v) for k,v in out.items() if k not in ps and k.strip() not in ps}
json.dump(miss, sys.stdout, ensure_ascii=False, indent=1)
print(f"\n// {len(miss)} of {len(out)} missing", file=sys.stderr)
