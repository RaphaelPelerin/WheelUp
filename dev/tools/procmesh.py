"""Petite bibliotheque de maillages procedureaux + export GLB.

Assez pour fabriquer des icones 3D d'inventaire (piece, moteur, suspension,
roue...) sans passer par un modeleur. Les normales sont analytiques, pas
recalculees depuis les faces : les aretes restent franches et les surfaces
rondes restent lisses.

Aucune UV n'est produite : les materiaux sont des PBR unis (couleur, metal,
rugosite), ce que glTF accepte sans TEXCOORD_0.
"""
import json
import struct

import numpy as np

TAU = np.pi * 2.0


# --------------------------------------------------------------------- maillage

class Mesh:
    def __init__(self, pos, nrm, tris, material=0):
        self.pos = np.asarray(pos, np.float64).reshape(-1, 3)
        self.nrm = np.asarray(nrm, np.float64).reshape(-1, 3)
        self.tris = np.asarray(tris, np.int64).reshape(-1, 3)
        self.material = material

    def copy(self):
        return Mesh(self.pos.copy(), self.nrm.copy(), self.tris.copy(), self.material)

    def transformed(self, matrix=None, translate=None, scale=None, material=None):
        m = self.copy()
        if scale is not None:
            s = np.asarray(scale, np.float64)
            s = np.repeat(s, 3) if s.ndim == 0 else s
            m.pos *= s
            # Une mise a l'echelle non uniforme deforme les normales dans l'autre sens.
            m.nrm = m.nrm / s
            m.nrm /= np.linalg.norm(m.nrm, axis=1, keepdims=True) + 1e-12
        if matrix is not None:
            m.pos = m.pos @ matrix.T
            m.nrm = m.nrm @ matrix.T
            m.nrm /= np.linalg.norm(m.nrm, axis=1, keepdims=True) + 1e-12
        if translate is not None:
            m.pos = m.pos + np.asarray(translate, np.float64)
        if material is not None:
            m.material = material
        return m


def rot_x(deg):
    c, s = np.cos(np.radians(deg)), np.sin(np.radians(deg))
    return np.array([[1, 0, 0], [0, c, -s], [0, s, c]])


def rot_y(deg):
    c, s = np.cos(np.radians(deg)), np.sin(np.radians(deg))
    return np.array([[c, 0, s], [0, 1, 0], [-s, 0, c]])


def rot_z(deg):
    c, s = np.cos(np.radians(deg)), np.sin(np.radians(deg))
    return np.array([[c, -s, 0], [s, c, 0], [0, 0, 1]])


# ------------------------------------------------------------------- primitives

def box(size, center=(0, 0, 0), material=0):
    """Pave a aretes franches : chaque face a ses propres sommets."""
    sx, sy, sz = np.asarray(size, np.float64) / 2.0
    cx, cy, cz = center
    faces = [
        ((1, 0, 0), [(1, -1, -1), (1, 1, -1), (1, 1, 1), (1, -1, 1)]),
        ((-1, 0, 0), [(-1, -1, 1), (-1, 1, 1), (-1, 1, -1), (-1, -1, -1)]),
        ((0, 1, 0), [(-1, 1, -1), (-1, 1, 1), (1, 1, 1), (1, 1, -1)]),
        ((0, -1, 0), [(-1, -1, 1), (-1, -1, -1), (1, -1, -1), (1, -1, 1)]),
        ((0, 0, 1), [(-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)]),
        ((0, 0, -1), [(1, -1, -1), (-1, -1, -1), (-1, 1, -1), (1, 1, -1)]),
    ]
    P, N, T = [], [], []
    for normal, corners in faces:
        base = len(P)
        for x, y, z in corners:
            P.append((cx + x * sx, cy + y * sy, cz + z * sz))
            N.append(normal)
        T += [(base, base + 1, base + 2), (base, base + 2, base + 3)]
    return Mesh(P, N, T, material)


def cylinder(r_bottom, r_top, height, segments=28, caps=True, center=(0, 0, 0), material=0):
    """Cylindre ou tronc de cone, axe Y. Cotes lisses, capuchons a arete franche."""
    P, N, T = [], [], []
    y0, y1 = -height / 2.0, height / 2.0
    slope = np.arctan2(r_bottom - r_top, height)
    cs, sn = np.cos(slope), np.sin(slope)

    for i in range(segments + 1):
        a = i / segments * TAU
        ca, sa = np.cos(a), np.sin(a)
        n = (ca * cs, sn, sa * cs)
        P.append((ca * r_bottom, y0, sa * r_bottom)); N.append(n)
        P.append((ca * r_top, y1, sa * r_top)); N.append(n)

    for i in range(segments):
        b = i * 2
        T += [(b, b + 1, b + 3), (b, b + 3, b + 2)]

    if caps:
        for r, y, ny, flip in ((r_bottom, y0, -1.0, True), (r_top, y1, 1.0, False)):
            if r <= 1e-9:
                continue
            base = len(P)
            P.append((0, y, 0)); N.append((0, ny, 0))
            for i in range(segments + 1):
                a = i / segments * TAU
                P.append((np.cos(a) * r, y, np.sin(a) * r)); N.append((0, ny, 0))
            for i in range(segments):
                t = (base, base + 1 + i, base + 2 + i)
                T.append(t[::-1] if flip else t)

    m = Mesh(P, N, T, material)
    return m.transformed(translate=center) if any(center) else m


def torus(major, minor, seg_major=40, seg_minor=16, center=(0, 0, 0), material=0):
    """Tore dans le plan XZ, axe Y."""
    P, N, T = [], [], []
    for i in range(seg_major + 1):
        u = i / seg_major * TAU
        cu, su = np.cos(u), np.sin(u)
        for j in range(seg_minor + 1):
            v = j / seg_minor * TAU
            cv, sv = np.cos(v), np.sin(v)
            P.append(((major + minor * cv) * cu, minor * sv, (major + minor * cv) * su))
            N.append((cv * cu, sv, cv * su))
    stride = seg_minor + 1
    for i in range(seg_major):
        for j in range(seg_minor):
            a = i * stride + j
            T += [(a, a + 1, a + stride + 1), (a, a + stride + 1, a + stride)]
    m = Mesh(P, N, T, material)
    return m.transformed(translate=center) if any(center) else m


def tube_along(path, tube_r, seg_around=12, closed=False, material=0):
    """Tube balaye le long d'une polyligne 3D (ressorts, arceaux, durites)."""
    path = np.asarray(path, np.float64)
    n = len(path)

    tangents = np.zeros_like(path)
    tangents[1:-1] = path[2:] - path[:-2]
    tangents[0] = path[1] - path[0]
    tangents[-1] = path[-1] - path[-2]
    tangents /= np.linalg.norm(tangents, axis=1, keepdims=True) + 1e-12

    # Repere parallele transporte : evite la vrille d'un repere de Frenet naif.
    up = np.array([0.0, 0.0, 1.0])
    if abs(tangents[0] @ up) > 0.9:
        up = np.array([1.0, 0.0, 0.0])
    normals = np.zeros_like(path)
    normals[0] = np.cross(tangents[0], up)
    normals[0] /= np.linalg.norm(normals[0]) + 1e-12
    for i in range(1, n):
        v = normals[i - 1] - tangents[i] * (normals[i - 1] @ tangents[i])
        normals[i] = v / (np.linalg.norm(v) + 1e-12)
    binormals = np.cross(tangents, normals)

    P, N, T = [], [], []
    for i in range(n):
        for j in range(seg_around + 1):
            a = j / seg_around * TAU
            d = np.cos(a) * normals[i] + np.sin(a) * binormals[i]
            P.append(path[i] + d * tube_r)
            N.append(d)
    stride = seg_around + 1
    rings = n if closed else n - 1
    for i in range(rings):
        i2 = (i + 1) % n
        for j in range(seg_around):
            a = i * stride + j
            b = i2 * stride + j
            T += [(a, a + 1, b + 1), (a, b + 1, b)]
    return Mesh(P, N, T, material)


def helix(radius, height, turns, points=180, phase=0.0):
    t = np.linspace(0.0, 1.0, points)
    a = t * turns * TAU + phase
    return np.stack([np.cos(a) * radius, (t - 0.5) * height, np.sin(a) * radius], axis=1)


def sphere(radius, seg_u=32, seg_v=18, center=(0, 0, 0), material=0):
    P, N, T = [], [], []
    for j in range(seg_v + 1):
        v = j / seg_v * np.pi
        for i in range(seg_u + 1):
            u = i / seg_u * TAU
            d = (np.sin(v) * np.cos(u), np.cos(v), np.sin(v) * np.sin(u))
            P.append((d[0] * radius, d[1] * radius, d[2] * radius))
            N.append(d)
    stride = seg_u + 1
    for j in range(seg_v):
        for i in range(seg_u):
            a = j * stride + i
            T += [(a, a + stride, a + stride + 1), (a, a + stride + 1, a + 1)]
    m = Mesh(P, N, T, material)
    return m.transformed(translate=center) if any(center) else m


def ring_of(mesh, count, radius, axis='y', start_deg=0.0, spin=True):
    """Repete un maillage en couronne (rayons de jante, boulons, ailettes)."""
    out = []
    rot = {'y': rot_y, 'x': rot_x, 'z': rot_z}[axis]
    for i in range(count):
        deg = start_deg + i * 360.0 / count
        offset = np.array([radius, 0.0, 0.0]) @ rot(deg).T
        out.append(mesh.transformed(matrix=rot(deg) if spin else None, translate=offset))
    return out


# ------------------------------------------------------------------ export GLB

def _pad4(b, fill=b'\x00'):
    return b + fill * ((4 - len(b) % 4) % 4)


def save_glb(path, meshes, materials):
    """materials : liste de dicts {name, color:(r,g,b), metallic, roughness, emissive?}."""
    js = {
        'asset': {'version': '2.0', 'generator': 'WheelUp procmesh'},
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

    def add_view(data, target):
        while len(blob) % 4:
            blob.append(0)
        off = len(blob)
        blob.extend(data)
        js['bufferViews'].append({'buffer': 0, 'byteOffset': off, 'byteLength': len(data), 'target': target})
        return len(js['bufferViews']) - 1

    def add_accessor(arr, comp, typ, target, minmax=False):
        bv = add_view(arr.tobytes(), target)
        acc = {'bufferView': bv, 'componentType': comp, 'count': len(arr), 'type': typ}
        if minmax:
            acc['min'] = arr.min(axis=0).tolist()
            acc['max'] = arr.max(axis=0).tolist()
        js['accessors'].append(acc)
        return len(js['accessors']) - 1

    for mat in materials:
        entry = {
            'name': mat.get('name', 'Material'),
            'pbrMetallicRoughness': {
                'baseColorFactor': list(mat['color']) + [1.0],
                'metallicFactor': float(mat.get('metallic', 0.0)),
                'roughnessFactor': float(mat.get('roughness', 0.6)),
            },
            'alphaMode': 'OPAQUE',
        }
        if mat.get('emissive'):
            entry['emissiveFactor'] = list(mat['emissive'])
        js['materials'].append(entry)

    # Une primitive par materiau : Unity en fait autant de sous-maillages.
    by_material = {}
    for mesh in meshes:
        by_material.setdefault(mesh.material, []).append(mesh)

    for material in sorted(by_material):
        group = by_material[material]
        offset = 0
        pos, nrm, tris = [], [], []
        for m in group:
            pos.append(m.pos)
            nrm.append(m.nrm)
            tris.append(m.tris + offset)
            offset += len(m.pos)
        pos = np.concatenate(pos).astype('<f4')
        nrm = np.concatenate(nrm).astype('<f4')
        tris = np.concatenate(tris).astype('<u4')

        idx = add_accessor(tris.ravel(), 5125, 'SCALAR', 34963)
        attrs = {
            'POSITION': add_accessor(pos, 5126, 'VEC3', 34962, minmax=True),
            'NORMAL': add_accessor(nrm, 5126, 'VEC3', 34962),
        }
        js['meshes'][0]['primitives'].append(
            {'attributes': attrs, 'indices': idx, 'mode': 4, 'material': material})

    js['buffers'].append({'byteLength': len(blob)})

    json_chunk = _pad4(json.dumps(js, separators=(',', ':')).encode('utf-8'), b' ')
    bin_chunk = _pad4(bytes(blob))
    total = 12 + 8 + len(json_chunk) + 8 + len(bin_chunk)
    with open(path, 'wb') as fh:
        fh.write(struct.pack('<III', 0x46546C67, 2, total))
        fh.write(struct.pack('<II', len(json_chunk), 0x4E4F534A)); fh.write(json_chunk)
        fh.write(struct.pack('<II', len(bin_chunk), 0x004E4942)); fh.write(bin_chunk)

    verts = sum(len(m.pos) for m in meshes)
    tri_count = sum(len(m.tris) for m in meshes)
    return verts, tri_count, total


def normalize(meshes, target_height=1.0):
    """Recentre l'ensemble sur l'origine et le met a l'echelle demandee."""
    allp = np.concatenate([m.pos for m in meshes])
    lo, hi = allp.min(axis=0), allp.max(axis=0)
    center = (lo + hi) / 2.0
    scale = target_height / max(hi[1] - lo[1], 1e-9)
    return [m.transformed(translate=-center).transformed(scale=np.array([scale] * 3)) for m in meshes]
