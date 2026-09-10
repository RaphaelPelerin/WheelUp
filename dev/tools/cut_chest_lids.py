"""Regenere les coffres ouverts a partir des coffres fermes.

    python dev/tools/cut_chest_lids.py [seam] [angle]

seam  : hauteur du plan de coupe dans le repere du modele (defaut 0.42, le
        joint peint sur la texture, reperable a la levre orientee vers le bas)
angle : ouverture du couvercle en degres (defaut 105)

Les fichiers chest_<tier>_open.glb sont ecrases sur place ; leurs .meta sont
conserves, donc les GUID Unity ne changent pas.
"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import openchest as oc

CHESTS = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..',
                      'Assets', 'Resources', 'Chests')

seam = float(sys.argv[1]) if len(sys.argv) > 1 else 0.42
angle = float(sys.argv[2]) if len(sys.argv) > 2 else 105.0

for tier in ('bronze', 'argent', 'or'):
    src = os.path.normpath(os.path.join(CHESTS, f'chest_{tier}_closed.glb'))
    dst = os.path.normpath(os.path.join(CHESTS, f'chest_{tier}_open.glb'))
    print(tier)
    oc.build_open(src, dst, seam=seam, angle_deg=angle)
