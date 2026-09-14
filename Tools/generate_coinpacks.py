#!/usr/bin/env python3
"""
Génère les trois visuels des lots de pièces de la boutique via l'API Meshy et les range là où le
jeu les attend (Assets/Resources/Shop/<id>.glb).

Pourquoi trois modèles et non un : le catalogue vend « Poignée de pièces », « Sacoche de pièces »
et « Coffre-fort », donc trois objets distincts — et la boutique affichait la même pile de pièces
pour les trois. Un joueur qui compare un lot à 1,99 € et un lot à 17,99 € voyait exactement la même
image. L'écart de valeur doit se lire sans avoir à comparer les chiffres.

La plomberie est celle de generate_rewards.py, importée plutôt que recopiée : c'est déjà la
deuxième copie dans Tools/, une troisième aurait fait diverger les trois au premier correctif.
STYLE et FINISH viennent de là aussi, et c'est voulu — les lots doivent appartenir à la même
famille visuelle que les icônes de récompense et que la pièce de marque.

Les identifiants de prévisualisation sont mémorisés dans Tools/.meshy_coinpacks.json : relancer le
script réutilise le maillage déjà payé au lieu d'en générer un nouveau.

La clé est lue dans MESHY_API_KEY, jamais passée en argument ni affichée.

Exemples :
    python Tools/generate_coinpacks.py                     # les trois lots
    python Tools/generate_coinpacks.py --only coins_large  # un seul
    python Tools/generate_coinpacks.py --force             # régénère les GLB existants
"""

import argparse
import json
import os
import sys
from pathlib import Path

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import generate_rewards as rewards

ROOT = Path(__file__).resolve().parent.parent
OUTPUT_DIR = ROOT / "Assets" / "Resources" / "Shop"
STATE_FILE = Path(__file__).resolve().parent / ".meshy_coinpacks.json"

# Les identifiants sont ceux de CoinPackCatalog : le chemin du modèle en découle directement, et
# renommer un lot d'un côté sans l'autre se verrait tout de suite.
SHAPE_PROMPTS = {
    "coins_small": (
        "A small loose handful of about eight thick round gold coins in a low scattered heap, most "
        "lying flat, two coins leaning upright against the heap, each coin with a raised rim and a "
        "simple embossed chevron emblem on its face, coins clearly separated from each other"
    ),
    "coins_medium": (
        "An open leather drawstring pouch standing upright, its mouth folded open wide, thick round "
        "gold coins spilling out of the opening and piling around its base, rounded full pouch body, "
        "a drawstring cord tied around the neck"
    ),
    "coins_large": (
        "A small heavy strongbox safe standing closed, thick rectangular steel body, one round "
        "combination dial centered on the front door, sturdy reinforced corners, a large mound of "
        "thick round gold coins heaped against its front and spilling out to both sides"
    ),
}

TEXTURE_PROMPTS = {
    "coins_small": (
        "Bright polished yellow gold on every coin, warm golden shine, darker gold in the recessed "
        "engraving, light scratches on the rims, no black paint anywhere"
    ),
    "coins_medium": (
        "Dark tan worn leather pouch with visible stitching and a darker cord, bright polished "
        "yellow gold coins with a warm golden shine"
    ),
    "coins_large": (
        "Dark gunmetal steel safe body, brushed steel combination dial, orange painted corner "
        "reinforcements, bright polished yellow gold coins with a warm golden shine"
    ),
}


def load_state() -> dict:
    if STATE_FILE.exists():
        try:
            return json.loads(STATE_FILE.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            return {}
    return {}


def save_state(state: dict) -> None:
    STATE_FILE.write_text(json.dumps(state, indent=2), encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description="Génère les visuels 3D des lots de pièces via Meshy.")
    parser.add_argument("--only", choices=sorted(SHAPE_PROMPTS), help="ne traiter qu'un lot")
    parser.add_argument("--force", action="store_true", help="régénérer même si le GLB existe")
    parser.add_argument("--reshape", action="store_true",
                        help="repartir d'un nouveau maillage au lieu de retexturer celui mémorisé")
    args = parser.parse_args()

    key = rewards.api_key()
    state = load_state()

    names = [args.only] if args.only else list(SHAPE_PROMPTS)
    names = [n for n in names if args.force or not (OUTPUT_DIR / f"{n}.glb").exists()]

    if not names:
        print("Tous les GLB existent déjà. Utilise --force pour régénérer.")
        return

    # Vague 1 : les formes. Une prévisualisation déjà payée n'est jamais relancée.
    previews = {}
    pending = {}
    for name in names:
        known = None if args.reshape else state.get(f"preview_{name}")
        if known:
            print(f"{name} : réutilisation de la prévisualisation déjà générée.")
            previews[name] = known
            continue

        response = rewards.request("POST", rewards.API_URL, key, {
            "mode": "preview",
            "prompt": rewards.check_prompt(name, f"{SHAPE_PROMPTS[name]}, {rewards.STYLE}"),
            "topology": "triangle",
            "target_polycount": rewards.TARGET_POLYCOUNT,
            "should_remesh": True,
        })
        pending[name] = rewards.task_id_from(response)

        # Mémorisé tout de suite : la forme est facturée à la création, pas à la réussite.
        state[f"preview_{name}"] = pending[name]
        save_state(state)
        print(f"{name} : forme envoyée ({pending[name]}).", flush=True)

    if pending:
        print("\nVague 1 — formes :", flush=True)
        for name in rewards.wait_for_all(dict(pending), key, "formes"):
            previews[name] = pending[name]

    if not previews:
        sys.exit("Aucune forme utilisable, arrêt.")

    # Vague 2 : les textures, sur les maillages obtenus.
    refines = {}
    for name, preview_id in previews.items():
        response = rewards.request("POST", rewards.API_URL, key, {
            "mode": "refine",
            "preview_task_id": preview_id,
            "texture_prompt": rewards.check_prompt(name, f"{TEXTURE_PROMPTS[name]}, {rewards.FINISH}"),
            "enable_pbr": True,
        })
        refines[name] = rewards.task_id_from(response)
        print(f"{name} : texturation envoyée.", flush=True)

    print("\nVague 2 — textures :", flush=True)
    finished = rewards.wait_for_all(dict(refines), key, "textures")

    for name, task in finished.items():
        glb_url = (task.get("model_urls") or {}).get("glb")
        if not glb_url:
            print(f"  {name} : aucune URL GLB dans la réponse.")
            continue

        destination = OUTPUT_DIR / f"{name}.glb"
        rewards.download(glb_url, destination)
        size = destination.stat().st_size / 1024
        print(f"  {name} : écrit dans {destination.name} ({size:.0f} Ko)")

    print("\nTerminé. Repasse sur Unity pour laisser importer les GLB.")


if __name__ == "__main__":
    main()
