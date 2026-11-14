# -*- coding: utf-8 -*-
"""작은 SVG 다이어그램 도구: 상자, 화살표, 시퀀스 다이어그램, 레인."""
import html

FONT = "'Malgun Gothic','Noto Sans KR',sans-serif"

# 모듈 색 (채움, 테두리)
C = {
    'boot':  ('#E3E9F7', '#3B5BA9'),
    'data':  ('#E6F2E6', '#3E8E41'),
    'run':   ('#E2F0FB', '#1F78B4'),
    'meta':  ('#EFE6F7', '#7B4FA6'),
    'save':  ('#F6E6F0', '#A64F86'),
    'view':  ('#FDEEDC', '#D9822B'),
    'fx':    ('#FDE6E3', '#C8553D'),
    'sound': ('#FFF5D6', '#B8860B'),
    'ui':    ('#DDF3F1', '#16857A'),
    'plat':  ('#ECECEC', '#6B6B6B'),
    'fw':    ('#F4F4F4', '#8A8A8A'),
    'note':  ('#FFFBE6', '#C9A227'),
    'warn':  ('#FDECEC', '#C0392B'),
    'plain': ('#FFFFFF', '#555555'),
    'dark':  ('#2F3B52', '#2F3B52'),
}


def esc(s):
    return html.escape(str(s), quote=True)


def tw(s, fs):
    """대략적인 글자 폭(px)."""
    w = 0.0
    for ch in str(s):
        o = ord(ch)
        if o >= 0x1100:
            w += fs * 0.95
        elif ch in 'mwMW@':
            w += fs * 0.8
        elif ch in 'il.,:;|!\'`()[]{}':
            w += fs * 0.3
        elif ch.isupper():
            w += fs * 0.64
        else:
            w += fs * 0.54
    return w


class Svg:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.items = []

    def add(self, s):
        self.items.append(s)

    def render(self):
        defs = (
            '<defs>'
            '<marker id="ah" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">'
            '<path d="M0,0 L10,5 L0,10 z" fill="#444"/></marker>'
            '<marker id="ahb" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">'
            '<path d="M0,0 L10,5 L0,10 z" fill="#1F78B4"/></marker>'
            '<marker id="ahr" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">'
            '<path d="M0,0 L10,5 L0,10 z" fill="#C0392B"/></marker>'
            '<marker id="ahg" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="7" markerHeight="7" orient="auto-start-reverse">'
            '<path d="M0,0 L10,5 L0,10 z" fill="#3E8E41"/></marker>'
            '</defs>')
        return (f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {self.w} {self.h}" '
                f'width="100%" style="font-family:{FONT}">' + defs + ''.join(self.items) + '</svg>')

    # ---- 기본 요소 ----
    def text(self, x, y, s, fs=12, anchor='middle', color='#222', weight='normal', italic=False):
        lines = str(s).split('\n')
        n = len(lines)
        y0 = y - (n - 1) * fs * 1.25 / 2
        st = ' font-style="italic"' if italic else ''
        out = [f'<text x="{x}" y="{y0}" font-size="{fs}" text-anchor="{anchor}" '
               f'dominant-baseline="central" fill="{color}" font-weight="{weight}"{st}>']
        for i, ln in enumerate(lines):
            dy = 0 if i == 0 else fs * 1.25
            out.append(f'<tspan x="{x}" dy="{dy}">{esc(ln)}</tspan>')
        out.append('</text>')
        self.add(''.join(out))

    def rect(self, x, y, w, h, kind='plain', r=6, dash=False, sw=1.4, fill=None, stroke=None):
        f, s = C[kind]
        f = fill or f
        s = stroke or s
        d = ' stroke-dasharray="5,3"' if dash else ''
        self.add(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{f}" stroke="{s}" stroke-width="{sw}"{d}/>')

    def box(self, x, y, w, h, title, sub=None, kind='plain', fs=12.5, sfs=10.5, dash=False, bold=True):
        self.rect(x, y, w, h, kind, dash=dash)
        col = C[kind][1] if kind not in ('plain', 'fw', 'note') else '#222'
        if kind == 'dark':
            col = '#fff'
        if sub:
            nt = len(str(title).split('\n'))
            ns = len(str(sub).split('\n'))
            tot = nt * fs * 1.25 + ns * sfs * 1.25
            ty = y + h / 2 - tot / 2 + nt * fs * 1.25 / 2
            sy = ty + nt * fs * 1.25 / 2 + ns * sfs * 1.25 / 2 + 1
            self.text(x + w / 2, ty, title, fs, color=col, weight='bold' if bold else 'normal')
            self.text(x + w / 2, sy, sub, sfs, color='#444' if kind != 'dark' else '#dde')
        else:
            self.text(x + w / 2, y + h / 2, title, fs, color=col, weight='bold' if bold else 'normal')
        return (x, y, w, h)

    def diamond(self, cx, cy, w, h, label, kind='note', fs=11):
        f, s = C[kind]
        pts = f'{cx},{cy-h/2} {cx+w/2},{cy} {cx},{cy+h/2} {cx-w/2},{cy}'
        self.add(f'<polygon points="{pts}" fill="{f}" stroke="{s}" stroke-width="1.4"/>')
        self.text(cx, cy, label, fs, weight='bold')

    def line(self, pts, arrow=True, color='#444', dash=False, sw=1.4, marker='ah', start_arrow=False):
        d = ' '.join(f'{x},{y}' for x, y in pts)
        da = ' stroke-dasharray="5,3"' if dash else ''
        me = f' marker-end="url(#{marker})"' if arrow else ''
        ms = f' marker-start="url(#{marker})"' if start_arrow else ''
        self.add(f'<polyline points="{d}" fill="none" stroke="{color}" stroke-width="{sw}"{da}{me}{ms}/>')

    def arrow(self, x1, y1, x2, y2, label=None, fs=10, color='#444', dash=False, marker='ah',
              lx=None, ly=None, anchor='middle', lcolor='#333', bg=True):
        self.line([(x1, y1), (x2, y2)], color=color, dash=dash, marker=marker)
        if label:
            mx = lx if lx is not None else (x1 + x2) / 2
            my = ly if ly is not None else (y1 + y2) / 2
            if bg:
                lines = label.split('\n')
                wmax = max(tw(l, fs) for l in lines)
                hh = len(lines) * fs * 1.25
                bx = mx - wmax / 2 - 3 if anchor == 'middle' else (mx - 3 if anchor == 'start' else mx - wmax - 3)
                self.add(f'<rect x="{bx}" y="{my-hh/2-1}" width="{wmax+6}" height="{hh+2}" fill="#fff" opacity="0.9"/>')
            self.text(mx, my, label, fs, anchor=anchor, color=lcolor)

    def badge(self, cx, cy, n, kind='dark', r=9, fs=10):
        f, s = C[kind]
        self.add(f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="{s}"/>')
        self.text(cx, cy, n, fs, color='#fff', weight='bold')


# ---- 시퀀스 다이어그램 ----
def sequence(actors, msgs, width=760, top=14, gap=30, fs=10.5, head_h=40, notes_w=None):
    """actors: [(key, title, sub, kind)], msgs: [(frm, to, label, opts)]
    opts: 'ret' 점선 반환, 'self' 자기 호출, 'note' 메모(frm 위치), 'sep' 구분선(label)
    """
    n = len(actors)
    margin = 10
    col_w = (width - 2 * margin) / n
    xs = {a[0]: margin + col_w * i + col_w / 2 for i, a in enumerate(actors)}
    # 높이 계산
    y = top + head_h + 18
    rows = []
    for m in msgs:
        frm, to, label, opt = (list(m) + [None])[:4]
        nl = len(str(label).split('\n'))
        hgt = gap + (nl - 1) * fs * 1.25
        if opt == 'self':
            hgt += 10
        rows.append((y, frm, to, label, opt, nl))
        y += hgt
    H = y + 10
    s = Svg(width, H)
    for k, title, sub, kind in actors:
        x = xs[k]
        bw = col_w - 10
        s.line([(x, top + head_h), (x, H - 6)], arrow=False, color='#AAA', dash=True, sw=1)
        s.box(x - bw / 2, top, bw, head_h, title, sub, kind=kind, fs=11 if tw(title, 11) < bw - 8 else 9.3, sfs=9)
    for (y, frm, to, label, opt, nl) in rows:
        if opt == 'sep':
            s.line([(margin, y), (width - margin, y)], arrow=False, color='#C9A227', dash=True, sw=1)
            tw_ = tw(label, fs) + 12
            s.rect(width / 2 - tw_ / 2, y - 9, tw_, 18, 'note', r=9)
            s.text(width / 2, y, label, fs, weight='bold')
            continue
        if opt == 'note':
            x = xs[frm]
            lines = str(label).split('\n')
            wmax = max(tw(l, fs - 0.5) for l in lines) + 14
            hh = len(lines) * (fs - 0.5) * 1.25 + 8
            x0 = min(max(x - wmax / 2, 4), width - wmax - 4)
            s.rect(x0, y - hh / 2, wmax, hh, 'note', r=4)
            s.text(x0 + wmax / 2, y, label, fs - 0.5)
            continue
        x1, x2 = xs[frm], xs[to]
        if opt == 'self':
            s.line([(x1, y - 6), (x1 + 34, y - 6), (x1 + 34, y + 8), (x1 + 3, y + 8)])
            s.text(x1 + 40, y + 1, label, fs, anchor='start')
            continue
        dash = opt == 'ret'
        color = '#777' if dash else '#333'
        s.line([(x1, y), (x2 - (3 if x2 > x1 else -3), y)], dash=dash, color=color)
        mid = (x1 + x2) / 2
        lines = str(label).split('\n')
        ly = y - 6 - (nl - 1) * fs * 1.25 / 2 - fs * 0.45
        wmax = max(tw(l, fs) for l in lines)
        s.add(f'<rect x="{mid-wmax/2-2}" y="{ly-nl*fs*1.25/2}" width="{wmax+4}" height="{nl*fs*1.25}" fill="#fff" opacity="0.85"/>')
        s.text(mid, ly, label, fs, color='#555' if dash else '#222')
    return s


def table(headers, rows, widths=None, cls=''):
    out = [f'<table class="{cls}">']
    if widths:
        out.append('<colgroup>' + ''.join(f'<col style="width:{w}">' for w in widths) + '</colgroup>')
    out.append('<thead><tr>' + ''.join(f'<th>{h}</th>' for h in headers) + '</tr></thead><tbody>')
    for r in rows:
        out.append('<tr>' + ''.join(f'<td>{c}</td>' for c in r) + '</tr>')
    out.append('</tbody></table>')
    return ''.join(out)
