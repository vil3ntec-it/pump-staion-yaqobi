#!/usr/bin/env python3
"""
══ سنجه‌ای که کند شدن را خودش بگیرد (۱۴۰۵/۰۷/۲۲، بندِ ۵ِ کندی‌ها) ══

خروجیِ سنجهٔ پنج‌ساله (‎years‎) روی ‎main‎ و روی همین شاخه را کنارِ هم می‌گذارد. هر کاری
که در هر دو هست مقایسه می‌شود؛ از هر طرف کمینهٔ چند اجرا (نوسانِ رانر). سرخ وقتی که:
    شاخه > main × (1 + RATIO)   و   شاخه − main > FLOOR_MS
کارِ تازه (فقط در شاخه) و کارِ برداشته‌شده سنجیده نمی‌شوند — فقط گفته می‌شوند.

    python3 perf-compare.py base1.log base2.log -- head1.log head2.log
"""
import re, sys

RATIO = 0.30
FLOOR_MS = 150
LINE = re.compile(r"^(?P<label>.+?)\s{2,}(?P<ms>[\d,]+) ms\s*$")

def parse(path):
    seen, out = {}, {}
    with open(path, encoding="utf-8", errors="replace") as f:
        for raw in f:
            m = LINE.match(raw.rstrip("\n"))
            if not m: continue
            label = m.group("label").strip()
            if label.startswith(("🎯", "❌")): continue
            n = seen.get(label, 0); seen[label] = n + 1
            key = label if n == 0 else f"{label} #{n + 1}"
            out[key] = int(m.group("ms").replace(",", ""))
    return out

def best(paths):
    runs = [parse(p) for p in paths]
    keys = set().union(*runs) if runs else set()
    return {k: min(r[k] for r in runs if k in r) for k in keys}

def main(argv):
    if "--" not in argv: sys.exit(__doc__)
    i = argv.index("--")
    base, head = best(argv[:i]), best(argv[i + 1:])
    if not base or not head:
        print("::error::خروجیِ سنجهٔ پنج‌ساله خالی است — سنجه اجرا نشد؟"); return 2
    slow, rows = [], []
    for k in sorted(set(base) & set(head), key=lambda k: -head[k]):
        b, h = base[k], head[k]
        flag = h > b * (1 + RATIO) and h - b > FLOOR_MS
        rows.append((k, b, h, flag))
        if flag: slow.append((k, b, h))
    print(f"{'کار':<52}{'main':>9}{'شاخه':>9}")
    for k, b, h, flag in rows:
        pct = (h - b) * 100 // b if b else 0
        print(f"{k:<52}{b:>9,}{h:>9,}  {pct:+d}%{'  ⛔' if flag else ''}")
    only_head = sorted(set(head) - set(base))
    if only_head: print("\nکارهای تازه (فقط در شاخه، سنجیده نشد): " + "، ".join(only_head[:10]))
    if slow:
        for k, b, h in slow:
            print(f"::error::کند شد: {k} — main {b:,}ms ⇒ شاخه {h:,}ms (+{(h - b) * 100 // b}%)")
        return 1
    print(f"\n✅ هیچ کاری بیش از {int(RATIO * 100)}٪ (و {FLOOR_MS}ms) کند نشد — {len(rows)} کار سنجیده شد")
    return 0

if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
