#!/usr/bin/env python3
"""
Génère les icônes 3D des lots de coffre via l'API Meshy et les range là où le jeu les attend
(Assets/Resources/Rewards/<id>.glb).

Différence avec generate_chests.py : les six objets sont des objets *différents*, il n'y a donc
aucun maillage à partager. La parenté visuelle se joue autrement, sur deux fragments de prompt
répétés à l'identique dans les six : STYLE pour la forme, FINISH pour la matière. C'est ce qui
évite six icônes au style hétérogène.

Les six sont traités par vagues plutôt qu'un par un : les six prévisualisations partent ensemble,
puis les six texturations. Douze tâches séquentielles prendraient une demi-heure, deux vagues en
prennent quelques minutes.

Les identifiants de prévisualisation sont mémorisés dans Tools/.meshy_rewards.json : relancer le
script réutilise le maillage déjà payé au lieu d'en générer un nouveau.

La clé est lue dans MESHY_API_KEY, jamais passée en argument ni affichée.

Exemples :
    python Tools/generate_rewards.py                # les six icônes
    python Tools/generate_rewards.py --only engine  # une seule
    python Tools/generate_rewards.py --force        # régénère les GLB existants
"""

import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.request
from pathlib import Path

API_URL = "https://api.meshy.ai/openapi/v2/text-to-3d"
ROOT = Path(__file__).resolve().parent.parent
OUTPUT_DIR = ROOT / "Assets" / "Resources" / "Rewards"
STATE_FILE = Path(__file__).resolve().parent / ".meshy_rewards.json"

# Répété mot pour mot dans les six prompts de forme : c'est lui qui fait tenir la famille.
STYLE = (
    "stylized low-poly game inventory icon, single centered object, clean readable silhouette, "
    "chunky simplified forms, neutral grey material, plain object with no background, no ground "
    "plane, no pedestal, no base"
)

# Même rôle pour la matière, et raccroche les icônes à la direction artistique des coffres.
FINISH = (
    "PBR materials, matte finish, subtle wear along the edges, clean game-ready texturing"
)

SHAPE_PROMPTS = {
    "coins": (
        "A neat stack of five thick round gold coins lying flat on top of one another, plus one coin "
        "leaning upright against the side of the stack, each coin with a raised rim and a simple "
        "embossed chevron emblem on its face, coins clearly separated from each other"
    ),
    "engine": (
        "A motorcycle engine seen from the side, boxy rectangular crankcase at the bottom, one "
        "upright square cylinder above it with wide flat horizontal cooling fins stacked like "
        "plates, a rectangular cylinder head on top with four bolts, a short exhaust pipe leaving "
        "the front, strictly boxy and mechanical"
    ),
    "suspension": (
        "One single motorcycle coil-over shock absorber, one straight vertical cylindrical tube with "
        "a helical coil spring wrapped tightly around it from top to bottom, a round mounting eyelet "
        "at the very top and another round mounting eyelet at the very bottom, perfectly symmetrical "
        "around one vertical axis, exactly one part, nothing crossing it, nothing beside it"
    ),
    "tires": (
        "A motorcycle wheel standing upright, chunky knobby tire tread, five spoke alloy rim, "
        "central hub with a brake disc"
    ),
    "weight": (
        "A lightweight motorcycle trellis frame section, open tubular structure with diagonal cross "
        "braces and welded joints, hollow and airy"
    ),
    "paint": (
        "An aerosol spray paint can standing upright, cylindrical body, tapered shoulder, cap with a "
        "spray nozzle, crimped rim at the base"
    ),
}

TEXTURE_PROMPTS = {
    "coins": (
        "Bright polished yellow gold on every surface, warm golden shine, darker gold in the "
        "recessed engraving, light scratches on the rims, no black paint anywhere"
    ),
    "engine": (
        "Dark gunmetal crankcase, light brushed aluminium cooling fins clearly lighter than the "
        "crankcase, an orange painted cam cover on top, chrome exhaust pipe"
    ),
    "suspension": (
        "Dark anodised damper body, bright orange coil spring, polished chrome rod, aluminium collars"
    ),
    "tires": (
        "Deep black rubber tire, dark grey alloy rim with a thin orange pinstripe on the rim edge, "
        "steel brake disc"
    ),
    "weight": "Woven carbon fibre tubes under a satin clearcoat, orange anodised aluminium joints",
    "paint": "Glossy off-white spray can body with a dark grey label band and an orange cap",
}

TARGET_POLYCOUNT = 20000
POLL_SECONDS = 10
POLL_TIMEOUT_SECONDS = 900


def api_key() -> str:
    key = os.environ.get("MESHY_API_KEY", "").strip()
    if not key:
        sys.exit("MESHY_API_KEY absente de l'environnement. Définis-la avant de lancer le script.")
    return key


def load_state() -> dict:
    if STATE_FILE.exists():
        try:
            return json.loads(STATE_FILE.read_text(encoding="utf-8"))
        except json.JSONDecodeError:
            return {}
    return {}


def save_state(state: dict) -> None:
    STATE_FILE.write_text(json.dumps(state, indent=2), encoding="utf-8")


def request(method: str, url: str, key: str, payload: dict | None = None) -> dict:
    data = json.dumps(payload).encode("utf-8") if payload is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Authorization", f"Bearer {key}")
    if data:
        req.add_header("Content-Type", "application/json")

    try:
        with urllib.request.urlopen(req, timeout=60) as response:
            return json.loads(response.read().decode("utf-8"))
    except urllib.error.HTTPError as error:
        body = error.read().decode("utf-8", errors="replace")
        sys.exit(f"Meshy a répondu {error.code} sur {method} {url} :\n{body}")
    except urllib.error.URLError as error:
        sys.exit(f"Impossible de joindre Meshy : {error.reason}")


def task_id_from(response: dict) -> str:
    return response.get("result") or response.get("id")


def wait_for_all(tasks: dict, key: str, label: str) -> dict:
    """Attend une vague entière de tâches. tasks : {id_objet: id_tache}."""
    done = {}
    deadline = time.time() + POLL_TIMEOUT_SECONDS

    while tasks and time.time() < deadline:
        progress = []
        for name in list(tasks):
            data = request("GET", f"{API_URL}/{tasks[name]}", key)
            status = data.get("status")

            if status == "SUCCEEDED":
                done[name] = data
                del tasks[name]
                progress.append(f"{name} ok")
            elif status in ("FAILED", "CANCELED"):
                message = (data.get("task_error") or {}).get("message", "sans détail")
                print(f"  {name} : {status} — {message}", flush=True)
                del tasks[name]
            else:
                progress.append(f"{name} {data.get('progress', 0)}%")

        if tasks:
            print(f"  {label} : " + ", ".join(progress), flush=True)
            time.sleep(POLL_SECONDS)

    if tasks:
        sys.exit(f"  {label} : délai dépassé pour {', '.join(tasks)}.")
    return done


def download(url: str, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    with urllib.request.urlopen(url, timeout=300) as response, open(destination, "wb") as out:
        out.write(response.read())


def main() -> None:
    parser = argparse.ArgumentParser(description="Génère les icônes de récompense 3D via Meshy.")
    parser.add_argument("--only", choices=sorted(SHAPE_PROMPTS), help="ne traiter qu'une icône")
    parser.add_argument("--force", action="store_true", help="régénérer même si le GLB existe")
    parser.add_argument("--reshape", action="store_true",
                        help="repartir d'un nouveau maillage au lieu de retexturer celui mémorisé")
    args = parser.parse_args()

    key = api_key()
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

        response = request("POST", API_URL, key, {
            "mode": "preview",
            "prompt": f"{SHAPE_PROMPTS[name]}, {STYLE}",
            "topology": "triangle",
            "target_polycount": TARGET_POLYCOUNT,
            "should_remesh": True,
        })
        pending[name] = task_id_from(response)
        print(f"{name} : forme envoyée.", flush=True)

    if pending:
        print("\nVague 1 — formes :", flush=True)
        for name in wait_for_all(dict(pending), key, "formes"):
            previews[name] = pending[name]
            state[f"preview_{name}"] = pending[name]
            save_state(state)

    if not previews:
        sys.exit("Aucune forme utilisable, arrêt.")

    # Vague 2 : les textures, sur les maillages obtenus.
    refines = {}
    for name, preview_id in previews.items():
        response = request("POST", API_URL, key, {
            "mode": "refine",
            "preview_task_id": preview_id,
            "texture_prompt": f"{TEXTURE_PROMPTS[name]}, {FINISH}",
            "enable_pbr": True,
        })
        refines[name] = task_id_from(response)
        print(f"{name} : texturation envoyée.", flush=True)

    print("\nVague 2 — textures :", flush=True)
    finished = wait_for_all(dict(refines), key, "textures")

    for name, task in finished.items():
        glb_url = (task.get("model_urls") or {}).get("glb")
        if not glb_url:
            print(f"  {name} : aucune URL GLB dans la réponse.")
            continue

        destination = OUTPUT_DIR / f"{name}.glb"
        download(glb_url, destination)
        size = destination.stat().st_size / 1024
        print(f"  {name} : écrit dans {destination.name} ({size:.0f} Ko)")

    print("\nTerminé. Repasse sur Unity pour laisser importer les GLB.")


if __name__ == "__main__":
    main()
