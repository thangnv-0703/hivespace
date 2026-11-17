#!/usr/bin/env python3
"""Render a built diagram to PNG with headless Chrome/Edge so you can look at it.

    python screenshot.py diagram.html -o shot.png                 # dark theme, overview
    python screenshot.py diagram.html -o shot.png --theme light --flow checkout
    python screenshot.py diagram.html -o shot.png --width 1600 --height 1100

Also prints any JavaScript errors the page logged ("JS ERROR ..."), exit code 3 if any.
Set CHROME=/path/to/chrome when the browser is not in a standard location.
"""
import argparse
import os
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

CANDIDATES = [
    os.environ.get("CHROME", ""),
    r"C:\Program Files\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe",
    r"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
    r"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
    "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
    "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
]


def find_browser():
    for c in CANDIDATES:
        if c and Path(c).exists():
            return c
    for name in ("google-chrome", "google-chrome-stable", "chromium", "chromium-browser", "chrome", "msedge"):
        p = shutil.which(name)
        if p:
            return p
    return None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("html")
    ap.add_argument("-o", "--out", required=True)
    ap.add_argument("--width", type=int, default=1500)
    ap.add_argument("--height", type=int, default=1000)
    ap.add_argument("--theme", choices=["dark", "light"], default="dark")
    ap.add_argument("--flow", help="flow key to pre-select (uses ?flow=)")
    a = ap.parse_args()
    browser = find_browser()
    if not browser:
        print("No Chrome/Edge found; set CHROME=/path/to/browser or open the file manually.")
        sys.exit(4)
    url = Path(a.html).resolve().as_uri() + "?theme=" + a.theme + ("&flow=" + a.flow if a.flow else "")
    with tempfile.TemporaryDirectory() as prof:
        cmd = [browser, "--headless=new", "--disable-gpu", "--hide-scrollbars", "--no-first-run", "--no-default-browser-check",
               "--user-data-dir=" + prof, "--window-size=%d,%d" % (a.width, a.height), "--virtual-time-budget=4000",
               "--enable-logging=stderr", "--v=0", "--screenshot=" + str(Path(a.out).resolve()), url]
        r = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=90)
    errs = [l for l in (r.stderr or "").splitlines() if "CONSOLE" in l and ("Uncaught" in l or "Error" in l)]
    for l in errs:
        print("JS ERROR " + l.strip())
    if Path(a.out).exists():
        print("wrote " + a.out)
    else:
        print("screenshot failed:\n" + (r.stderr or "")[-2000:])
        sys.exit(5)
    if errs:
        sys.exit(3)


if __name__ == "__main__":
    main()
