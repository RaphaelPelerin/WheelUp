"""Derive an 'open chest' GLB from a 'closed chest' GLB.

The closed mesh is a single welded shell, so the lid has to be cut out:
  1. split every triangle against the horizontal plane y = seam
  2. chain the resulting cut segments into the rim loop
  3. build a real cavity below the rim (ledge + walls + floor) for the body
  4. cap the lid underside
  5. rotate the lid around the rear hinge edge

Everything keeps the original UVs and material, so the open chest is literally
the closed chest with its lid raised.
"""
import json, struct
import numpy as np

# ---------------------------------------------------------------- GLB reading

COMP_DTYPE = {5120: 'i1', 5121: 'u1', 5122: 'i2', 5123: 'u2', 5125: 'u4', 5126: 'f4'}
NCOMP = {'SCALAR': 1, 'VEC2': 2, 'VEC3': 3, 'VEC4': 4}


def load_glb(path):
    with open(path, 'rb') as fh:
        struct.unpack('<III', fh.read(12))
        clen, _ = struct.unpack('<II', fh.read(8))
        js = json.loads(fh.read(clen).decode('utf-8'))
        blen, _ = struct.unpack('<II', fh.read(8))
        bin_ = fh.read(blen)
    return js, bin_


def read_accessor(js, bin_, index):
    acc = js['accessors'][index]
    bv = js['bufferViews'][acc['bufferView']]
    assert 'byteStride' not in bv or bv['byteStride'] == 0, 'interleaved buffer not supported'
    n = NCOMP[acc['type']]
    dt = np.dtype('<' + COMP_DTYPE[acc['componentType']])
    off = bv.get('byteOffset', 0) + acc.get('byteOffset', 0)
    data = np.frombuffer(bin_, dtype=dt, count=acc['count'] * n, offset=off)
    return data.reshape(acc['count'], n).astype(np.float64 if dt.kind == 'f' else np.int64)


def view_bytes(js, bin_, index):
    bv = js['bufferViews'][index]
    off = bv.get('byteOffset', 0)
    return bin_[off:off + bv['byteLength']]


# ---------------------------------------------------------------- plane split

class Builder:
    """Accumulates vertices/triangles while de-duplicating interpolated points."""

    def __init__(self):
        self.pos, self.nrm, self.uv, self.tris = [], [], [], []
        self.cache = {}

    def add_existing(self, pos, nrm, uv, i):
        key = ('v', int(i))
        if key not in self.cache:
            self.cache[key] = len(self.pos)
            self.pos.append(pos[i]); self.nrm.append(nrm[i]); self.uv.append(uv[i])
        return self.cache[key]

    def add_lerp(self, pos, nrm, uv, a, b, t):
        key = ('e', int(min(a, b)), int(max(a, b)))
        if key not in self.cache:
            tt = t if a <= b else 1.0 - t
            i, j = (a, b) if a <= b else (b, a)
            self.cache[key] = len(self.pos)
            self.pos.append(pos[i] + tt * (pos[j] - pos[i]))
            n = nrm[i] + tt * (nrm[j] - nrm[i])
            self.nrm.append(n / (np.linalg.norm(n) + 1e-12))
            self.uv.append(uv[i] + tt * (uv[j] - uv[i]))
        return self.cache[key]

    def tri(self, a, b, c):
        if a != b and b != c and a != c:
            self.tris.append((a, b, c))

    def arrays(self):
        return (np.array(self.pos, np.float64), np.array(self.nrm, np.float64),
                np.array(self.uv, np.float64), np.array(self.tris, np.int64))


def split_by_plane(pos, nrm, uv, tris, seam):
    """Split the mesh on y = seam. Returns (above, below, cut_segments)."""
    above, below = Builder(), Builder()
    segments = []
    d = pos[:, 1] - seam

    for tri in tris:
        i0, i1, i2 = tri
        ds = d[[i0, i1, i2]]
        if np.all(ds >= 0):
            above.tri(*[above.add_existing(pos, nrm, uv, i) for i in tri]); continue
        if np.all(ds <= 0):
            below.tri(*[below.add_existing(pos, nrm, uv, i) for i in tri]); continue

        # Rotate the triangle so that the lone vertex on one side comes first.
        order = [0, 1, 2]
        for r in range(3):
            a, b, c = (order[r], order[(r + 1) % 3], order[(r + 2) % 3])
            if (ds[a] > 0) != (ds[b] > 0) and (ds[a] > 0) != (ds[c] > 0):
                break
        ia, ib, ic = tri[a], tri[b], tri[c]
        da, db, dc = ds[a], ds[b], ds[c]

        tab = da / (da - db)
        tac = da / (da - dc)
        lone_above = da > 0
        top, bot = (above, below) if lone_above else (below, above)

        # lone vertex side: one triangle
        pa = top.add_existing(pos, nrm, uv, ia)
        pab = top.add_lerp(pos, nrm, uv, ia, ib, tab)
        pac = top.add_lerp(pos, nrm, uv, ia, ic, tac)
        top.tri(pa, pab, pac)

        # opposite side: quad -> two triangles
        qb = bot.add_existing(pos, nrm, uv, ib)
        qc = bot.add_existing(pos, nrm, uv, ic)
        qab = bot.add_lerp(pos, nrm, uv, ia, ib, tab)
        qac = bot.add_lerp(pos, nrm, uv, ia, ic, tac)
        bot.tri(qab, qb, qc)
        bot.tri(qab, qc, qac)

        p1 = pos[ia] + tab * (pos[ib] - pos[ia])
        p2 = pos[ia] + tac * (pos[ic] - pos[ia])
        segments.append((p1, p2))

    return above, below, segments


def chain_loop(segments, tol=5):
    """Chain cut segments into closed loops, keyed on rounded positions."""
    def key(p):
        return (round(p[0], tol), round(p[2], tol))

    adj = {}
    for p1, p2 in segments:
        k1, k2 = key(p1), key(p2)
        if k1 == k2:
            continue
        adj.setdefault(k1, []).append((k2, p2))
        adj.setdefault(k2, []).append((k1, p1))

    loops, seen = [], set()
    for start in adj:
        if start in seen:
            continue
        loop, cur, prev = [], start, None
        pt = {k: None for k in ()}
        while cur is not None and cur not in seen:
            seen.add(cur)
            loop.append(cur)
            nxt = None
            for k, p in adj.get(cur, []):
                if k != prev and k not in seen:
                    nxt = k; break
            prev, cur = cur, nxt
        if len(loop) >= 8:
            loops.append(np.array([[k[0], 0.0, k[1]] for k in loop]))
    loops.sort(key=len, reverse=True)
    return loops


# ---------------------------------------------------------------- cavity caps

def cavity(loop_xz, seam, floor_y, inset=0.90, lip=0.035):
    """Ledge + inner walls + floor built from the rim loop. Returns pos, nrm, tris."""
    c = loop_xz.mean(axis=0)
    rim = loop_xz.copy(); rim[:, 1] = seam
    inner = c + (loop_xz - c) * inset; inner[:, 1] = seam - lip
    floor = c + (loop_xz - c) * inset; floor[:, 1] = floor_y

    n = len(rim)
    P = np.concatenate([rim, inner, floor, [[c[0], floor_y, c[2]]]])
    N = np.zeros_like(P)
    N[:n] = [0, 1, 0]
    for i in range(n):
        radial = inner[i] - np.array([c[0], inner[i][1], c[2]])
        radial[1] = 0
        radial /= np.linalg.norm(radial) + 1e-12
        N[n + i] = -radial
        N[2 * n + i] = -radial
    N[3 * n] = [0, 1, 0]

    T = []
    for i in range(n):
        j = (i + 1) % n
        T += [(i, n + i, n + j), (i, n + j, j)]                    # ledge
        T += [(n + i, 2 * n + i, 2 * n + j), (n + i, 2 * n + j, n + j)]  # walls
        T += [(2 * n + i, 3 * n, 2 * n + j)]                        # floor fan
    return P, N, np.array(T, np.int64)


def flat_cap(loop_xz, y, facing_down=True):
    c = loop_xz.mean(axis=0)
    ring = loop_xz.copy(); ring[:, 1] = y
    P = np.concatenate([ring, [[c[0], y, c[2]]]])
    N = np.tile([0.0, -1.0 if facing_down else 1.0, 0.0], (len(P), 1))
    n = len(ring)
    T = [(i, n, (i + 1) % n) if facing_down else (i, (i + 1) % n, n) for i in range(n)]
    return P, N, np.array(T, np.int64)


def fix_winding(P, N, T):
    """Flip triangles whose geometric normal opposes the intended shading normal."""
    out = []
    for a, b, c in T:
        gn = np.cross(P[b] - P[a], P[c] - P[a])
        if gn @ N[a] < 0:
            out.append((a, c, b))
        else:
            out.append((a, b, c))
    return np.array(out, np.int64)


# ---------------------------------------------------------------- GLB writing

def pad4(b, fill=b'\x00'):
    return b + fill * ((4 - len(b) % 4) % 4)


def write_glb(path, src_js, src_bin, parts):
    """parts: list of dicts {pos, nrm, uv|None, tris, material}."""
    js = {
        'asset': {'version': '2.0', 'generator': 'WheelUp lid-cutter'},
        'scene': 0,
        'scenes': [{'nodes': [0]}],
        'nodes': [{'mesh': 0}],
        'meshes': [{'primitives': []}],
        'materials': [],
        'accessors': [],
        'bufferViews': [],
        'buffers': [],
    }

    blob = bytearray()

    def add_view(data, target=None):
        while len(blob) % 4:
            blob.append(0)
        off = len(blob)
        blob.extend(data)
        bv = {'buffer': 0, 'byteOffset': off, 'byteLength': len(data)}
        if target:
            bv['target'] = target
        js['bufferViews'].append(bv)
        return len(js['bufferViews']) - 1

    def add_accessor(arr, comp, typ, target, minmax=False):
        bv = add_view(arr.tobytes(), target)
        acc = {'bufferView': bv, 'componentType': comp, 'count': len(arr), 'type': typ}
        if minmax:
            acc['min'] = arr.min(axis=0).tolist()
            acc['max'] = arr.max(axis=0).tolist()
        js['accessors'].append(acc)
        return len(js['accessors']) - 1

    # textured material copied from the source file
    src_mat = json.loads(json.dumps(src_js['materials'][0]))
    js['materials'].append(src_mat)
    js['textures'] = json.loads(json.dumps(src_js['textures']))
    js['samplers'] = json.loads(json.dumps(src_js['samplers']))
    js['images'] = []
    for img in src_js['images']:
        data = view_bytes(src_js, src_bin, img['bufferView'])
        bv = add_view(data)
        js['images'].append({'mimeType': img['mimeType'], 'bufferView': bv})

    # dark interior material
    js['materials'].append({
        'name': 'ChestInterior',
        'pbrMetallicRoughness': {
            'baseColorFactor': [0.055, 0.042, 0.032, 1.0],
            'metallicFactor': 0.0,
            'roughnessFactor': 0.95,
        },
        'alphaMode': 'OPAQUE',
        'doubleSided': True,
    })

    for part in parts:
        idx = add_accessor(part['tris'].astype('<u4').ravel(), 5125, 'SCALAR', 34963)
        attrs = {'POSITION': add_accessor(part['pos'].astype('<f4'), 5126, 'VEC3', 34962, minmax=True),
                 'NORMAL': add_accessor(part['nrm'].astype('<f4'), 5126, 'VEC3', 34962)}
        if part.get('uv') is not None:
            attrs['TEXCOORD_0'] = add_accessor(part['uv'].astype('<f4'), 5126, 'VEC2', 34962)
        js['meshes'][0]['primitives'].append(
            {'attributes': attrs, 'indices': idx, 'mode': 4, 'material': part['material']})

    js['buffers'].append({'byteLength': len(blob)})

    json_chunk = pad4(json.dumps(js, separators=(',', ':')).encode('utf-8'), b' ')
    bin_chunk = pad4(bytes(blob))
    total = 12 + 8 + len(json_chunk) + 8 + len(bin_chunk)

    with open(path, 'wb') as fh:
        fh.write(struct.pack('<III', 0x46546C67, 2, total))
        fh.write(struct.pack('<II', len(json_chunk), 0x4E4F534A)); fh.write(json_chunk)
        fh.write(struct.pack('<II', len(bin_chunk), 0x004E4942)); fh.write(bin_chunk)
    return total


# ---------------------------------------------------------------- main recipe

def rot_x(points, angle, pivot):
    c, s = np.cos(angle), np.sin(angle)
    R = np.array([[1, 0, 0], [0, c, -s], [0, s, c]])
    return (points - pivot) @ R.T + pivot


def build_open(src_path, dst_path, seam, angle_deg=105.0, floor_ratio=0.55, verbose=True):
    js, bin_ = load_glb(src_path)
    prim = js['meshes'][0]['primitives'][0]
    pos = read_accessor(js, bin_, prim['attributes']['POSITION'])
    nrm = read_accessor(js, bin_, prim['attributes']['NORMAL'])
    uv = read_accessor(js, bin_, prim['attributes']['TEXCOORD_0'])
    tris = read_accessor(js, bin_, prim['indices']).reshape(-1, 3)

    lid_b, body_b, segments = split_by_plane(pos, nrm, uv, tris, seam)
    lid_p, lid_n, lid_uv, lid_t = lid_b.arrays()
    body_p, body_n, body_uv, body_t = body_b.arrays()

    loops = chain_loop(segments)
    if not loops:
        raise RuntimeError('no cut loop found at y=%.3f' % seam)
    loop = loops[0]

    ymin = pos[:, 1].min()
    floor_y = ymin + (seam - ymin) * (1.0 - floor_ratio)

    cav_p, cav_n, cav_t = cavity(loop, seam, floor_y)
    cav_t = fix_winding(cav_p, cav_n, cav_t)

    cap_p, cap_n, cap_t = flat_cap(loop, seam, facing_down=True)
    cap_t = fix_winding(cap_p, cap_n, cap_t)

    # hinge on the rear edge of the rim
    z_back = loop[:, 2].max()
    pivot = np.array([0.0, seam, z_back])
    # +angle : le bord avant monte et part vers l'arriere (charniere au fond)
    angle = np.radians(abs(angle_deg))

    lid_p = rot_x(lid_p, angle, pivot)
    lid_n = rot_x(lid_n, angle, np.zeros(3))
    cap_p = rot_x(cap_p, angle, pivot)
    cap_n = rot_x(cap_n, angle, np.zeros(3))

    parts = [
        {'pos': body_p, 'nrm': body_n, 'uv': body_uv, 'tris': body_t, 'material': 0},
        {'pos': lid_p, 'nrm': lid_n, 'uv': lid_uv, 'tris': lid_t, 'material': 0},
        {'pos': cav_p, 'nrm': cav_n, 'uv': None, 'tris': cav_t, 'material': 1},
        {'pos': cap_p, 'nrm': cap_n, 'uv': None, 'tris': cap_t, 'material': 1},
    ]
    size = write_glb(dst_path, js, bin_, parts)

    if verbose:
        print(f'  seam y={seam:+.3f} | corps {len(body_t)} tris | couvercle {len(lid_t)} tris '
              f'| rim {len(loop)} pts | {size/1048576:.1f} Mo')
    return parts
