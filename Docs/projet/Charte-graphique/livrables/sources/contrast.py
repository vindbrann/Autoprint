import sys
def lum(h):
    h=h.lstrip('#'); r,g,b=[int(h[i:i+2],16)/255 for i in (0,2,4)]
    f=lambda c: c/12.92 if c<=0.03928 else ((c+0.055)/1.055)**2.4
    return 0.2126*f(r)+0.7152*f(g)+0.0722*f(b)
def cr(a,b):
    la,lb=sorted([lum(a),lum(b)],reverse=True); return (la+.05)/(lb+.05)
L=dict(bg="#f5f6f8",surface="#ffffff",raised="#eef0f3",sidebar="#fbfbfc",
 text="#161a21",text2="#4a5262",muted="#646c7c",disabled="#9aa1ad",bstrong="#858d9b",
 primary="#1d5fcf",onp="#ffffff",psub="#e8f0fc",phover="#174ea9",
 success="#1b7a43",ssub="#e5f3ea",warning="#9a5200",wsub="#fbf0dd",danger="#c0302b",dsub="#fbe9e8",info="#0e6aa3",isub="#e2f0f9",neutral="#646c7c",focus="#1d5fcf")
D=dict(bg="#111317",surface="#171a1f",raised="#20242b",sidebar="#0d0f12",
 text="#e8eaee",text2="#a9afbb",muted="#8a919e",disabled="#5c636f",bstrong="#6b7381",
 primary="#5b9bf5",onp="#0a1322",psub="#16253d",phover="#7cb0f8",
 success="#4cc38a",ssub="#12291e",warning="#f0a33a",wsub="#2d2210",danger="#f2726c",dsub="#331614",info="#5cb3ea",isub="#112838",neutral="#8a919e",focus="#7cb0f8")
for name,T in (("LIGHT",L),("DARK",D)):
    print("==",name)
    for k in ("text","text2","muted","disabled","primary","success","warning","danger","info","neutral"):
        print(f"{k:9}", " ".join(f"{s}:{cr(T[k],T[s]):.2f}" for s in ("bg","surface","raised","sidebar")))
    print("on-primary/primary", f"{cr(T['onp'],T['primary']):.2f}", "hover", f"{cr(T['onp'],T['phover']):.2f}")
    for s in ("success","warning","danger","info"):
        sub=T[s[0]+'sub']; print(f"{s} on own subtle {cr(T[s],sub):.2f}; text on subtle {cr(T['text'],sub):.2f}")
    on = "#ffffff" if name=="LIGHT" else "#0d0f12"
    for s in ("success","warning","danger","info","neutral"): print(f"on-{s} {on} {cr(on,T[s]):.2f}")
    print("border-strong vs surface", f"{cr(T['bstrong'],T['surface']):.2f}", "focus vs bg", f"{cr(T['focus'],T['bg']):.2f}", "primary text on psub", f"{cr(T['primary'] if name=='LIGHT' else T['phover'],T['psub']):.2f}")
