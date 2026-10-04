#!/usr/bin/env python3
"""
ممیزیِ عرضه (۱۴۰۵/۰۷/۲۰) — فایلِ «مجوزِ کتابخانه‌ها» که همراهِ نصاب می‌رود.

    python3 tools/third-party-notices.py native/PumpYaqobi.App/bin/Release/net8.0/PumpYaqobi.deps.json

هر بستهٔ NuGetی که در خروجیِ برنامه هست (از ‎deps.json‎) با نسخه و مجوزش از
‎nuspec‎ِ همان بسته در کَشِ NuGet خوانده می‌شود، و متنِ کاملِ هر مجوز از
‎native/installer/licenses/‎ (در مخزن، پس بی اینترنت ساخته می‌شود) پشتش می‌آید.
خروجی: ‎native/installer/THIRD-PARTY-NOTICES.txt‎.

⛔ مجوزی که این‌جا شناخته نشود اسکریپت را می‌اندازد — فایلِ ناقص بدتر از نبودن است.
⚠️ ‎VideoLAN.LibVLC.Windows‎ فقط روی ویندوز می‌آید و در ‎deps.json‎ِ لینوکس نیست؛
دستی در ‎EXTRA‎ نوشته شده.
"""
import glob
import json
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
LIC = os.path.join(ROOT, "native", "installer", "licenses")
OUT = os.path.join(ROOT, "native", "installer", "THIRD-PARTY-NOTICES.txt")

# مجوزهایی که nuspec به‌شکلِ «فایل» یا «نشانی» می‌گوید ⇒ نامِ کوتاه
ALIAS = {
    "Avalonia.Angle.Windows.Natives": "BSD-3-Clause (ANGLE)",
    "QuestPDF": "QuestPDF Community License",
    "SkiaSharp.HarfBuzz": "MIT",
    "System.Memory": "MIT",
    "Avalonia.BuildServices": "MIT",
}
EXTRA = [("VideoLAN.LibVLC.Windows", "3.0.21", "LGPL-2.1-or-later (+ plugins under their own licenses)")]
TEXTS = [
    ("MIT", "MIT.txt"),
    ("Apache-2.0", "Apache-2.0.txt"),
    ("MS-PL", "MS-PL.txt"),
    ("LGPL-2.1-or-later", "LGPL-2.1.txt"),
    ("BSD-3-Clause (ANGLE)", "ANGLE-BSD.txt"),
    ("QuestPDF Community License", "QuestPDF.txt"),
]


def license_of(name, ver):
    if name in ALIAS:
        return ALIAS[name]
    nus = glob.glob(os.path.expanduser(f"~/.nuget/packages/{name.lower()}/{ver}/*.nuspec"))
    if not nus:
        sys.exit(f"nuspec نیست: {name} {ver}")
    t = open(nus[0], encoding="utf-8", errors="ignore").read()
    m = re.search(r'<license type="expression">([^<]+)</license>', t)
    if not m:
        sys.exit(f"مجوزِ {name} {ver} شناخته نشد — به ALIAS بیفزایید")
    return m.group(1).strip()


def main(deps_path):
    deps = json.load(open(deps_path, encoding="utf-8"))
    pkgs = [k.split("/") for k, v in deps["libraries"].items() if v["type"] == "package"]
    rows = [(n, v, license_of(n, v)) for n, v in pkgs] + EXTRA
    rows.sort(key=lambda r: r[0].lower())
    known = {k for k, _ in TEXTS}
    for n, _, lic in rows:
        base = lic.split(" (+")[0]
        if base not in known:
            sys.exit(f"متنِ مجوزِ «{lic}» ({n}) در licenses/ نیست")

    w = max(len(n) for n, _, _ in rows)
    lines = [
        "Pump Benzin (PumpYaqobi) — Third-party software notices",
        "این برنامه از کتابخانه‌های زیر استفاده می‌کند. مجوزِ هر کدام پایینِ همین فایل آمده است.",
        "",
        "LibVLC is loaded dynamically from the libvlc folder and may be replaced by the user",
        "with a compatible build, as the LGPL-2.1 requires. Source: https://code.videolan.org/videolan/vlc",
        "",
    ]
    lines += [f"{n.ljust(w)}  {v.ljust(22)}  {lic}" for n, v, lic in rows]
    for key, fn in TEXTS:
        lines += ["", "=" * 78, key, "=" * 78, open(os.path.join(LIC, fn), encoding="utf-8").read().rstrip()]
    lines += ["", "=" * 78, "QuestPDF bundled native dependencies", "=" * 78]
    for fn in sorted(os.listdir(os.path.join(LIC, "questpdf-deps"))):
        lines += ["", "--- " + fn, open(os.path.join(LIC, "questpdf-deps", fn), encoding="utf-8", errors="ignore").read().rstrip()]
    open(OUT, "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")
    print(f"{len(rows)} بسته ⇒ {OUT}")


if __name__ == "__main__":
    main(sys.argv[1])
