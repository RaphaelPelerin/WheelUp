"""Fabrique la piece de marque dans Assets/Resources/Rewards/coin_logo_proc.glb.

    python dev/tools/make_coin_logo.py

Contrairement aux autres recompenses, celle-ci porte le logo de l'application. Le relief n'est pas
decrit a une IA generative : il est *decoupe dans le PNG de marque*, donc c'est la silhouette exacte
du logo qui ressort en geometrie, pas une interpretation.

Pourquoi pas une texture plaquee : procmesh ne produit aucune UV (voir son en-tete), les materiaux
sont des PBR unis. Extruder la silhouette contourne le probleme et donne un vrai relief frappe,
qui capte la lumiere comme une piece gravee plutot que comme un autocollant.

La piece est construite a plat (axe Y, comme cylinder) puis redressee face a la camera.
"""
import os
import sys

import numpy as np
from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import procmesh as pm

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.normpath(os.path.join(HERE, '..', '..'))
SOURCE = os.path.join(ROOT, 'Assets', 'Branding', 'WheelUpAppIcon.png')
# Volontairement distinct de coin_logo.glb, qui est la version Meshy retenue par le jeu :
# ce script servait de source et de repli, il ne doit plus ecraser l'asset en service.
OUT = os.path.join(ROOT, 'Assets', 'Resources', 'Rewards', 'coin_logo_proc.glb')

# --------------------------------------------------------------------- palette
# Reprise mot pour mot de make_reward_assets.py : la piece doit appartenir a la meme famille.
GOLD = {'name': 'Gold', 'color': (0.95, 0.72, 0.20), 'metallic': 1.0, 'roughness': 0.30}
GOLD_DARK = {'name': 'GoldDark', 'color': (0.72, 0.50, 0.12), 'metallic': 1.0, 'roughness': 0.42}
# L'emblème est du meme or que la piece : une piece frappee ne joue pas sur la couleur, la
# silhouette se lit par son relief. Seule la finition change — plus poli que le champ, donc plus
# clair sous la lumiere et plus sombre dans l'ombre, ce qui suffit a la detacher.
LOGO_RELIEF = {'name': 'LogoRelief', 'color': (0.98, 0.78, 0.28), 'metallic': 1.0, 'roughness': 0.18}

MAT_GOLD, MAT_GOLD_DARK, MAT_RELIEF = 0, 1, 2

# ----------------------------------------------------------------- proportions
COIN_R = 0.50           # rayon hors tout
COIN_H = 0.13           # epaisseur
FIELD_R = 0.415         # disque creuse qui recoit le relief
FIELD_H = 0.116         # plus mince que la piece : c'est ce qui creuse le champ sous le rebord
RELIEF_H = 0.042        # hauteur du logo au-dessus du champ
GRID = 160              # resolution de la grille de relief ; au-dela le GLB gonfle pour rien

# Nombre de cases sur lesquelles le relief remonte depuis son bord. C'est ce qui remplace les
# parois verticales par un biseau : une paroi droite ne renvoie aucune lumiere et le motif se lit
# comme un aplat decoupe, la pente lui donne l'arete brillante d'une frappe.
BEVEL_CELLS = 3

REED_COUNT = 88         # stries de la tranche
RIM_MINOR = 0.020       # boudin du rebord qui encadre le champ
# Part du rayon du champ que doit atteindre la case remplie la plus excentree. L'echelle est
# deduite de cette mesure plutot que du cadre du logo : un cadre carre gaspille ses coins, qui sont
# vides, et l'emblème se retrouve deux fois trop petit au milieu d'un champ nu.
COVERAGE = 0.90


def load_logo_mask(path, grid):
    """Decoupe le PNG de marque en deux masques booleens (blanc, rouge) sur une grille carree.

    Le fond de l'icone est un noir quasi uni : tout ce qui s'en detache appartient au logo. La
    distinction blanc/rouge se fait sur la dominante du canal rouge, pas sur la teinte exacte, pour
    rester insensible a l'antialiasing des bords.
    """
    image = Image.open(path).convert('RGB')
    rgb = np.asarray(image, np.float64) / 255.0
    r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]

    luma = 0.2126 * r + 0.7152 * g + 0.0722 * b
    is_red = (r > 0.30) & (r > g * 1.7) & (r > b * 1.7)
    mark = (luma > 0.32) | is_red

    if not mark.any():
        sys.exit(f"Aucun logo detecte dans {path} : le fond n'est pas celui attendu.")

    # Recadre sur le logo avant de reechantillonner : sinon la marge noire mange la resolution.
    rows, cols = np.where(mark)
    top, bottom = rows.min(), rows.max() + 1
    left, right = cols.min(), cols.max() + 1
    side = max(bottom - top, right - left)

    # Carre centre sur le logo, pour ne pas deformer ses proportions.
    cy, cx = (top + bottom) // 2, (left + right) // 2
    half = side // 2 + 1
    pad = ((max(0, half - cy), max(0, cy + half - mark.shape[0])),
           (max(0, half - cx), max(0, cx + half - mark.shape[1])))
    padded = np.pad(rgb, pad + ((0, 0),), mode='constant')
    cy += pad[0][0]
    cx += pad[1][0]
    crop = padded[cy - half:cy + half, cx - half:cx + half]

    small = np.asarray(
        Image.fromarray((crop * 255).astype(np.uint8)).resize((grid, grid), Image.LANCZOS),
        np.float64) / 255.0
    sr, sg, sb = small[..., 0], small[..., 1], small[..., 2]
    slum = 0.2126 * sr + 0.7152 * sg + 0.0722 * sb
    # Seuils plus laches qu'a pleine resolution : le reechantillonnage melange le rouge au fond noir
    # et l'assombrit, si bien que la fleche disparait des qu'on lui demande la meme saturation.
    small_red = (sr > 0.17) & (sr > sg * 1.4) & (sr > sb * 1.4)
    small_mark = (slum > 0.32) | small_red

    # Aucun retournement ici : la grille part du fond vers l'avant, mais le redressement final
    # (rot_x) renvoie cet axe vers le bas de l'ecran. Les deux s'annulent, et l'image se retrouve
    # dans le bon sens. Retourner le masque en plus mettrait la moto sur le dos.
    return small_mark & ~small_red, small_mark & small_red


def bevel_profile(mask, levels):
    """Hauteur relative (0 a 1) de chaque case, croissante depuis le bord du motif.

    Obtenue par erosions successives : une case qui survit a k erosions se trouve a k cases du bord.
    La racine carree redresse la pente pres du bord et l'adoucit pres du sommet, ce qui donne
    l'arete franche d'une frappe plutot qu'un talus regulier.
    """
    distance = np.zeros(mask.shape, np.float64)
    current = mask.copy()

    for _ in range(levels):
        distance[current] += 1.0
        shrunk = current.copy()
        shrunk[1:, :] &= current[:-1, :]
        shrunk[:-1, :] &= current[1:, :]
        shrunk[:, 1:] &= current[:, :-1]
        shrunk[:, :-1] &= current[:, 1:]
        # Le pourtour de la grille compte comme vide : sans cela un motif qui l'atteint garderait
        # un mur droit au lieu de redescendre.
        shrunk[0, :] = shrunk[-1, :] = False
        shrunk[:, 0] = shrunk[:, -1] = False
        current = shrunk

    ramp = np.sqrt(distance / levels)

    # Les erosions ne donnent que quelques paliers entiers, ce qui grene le dessus du relief sur les
    # traits fins — la ou chaque case est proche d'un bord. Deux passes de moyenne les fondent en
    # une pente continue ; le masque est reapplique ensuite, sinon le flou deborderait sur le champ.
    for _ in range(2):
        padded = np.pad(ramp, 1, mode='edge')
        ramp = (padded[:-2, 1:-1] + padded[2:, 1:-1] +
                padded[1:-1, :-2] + padded[1:-1, 2:] + 4.0 * ramp) / 8.0
    ramp[~mask] = 0.0

    return ramp


def relief_mesh(mask, heights, material, cell, y_base, radius):
    """Extrude une carte de hauteurs en relief biseaute.

    Une paroi n'est posee que vers une case *plus basse*, et seulement jusqu'a sa hauteur. C'est ce
    qui fabrique le biseau : les marches internes du profil deviennent de courtes facettes inclinees,
    et seul le contour du motif descend jusqu'au champ.
    """
    grid = mask.shape[0]
    origin = -grid * cell / 2.0

    P, N, T = [], [], []

    def quad(a, b, c, d, normal):
        base = len(P)
        P.extend((a, b, c, d))
        N.extend([normal] * 4)
        T.extend(((base, base + 1, base + 2), (base, base + 2, base + 3)))

    for row in range(grid):
        for col in range(grid):
            if not mask[row, col]:
                continue

            height = heights[row, col]
            if height <= 0.0:
                continue

            x0 = origin + col * cell
            z0 = origin + row * cell
            x1, z1 = x0 + cell, z0 + cell

            # Hors du champ creuse, le relief deborderait sur le rebord de la piece.
            if np.hypot((x0 + x1) / 2.0, (z0 + z1) / 2.0) > radius:
                continue

            y_top = y_base + height
            quad((x0, y_top, z0), (x1, y_top, z0), (x1, y_top, z1), (x0, y_top, z1), (0, 1, 0))

            for dr, dc, normal in ((-1, 0, (0, 0, -1)), (1, 0, (0, 0, 1)),
                                   (0, -1, (-1, 0, 0)), (0, 1, (1, 0, 0))):
                nr, nc = row + dr, col + dc
                inside = 0 <= nr < grid and 0 <= nc < grid
                neighbour = heights[nr, nc] if inside else 0.0
                if neighbour >= height:
                    continue

                y_low = y_base + neighbour
                if dr == -1:
                    wall = ((x0, y_low, z0), (x0, y_top, z0), (x1, y_top, z0), (x1, y_low, z0))
                elif dr == 1:
                    wall = ((x1, y_low, z1), (x1, y_top, z1), (x0, y_top, z1), (x0, y_low, z1))
                elif dc == -1:
                    wall = ((x0, y_low, z1), (x0, y_top, z1), (x0, y_top, z0), (x0, y_low, z0))
                else:
                    wall = ((x1, y_low, z0), (x1, y_top, z0), (x1, y_top, z1), (x1, y_low, z1))
                quad(*wall, normal)

    if not T:
        return None
    return pm.Mesh(P, N, T, material)


def build():
    white, red = load_logo_mask(SOURCE, GRID)
    filled = int(white.sum() + red.sum())
    print(f"logo : {filled} cases sur {GRID * GRID} ({filled / (GRID * GRID) * 100:.0f}% du carre)")

    parts = [
        # Le corps, puis le champ plus mince : la difference d'epaisseur creuse le centre et laisse
        # le pourtour former un rebord, sans avoir a modeliser ce rebord separement.
        pm.cylinder(COIN_R, COIN_R, COIN_H, segments=96, material=MAT_GOLD),
        pm.cylinder(FIELD_R, FIELD_R, FIELD_H, segments=80, material=MAT_GOLD_DARK),
        # Boudin pose dans la marche entre le champ et le corps : il encadre l'emblème et rattrape
        # l'arete vive que laissaient deux cylindres empiles.
        pm.torus(FIELD_R + 0.008, RIM_MINOR, seg_major=96, seg_minor=12,
                 center=(0, FIELD_H / 2.0 - 0.006, 0), material=MAT_GOLD),
    ]

    # Stries de tranche. Une piece a tranche lisse se lit comme un jeton ; ce sont elles qui
    # accrochent la lumiere sur le pourtour et donnent l'echelle.
    reed = pm.box((0.022, COIN_H * 0.84, 0.017), material=MAT_GOLD)
    parts.extend(pm.ring_of(reed, REED_COUNT, COIN_R))

    y_field = FIELD_H / 2.0

    # Le biseau se calcule sur le motif entier, pas sur chaque couleur : sinon une rainure
    # apparaitrait le long de la frontiere entre la moto blanche et la fleche rouge, qui sont
    # pourtant un seul relief.
    combined = white | red
    heights = bevel_profile(combined, BEVEL_CELLS) * RELIEF_H

    # Echelle calee sur la case remplie la plus lointaine : le motif touche alors le bord du champ
    # quelle que soit sa forme, au lieu d'etre bride par un cadre carre dont les coins sont vides.
    rows, cols = np.where(combined)
    middle = (GRID - 1) / 2.0
    reach = float(np.hypot(cols - middle, rows - middle).max()) + 0.75
    cell = (FIELD_R * COVERAGE) / reach
    print(f"echelle : {cell * reach / FIELD_R * 100:.0f}% du rayon du champ")

    # Hors du champ, la hauteur retombe a zero : c'est ce qui ferme le relief par une paroi au lieu
    # de le laisser ouvert la ou il touche le bord du disque.
    origin = -GRID * cell / 2.0
    axis = origin + (np.arange(GRID) + 0.5) * cell
    xx, zz = np.meshgrid(axis, axis)
    heights[np.hypot(xx, zz) > FIELD_R * 0.94] = 0.0

    # Les deux masques partagent desormais le meme or : ils restent distincts a la lecture du PNG,
    # mais fusionnent en un seul relief, exactement comme une matrice de frappe les rendrait.
    mesh = relief_mesh(white | red, heights, MAT_RELIEF, cell, y_field, FIELD_R * 0.94)
    if mesh is not None:
        parts.append(mesh)

    # Construite a plat, la piece est redressee pour faire face a la camera des apercus.
    upright = [part.transformed(matrix=pm.rot_x(90)) for part in parts]
    pm.normalize(upright, target_height=1.0)
    return upright


def main():
    if not os.path.exists(SOURCE):
        sys.exit(f"Logo introuvable : {SOURCE}")

    meshes = build()
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    pm.save_glb(OUT, meshes, [GOLD, GOLD_DARK, LOGO_RELIEF])

    triangles = sum(len(m.tris) for m in meshes)
    size = os.path.getsize(OUT) / 1024.0
    print(f"ecrit : {os.path.relpath(OUT, ROOT)} ({triangles} triangles, {size:.0f} Ko)")


if __name__ == '__main__':
    main()
