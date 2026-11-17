#!/usr/bin/env python3
"""Build an interactive HTML flow diagram from a JSON model.

    python build_diagram.py model.json -o diagram.html          # build + lint report
    python build_diagram.py model.json --check                  # lint only, no output
    python build_diagram.py model.json -o out.html --strict     # exit 1 if any warning

What it does:

1. Routes every edge that has no explicit "d" into an orthogonal path with rounded
   corners (auto sides, straight lines when boxes overlap on an axis, optional "via").
2. Places edge labels (midpoint of the longest segment unless lx/ly/labelAt given).
3. Lints the layout: unknown ids, overlapping nodes, arrows crossing unrelated nodes,
   labels colliding with nodes or other labels, text too wide for its box, content
   outside the viewBox or under the legend.
4. Injects the resolved model into assets/template.html.

The model format is documented in references/model-schema.md.
"""
import argparse
import html
import json
import math
import re
import sys
from pathlib import Path

TEMPLATE = Path(__file__).resolve().parent.parent / "assets" / "template.html"

# JetBrains Mono advance width is 0.6em; these mirror the sizes used by the template.
CH_LABEL, CH_SUB, CH_SUB2, CH_EDGE, CH_FRAME = 6.6, 5.4, 4.5, 4.9, 5.1
RADIUS = 8


# --------------------------------------------------------------- geometry helpers

class Rect:
    def __init__(self, x, y, w, h):
        self.x, self.y, self.w, self.h = x, y, w, h

    @property
    def r(self):
        return self.x + self.w

    @property
    def b(self):
        return self.y + self.h

    def shrink(self, d):
        return Rect(self.x + d, self.y + d, max(0, self.w - 2 * d), max(0, self.h - 2 * d))

    def intersects(self, o):
        return self.x < o.r and o.x < self.r and self.y < o.b and o.y < self.b


def seg_hits_rect(a, b, rc):
    """Segment a-b intersects the open rectangle rc."""
    (x1, y1), (x2, y2) = a, b
    if abs(y1 - y2) < 0.5:  # horizontal
        lo, hi = sorted((x1, x2))
        return rc.y < y1 < rc.b and lo < rc.r and hi > rc.x
    if abs(x1 - x2) < 0.5:  # vertical
        lo, hi = sorted((y1, y2))
        return rc.x < x1 < rc.r and lo < rc.b and hi > rc.y
    # diagonal (only from explicit "d"): sample points
    for i in range(1, 20):
        t = i / 20
        px, py = x1 + (x2 - x1) * t, y1 + (y2 - y1) * t
        if rc.x < px < rc.r and rc.y < py < rc.b:
            return True
    return False


def anchor(n, side, at):
    x, y, w, h = n["x"], n["y"], n["w"], n["h"]
    return {
        "top": (x + w * at, y),
        "bottom": (x + w * at, y + h),
        "left": (x, y + h * at),
        "right": (x + w, y + h * at),
    }[side]


def horiz(side):
    return side in ("left", "right")


def auto_sides(s, t):
    sr, tr = Rect(s["x"], s["y"], s["w"], s["h"]), Rect(t["x"], t["y"], t["w"], t["h"])
    gx = tr.x - sr.r if tr.x >= sr.r else (sr.x - tr.r if tr.r <= sr.x else -1)
    gy = tr.y - sr.b if tr.y >= sr.b else (sr.y - tr.b if tr.b <= sr.y else -1)
    if gx >= 0 and (gy < 0 or gx >= gy):
        return ("right", "left") if tr.x >= sr.r else ("left", "right")
    if gy >= 0:
        return ("bottom", "top") if tr.y >= sr.b else ("top", "bottom")
    return ("right", "left")


def dedupe(pts):
    out = []
    for p in pts:
        if not out or abs(out[-1][0] - p[0]) > 0.01 or abs(out[-1][1] - p[1]) > 0.01:
            out.append(p)
    # drop collinear middle points
    i = 1
    while i < len(out) - 1:
        a, b, c = out[i - 1], out[i], out[i + 1]
        if (abs(a[0] - b[0]) < 0.01 and abs(b[0] - c[0]) < 0.01) or (abs(a[1] - b[1]) < 0.01 and abs(b[1] - c[1]) < 0.01):
            out.pop(i)
        else:
            i += 1
    return out


def route(e, nodes):
    s, t = nodes[e["from"]], nodes[e["to"]]
    fs, ts = e.get("fromSide"), e.get("toSide")
    if not fs or not ts:
        a, b = auto_sides(s, t)
        fs, ts = fs or a, ts or b
    fa, ta = e.get("fromAt"), e.get("toAt")
    # Straight line when both sides face each other and the boxes overlap on the cross axis.
    if fa is None and ta is None and not e.get("via"):
        if horiz(fs) and horiz(ts) and fs != ts:
            lo, hi = max(s["y"], t["y"]), min(s["y"] + s["h"], t["y"] + t["h"])
            if hi - lo >= 8:
                y = (lo + hi) / 2
                fa, ta = (y - s["y"]) / s["h"], (y - t["y"]) / t["h"]
        elif not horiz(fs) and not horiz(ts) and fs != ts:
            lo, hi = max(s["x"], t["x"]), min(s["x"] + s["w"], t["x"] + t["w"])
            if hi - lo >= 8:
                x = (lo + hi) / 2
                fa, ta = (x - s["x"]) / s["w"], (x - t["x"]) / t["w"]
    S, E = anchor(s, fs, 0.5 if fa is None else fa), anchor(t, ts, 0.5 if ta is None else ta)
    via = [tuple(p) for p in e.get("via", [])]
    if via:
        pts, d = [S], "h" if horiz(fs) else "v"
        for p in via + [E]:
            a = pts[-1]
            if abs(a[0] - p[0]) > 0.01 and abs(a[1] - p[1]) > 0.01:
                corner = (p[0], a[1]) if d == "h" else (a[0], p[1])
                pts.append(corner)
                d = "v" if d == "h" else "h"
            else:
                d = "h" if abs(a[1] - p[1]) <= 0.01 else "v"
            pts.append(p)
        return dedupe(pts), fs, ts
    if horiz(fs) and horiz(ts):
        if fs == ts:  # loop out on the same side
            out = (max(S[0], E[0]) + 24) if fs == "right" else (min(S[0], E[0]) - 24)
            pts = [S, (out, S[1]), (out, E[1]), E]
        else:
            mx = e.get("midX", (S[0] + E[0]) / 2)
            pts = [S, (mx, S[1]), (mx, E[1]), E]
    elif not horiz(fs) and not horiz(ts):
        if fs == ts:
            out = (max(S[1], E[1]) + 24) if fs == "bottom" else (min(S[1], E[1]) - 24)
            pts = [S, (S[0], out), (E[0], out), E]
        else:
            my = e.get("midY", (S[1] + E[1]) / 2)
            pts = [S, (S[0], my), (E[0], my), E]
    elif horiz(fs):
        pts = [S, (E[0], S[1]), E]
    else:
        pts = [S, (S[0], E[1]), E]
    return dedupe(pts), fs, ts


def path_d(pts):
    if len(pts) < 2:
        return ""
    out = ["M%g %g" % (round(pts[0][0], 2), round(pts[0][1], 2))]
    for i in range(1, len(pts) - 1):
        p0, p1, p2 = pts[i - 1], pts[i], pts[i + 1]
        l1, l2 = math.dist(p0, p1), math.dist(p1, p2)
        r = min(RADIUS, l1 / 2, l2 / 2)
        a = (p1[0] - (p1[0] - p0[0]) / l1 * r, p1[1] - (p1[1] - p0[1]) / l1 * r)
        b = (p1[0] + (p2[0] - p1[0]) / l2 * r, p1[1] + (p2[1] - p1[1]) / l2 * r)
        out.append("L%g %g Q%g %g %g %g" % (round(a[0], 2), round(a[1], 2), round(p1[0], 2), round(p1[1], 2), round(b[0], 2), round(b[1], 2)))
    out.append("L%g %g" % (round(pts[-1][0], 2), round(pts[-1][1], 2)))
    return " ".join(out)


def parse_d(d):
    """Rough polyline from an explicit path (M/L/H/V/Q only) for linting."""
    toks = re.findall(r"[MLHVQmlhvq]|-?\d*\.?\d+", d)
    pts, cmd, i, cur = [], None, 0, (0, 0)
    while i < len(toks):
        if toks[i].isalpha():
            cmd = toks[i]
            i += 1
            continue
        if cmd in ("M", "L"):
            cur = (float(toks[i]), float(toks[i + 1]))
            i += 2
        elif cmd == "H":
            cur = (float(toks[i]), cur[1])
            i += 1
        elif cmd == "V":
            cur = (cur[0], float(toks[i]))
            i += 1
        elif cmd == "Q":
            cur = (float(toks[i + 2]), float(toks[i + 3]))
            i += 4
        else:
            i += 1
            continue
        pts.append(cur)
    return pts


def point_at(pts, frac):
    total = sum(math.dist(pts[i], pts[i + 1]) for i in range(len(pts) - 1))
    goal, run = total * frac, 0
    for i in range(len(pts) - 1):
        seg = math.dist(pts[i], pts[i + 1])
        if run + seg >= goal and seg > 0:
            t = (goal - run) / seg
            return (pts[i][0] + (pts[i + 1][0] - pts[i][0]) * t, pts[i][1] + (pts[i + 1][1] - pts[i][1]) * t)
        run += seg
    return pts[-1]


def label_pos(pts, e):
    if e.get("labelAt") is not None:
        return point_at(pts, float(e["labelAt"]))
    best, bi = -1, 0
    for i in range(len(pts) - 1):
        seg = math.dist(pts[i], pts[i + 1])
        if seg > best:
            best, bi = seg, i
    a, b = pts[bi], pts[bi + 1]
    return ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2)


# --------------------------------------------------------------- build + lint

def build(model):
    issues = []  # (level, message)
    warn = lambda m: issues.append(("warn", m))
    err = lambda m: issues.append(("error", m))
    meta = model.setdefault("meta", {})
    vb = meta.get("viewBox", [1280, 820])
    nodes_list = model.get("nodes", [])
    nodes = {}
    for n in nodes_list:
        if n["id"] in nodes:
            err("duplicate node id '%s'" % n["id"])
        for k in ("x", "y", "w", "h"):
            if k not in n:
                err("node '%s' is missing '%s'" % (n["id"], k))
        nodes[n["id"]] = n
    if any(i[0] == "error" for i in issues):
        return model, issues

    # route edges
    edge_pts, seen = {}, set()
    for e in model.get("edges", []):
        if e.get("id") in seen:
            err("duplicate edge id '%s'" % e.get("id"))
        seen.add(e.get("id"))
        if e.get("from") not in nodes or e.get("to") not in nodes:
            err("edge '%s' references unknown node (%s → %s)" % (e.get("id"), e.get("from"), e.get("to")))
            continue
        if e.get("d"):
            pts = parse_d(e["d"])
            fs = ts = None
        else:
            pts, fs, ts = route(e, nodes)
            e["d"] = path_d(pts)
            if len(pts) >= 2 and ts:
                last_h = abs(pts[-1][1] - pts[-2][1]) < 0.5
                if last_h != horiz(ts):
                    warn("edge '%s' arrives at the %s side of '%s' from the wrong direction; adjust 'via' or 'toSide'" % (e["id"], ts, e["to"]))
        edge_pts[e["id"]] = pts
        if e.get("label") and (e.get("lx") is None or e.get("ly") is None) and len(pts) >= 2:
            p = label_pos(pts, e)
            e["lx"], e["ly"] = round(p[0], 1), round(p[1], 1)
        if len(pts) >= 2 and e.get("label"):
            seg_len = max(math.dist(pts[i], pts[i + 1]) for i in range(len(pts) - 1))
            if seg_len < len(e["label"]) * CH_EDGE + 14 and e.get("lx") is not None:
                warn("edge '%s' label '%s' is longer than its longest segment (%.0fpx); shorten it or lengthen the edge" % (e["id"], e["label"], seg_len))

    # references
    flows = model.get("flows", {})
    edge_ids = {e.get("id") for e in model.get("edges", [])}
    for k, f in flows.items():
        for st in f.get("steps", []):
            for eid in st.get("e", []):
                if eid not in edge_ids:
                    err("flow '%s' references unknown edge '%s'" % (k, eid))
        for nid in f.get("nodes", []):
            if nid not in nodes:
                err("flow '%s' references unknown node '%s'" % (k, nid))
    for nt in model.get("notes", []):
        if nt.get("on") and nt["on"] not in nodes:
            err("note '%s' is on unknown node '%s'" % (nt.get("id"), nt["on"]))
    for sid in model.get("specs", {}):
        if sid not in nodes:
            warn("spec for unknown node '%s'" % sid)
    for n in nodes_list:
        if n.get("flow") and n["flow"] not in flows:
            err("node '%s' opens unknown flow '%s'" % (n["id"], n["flow"]))

    # geometry lint
    rects = {k: Rect(n["x"], n["y"], n["w"], n["h"]) for k, n in nodes.items()}
    ids = list(rects)
    for i in range(len(ids)):
        for j in range(i + 1, len(ids)):
            if rects[ids[i]].shrink(1).intersects(rects[ids[j]].shrink(1)):
                warn("nodes '%s' and '%s' overlap" % (ids[i], ids[j]))
    for k, n in nodes.items():
        r = rects[k]
        if r.x < 0 or r.y < 0 or r.r > vb[0] or r.b > vb[1]:
            warn("node '%s' is outside the viewBox %s" % (k, vb))
        sigil = 30 if n["h"] >= 40 else 12
        if len(n.get("label", "")) * (CH_LABEL if n["h"] >= 40 else 6.0) > n["w"] - sigil:
            warn("node '%s' label '%s' is too wide for w=%s (needs ~%.0f)" % (k, n.get("label"), n["w"], len(n.get("label", "")) * CH_LABEL + sigil))
        if n.get("sub") and len(n["sub"]) * CH_SUB > n["w"] - 12:
            warn("node '%s' sub '%s' is too wide for w=%s (needs ~%.0f)" % (k, n["sub"], n["w"], len(n["sub"]) * CH_SUB + 12))
        if n.get("sub2") and len(n["sub2"]) * CH_SUB2 > n["w"] - 10:
            warn("node '%s' sub2 is too wide for w=%s" % (k, n["w"]))
        if n.get("sub2") and n["h"] < 70:
            warn("node '%s' has sub2 but h<70; three text lines need ~70px" % k)

    for e in model.get("edges", []):
        pts = edge_pts.get(e.get("id"))
        if not pts:
            continue
        for k, rc in rects.items():
            if k in (e["from"], e["to"]):
                continue
            inner = rc.shrink(3)
            if any(seg_hits_rect(pts[i], pts[i + 1], inner) for i in range(len(pts) - 1)):
                warn("edge '%s' (%s → %s) crosses node '%s'; add 'via' points or move a node" % (e["id"], e["from"], e["to"], k))

    label_rects = []
    for e in model.get("edges", []):
        if e.get("label") and e.get("lx") is not None:
            w = len(e["label"]) * CH_EDGE + 10
            lr = Rect(e["lx"] - w / 2, e["ly"] - 7, w, 13)
            for k, rc in rects.items():
                if lr.intersects(rc.shrink(1)):
                    warn("label of edge '%s' ('%s') overlaps node '%s'" % (e["id"], e["label"], k))
            for oid, other in label_rects:
                if lr.intersects(other):
                    warn("labels of edges '%s' and '%s' overlap" % (e["id"], oid))
            label_rects.append((e["id"], lr))

    for f in model.get("frames", []):
        if not f.get("label"):
            continue
        region = f.get("style") == "region"
        w = len(f["label"]) * (5.4 if region else CH_FRAME) + (16 if region else 0)
        ly = f["y"] + f["h"] - 8 if f.get("labelPos") == "bottom" else f["y"] + 16
        fx = f.get("labelX", f["x"] + 10)
        lr = Rect(fx - 2, ly - 11, w + 4, 15)
        for k, rc in rects.items():
            if lr.intersects(rc):
                warn("frame label '%s' overlaps node '%s'; use labelPos 'bottom' or labelX" % (f["label"], k))
        for e in model.get("edges", []):
            pts = edge_pts.get(e.get("id")) or []
            if any(seg_hits_rect(pts[i], pts[i + 1], lr) for i in range(len(pts) - 1)):
                warn("edge '%s' runs through frame label '%s'" % (e.get("id"), f["label"]))

    legend_y = meta.get("legendY", vb[1] - 62)
    band = Rect(0, legend_y - 16, vb[0], 72)
    for k, rc in rects.items():
        if rc.intersects(band):
            warn("node '%s' overlaps the legend band (y≥%s); enlarge viewBox height or set meta.legendY" % (k, legend_y - 16))
    return model, issues


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("model")
    ap.add_argument("-o", "--out")
    ap.add_argument("--check", action="store_true", help="lint only")
    ap.add_argument("--strict", action="store_true", help="exit 1 on warnings")
    ap.add_argument("--template", default=str(TEMPLATE))
    a = ap.parse_args()
    try:
        sys.stdout.reconfigure(encoding="utf-8")
    except Exception:
        pass
    model = json.loads(Path(a.model).read_text(encoding="utf-8"))
    model, issues = build(model)
    errors = [m for lvl, m in issues if lvl == "error"]
    warns = [m for lvl, m in issues if lvl == "warn"]
    for m in errors:
        print("ERROR  " + m)
    for m in warns:
        print("WARN   " + m)
    print("%d node(s), %d edge(s), %d flow(s), %d note(s): %d error(s), %d warning(s)" % (
        len(model.get("nodes", [])), len(model.get("edges", [])), len(model.get("flows", {})), len(model.get("notes", [])), len(errors), len(warns)))
    if errors:
        sys.exit(2)
    if not a.check:
        if not a.out:
            ap.error("-o/--out is required unless --check")
        tpl = Path(a.template).read_text(encoding="utf-8")
        payload = json.dumps(model, ensure_ascii=False).replace("</", "<\\/")
        title = html.escape(model.get("meta", {}).get("title", "Flow diagram"))
        out = tpl.replace("/*__MODEL__*/null", payload).replace("__TITLE__", title)
        Path(a.out).write_text(out, encoding="utf-8", newline="\n")
        print("wrote " + a.out)
    if a.strict and warns:
        sys.exit(1)


if __name__ == "__main__":
    main()
