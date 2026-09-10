"""Fabrique les icones 3D de recompense dans Assets/Resources/Rewards/.

    python dev/tools/make_reward_assets.py

Un fichier .glb par lot : pieces, moteur, suspension, pneus, allegement,
peinture. Tout est genere par programme (procmesh), donc rejouable et
modifiable sans logiciel de modelisation.
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import procmesh as pm

OUT = os.path.normpath(os.path.join(
    os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'Resources', 'Rewards'))

# --------------------------------------------------------------------- palette
GOLD = {'name': 'Gold', 'color': (0.95, 0.72, 0.20), 'metallic': 1.0, 'roughness': 0.30}
GOLD_DARK = {'name': 'GoldDark', 'color': (0.72, 0.50, 0.12), 'metallic': 1.0, 'roughness': 0.42}
DARK = {'name': 'DarkMetal', 'color': (0.20, 0.21, 0.245), 'metallic': 0.80, 'roughness': 0.45}
ALLOY = {'name': 'Alloy', 'color': (0.60, 0.63, 0.67), 'metallic': 0.90, 'roughness': 0.32}
CHROME = {'name': 'Chrome', 'color': (0.78, 0.81, 0.85), 'metallic': 1.0, 'roughness': 0.14}
ACCENT = {'name': 'Accent', 'color': (0.96, 0.36, 0.13), 'metallic': 0.10, 'roughness': 0.48}
RUBBER = {'name': 'Rubber', 'color': (0.115, 0.118, 0.128), 'metallic': 0.0, 'roughness': 0.90}
# Teintee a l'execution avec la couleur reellement debloquee : le nom sert de reperage cote Unity.
PAINT_BODY = {'name': 'PaintBody', 'color': (0.88, 0.89, 0.92), 'metallic': 0.25, 'roughness': 0.35}


def build_coins():
    """Pile de pieces legerement decalees, plus deux posees a plat devant."""
    mats = [GOLD, GOLD_DARK]
    parts = []
    rng = np.random.default_rng(4)

    def coin(y, tilt, offset):
        blank = pm.cylinder(0.50, 0.50, 0.11, segments=34, material=0)
        face = pm.cylinder(0.355, 0.355, 0.125, segments=30, material=1)
        group = [blank, face]
        matrix = pm.rot_z(tilt[0]) @ pm.rot_x(tilt[1])
        return [g.transformed(matrix=matrix, translate=(offset[0], y, offset[1])) for g in group]

    for i in range(5):
        parts += coin(-0.28 + i * 0.115,
                      (rng.uniform(-3, 3), rng.uniform(-3, 3)),
                      (rng.uniform(-0.05, 0.05), rng.uniform(-0.05, 0.05)))

    parts += coin(-0.335, (0, 0), (0.62, 0.30))
    parts += coin(-0.335, (0, 0), (-0.55, 0.42))
    # Une piece dressee contre la pile : la silhouette est plus lisible de face.
    leaning = pm.cylinder(0.50, 0.50, 0.11, segments=34, material=0)
    face = pm.cylinder(0.355, 0.355, 0.125, segments=30, material=1)
    tilt = pm.rot_z(84) @ pm.rot_y(18)
    parts += [g.transformed(matrix=tilt, translate=(0.72, -0.10, -0.30)) for g in (leaning, face)]

    return parts, mats


def build_engine():
    """Monocylindre a ailettes : carter, bloc, culasse, echappement."""
    mats = [DARK, ALLOY, ACCENT, CHROME]
    parts = [
        pm.box((0.98, 0.44, 0.64), center=(0, -0.34, 0), material=0),        # carter
        pm.box((0.72, 0.16, 0.50), center=(0, -0.62, 0), material=0),        # carter d'huile
        pm.box((0.58, 0.46, 0.52), center=(0, 0.04, 0), material=0),         # fut du cylindre
    ]

    # Ailettes de refroidissement : la signature visuelle d'un moteur, meme en tout petit.
    for i in range(5):
        parts.append(pm.box((0.74, 0.045, 0.66), center=(0, -0.14 + i * 0.10, 0), material=1))

    parts += [
        pm.box((0.68, 0.15, 0.58), center=(0, 0.35, 0), material=1),         # culasse
        pm.box((0.52, 0.11, 0.46), center=(0, 0.47, 0), material=2),         # cache-culbuteurs
    ]

    # Boulons de culasse
    bolt = pm.cylinder(0.045, 0.045, 0.09, segments=12, material=3)
    for x in (-0.26, 0.26):
        for z in (-0.21, 0.21):
            parts.append(bolt.transformed(translate=(x, 0.55, z)))

    # Echappement : coude vers l'avant puis descente
    path = np.array([
        [0.0, 0.10, 0.30], [0.0, 0.08, 0.46], [0.0, -0.02, 0.60],
        [0.0, -0.20, 0.68], [0.0, -0.42, 0.70], [0.0, -0.52, 0.62],
    ])
    parts.append(pm.tube_along(path, 0.062, seg_around=14, material=3))
    parts.append(pm.cylinder(0.055, 0.055, 0.16, segments=16, material=0)
                 .transformed(matrix=pm.rot_x(90), translate=(0.34, -0.30, 0.34)))

    return parts, mats


def build_suspension():
    """Amortisseur : corps, tige chromee, ressort helicoidal, silentblocs."""
    mats = [DARK, CHROME, ACCENT, ALLOY]
    parts = [
        pm.cylinder(0.185, 0.185, 0.58, segments=26, center=(0, -0.28, 0), material=0),   # corps
        pm.cylinder(0.205, 0.205, 0.07, segments=26, center=(0, 0.00, 0), material=3),    # bague de precharge
        pm.cylinder(0.075, 0.075, 0.52, segments=20, center=(0, 0.34, 0), material=1),    # tige
        pm.cylinder(0.16, 0.16, 0.10, segments=22, center=(0, 0.58, 0), material=3),      # tete
    ]

    # Ressort autour du corps : 6 spires, tube epais pour rester lisible en petit.
    spring = pm.helix(radius=0.30, height=0.74, turns=6.0, points=260)
    parts.append(pm.tube_along(spring, 0.052, seg_around=12, material=2)
                 .transformed(translate=(0, -0.13, 0)))
    parts.append(pm.cylinder(0.33, 0.33, 0.05, segments=26, center=(0, -0.52, 0), material=3))
    parts.append(pm.cylinder(0.33, 0.33, 0.05, segments=26, center=(0, 0.26, 0), material=3))

    # Silentblocs haut et bas
    for y in (0.68, -0.62):
        parts.append(pm.torus(0.10, 0.045, seg_major=24, seg_minor=10, material=3)
                     .transformed(matrix=pm.rot_x(90), translate=(0, y, 0)))

    return parts, mats


def build_tire():
    """Roue complete : pneu sculpte, jante a batons, moyeu, disque de frein."""
    mats = [RUBBER, ALLOY, DARK, CHROME]
    parts = [
        pm.torus(0.66, 0.25, seg_major=48, seg_minor=18, material=0),                  # pneu
        # Jante ouverte : un cylindre plein masquerait les batons, qui font tout le caractere.
        pm.cylinder(0.47, 0.47, 0.30, segments=40, caps=False, material=1),            # fond de jante
        pm.torus(0.485, 0.035, seg_major=40, seg_minor=10, material=1)
          .transformed(translate=(0, 0.15, 0)),                                        # rebords
        pm.torus(0.485, 0.035, seg_major=40, seg_minor=10, material=1)
          .transformed(translate=(0, -0.15, 0)),
        pm.cylinder(0.155, 0.155, 0.38, segments=22, material=2),                      # moyeu
        pm.cylinder(0.085, 0.085, 0.44, segments=16, material=3),                      # axe
    ]

    # Pavés de gomme : le pneu se lit comme un pneu, pas comme un anneau lisse.
    block = pm.box((0.10, 0.07, 0.30), material=0)
    for i in range(26):
        deg = i * 360.0 / 26
        offset = np.array([0.885, 0.0, 0.0]) @ pm.rot_y(deg).T
        z = 0.10 * (1 if i % 2 else -1)
        parts.append(block.transformed(matrix=pm.rot_y(deg), translate=offset + np.array([0, z, 0])))

    # Batons de jante, visibles au travers de la jante ouverte
    spoke = pm.box((0.36, 0.10, 0.13), material=1)
    parts += pm.ring_of(spoke, 5, 0.30, axis='y', start_deg=18)

    # Disque de frein cote oppose a la camera : devant, il masquerait les batons de jante.
    parts.append(pm.cylinder(0.29, 0.29, 0.035, segments=32, center=(0, -0.24, 0), material=3))

    # La roue se dresse : construite a plat autour de l'axe Y, basculee dans le plan de l'ecran.
    return [p.transformed(matrix=pm.rot_x(90)) for p in parts], mats


def build_weight():
    """Cadre treillis : l'image même de l'allègement sur une moto."""
    mats = [ALLOY, ACCENT, CHROME]

    def bar(a, b, r=0.05, material=0):
        return pm.tube_along(np.array([a, b], np.float64), r, seg_around=10, material=material)

    x, y, z = 0.62, 0.34, 0.16
    corners = {
        'ftl': (-x, y, z), 'ftr': (x, y, z), 'fbl': (-x, -y, z), 'fbr': (x, -y, z),
        'btl': (-x, y, -z), 'btr': (x, y, -z), 'bbl': (-x, -y, -z), 'bbr': (x, -y, -z),
    }
    edges = [
        ('ftl', 'ftr'), ('fbl', 'fbr'), ('ftl', 'fbl'), ('ftr', 'fbr'),
        ('btl', 'btr'), ('bbl', 'bbr'), ('btl', 'bbl'), ('btr', 'bbr'),
        ('ftl', 'btl'), ('ftr', 'btr'), ('fbl', 'bbl'), ('fbr', 'bbr'),
    ]
    parts = [bar(corners[a], corners[b]) for a, b in edges]

    # Croisillons : ce sont eux qui font lire "treillis" plutot que "caisse".
    for zz in (z, -z):
        parts.append(bar((-x, y, zz), (0.0, -y, zz), r=0.042, material=1))
        parts.append(bar((0.0, y, zz), (x, -y, zz), r=0.042, material=1))
        parts.append(bar((0.0, -y, zz), (x, y, zz), r=0.042, material=1))
        parts.append(bar((-x, -y, zz), (0.0, y, zz), r=0.042, material=1))

    for corner in corners.values():
        parts.append(pm.sphere(0.068, seg_u=16, seg_v=10, center=corner, material=2))

    return parts, mats


def build_paint():
    """Bombe de peinture. Le corps porte le materiau PaintBody, teinte a l'execution."""
    mats = [PAINT_BODY, DARK, CHROME, ACCENT]
    parts = [
        pm.cylinder(0.30, 0.30, 0.78, segments=32, center=(0, -0.10, 0), material=0),   # corps
        pm.cylinder(0.305, 0.305, 0.20, segments=32, center=(0, -0.18, 0), material=1),  # bandeau
        pm.cylinder(0.30, 0.17, 0.14, segments=32, center=(0, 0.36, 0), material=0),     # epaulement
        pm.cylinder(0.155, 0.155, 0.07, segments=22, center=(0, 0.46, 0), material=2),   # col
        pm.cylinder(0.19, 0.19, 0.20, segments=26, center=(0, 0.59, 0), material=1),     # capuchon
        pm.box((0.10, 0.07, 0.13), center=(0.0, 0.63, 0.15), material=3),                # diffuseur
        pm.torus(0.29, 0.035, seg_major=32, seg_minor=10, material=2)
          .transformed(translate=(0, -0.48, 0)),                                          # sertissage bas
        pm.torus(0.29, 0.030, seg_major=32, seg_minor=10, material=2)
          .transformed(translate=(0, 0.28, 0)),
    ]
    return parts, mats


BUILDERS = {
    'coins': build_coins,
    'engine': build_engine,
    'suspension': build_suspension,
    'tires': build_tire,
    'weight': build_weight,
    'paint': build_paint,
}


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, builder in BUILDERS.items():
        parts, mats = builder()
        parts = pm.normalize(parts, target_height=1.0)
        path = os.path.join(OUT, name + '.glb')
        verts, tris, size = pm.save_glb(path, parts, mats)
        print(f'  {name:11s} {verts:6d} sommets  {tris:6d} tris  {len(mats)} materiaux  {size/1024:6.0f} Ko')


if __name__ == '__main__':
    main()
