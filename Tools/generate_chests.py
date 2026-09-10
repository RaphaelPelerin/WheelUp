#!/usr/bin/env python3
"""
Génère les modèles 3D des coffres via l'API Meshy et les range là où le jeu les attend
(Assets/Resources/Chests/chest_<id>_<etat>.glb).

Stratégie de cohérence : l'API Meshy n'expose aucune graine, deux générations d'un même prompt
donnent donc deux objets différents. Pour que les trois coffres forment une vraie famille, on ne
génère qu'UNE prévisualisation par état (fermé, ouvert), puis on la texture trois fois avec
`texture_prompt`. Silhouette rigoureusement identique, finitions distinctes selon le palier.

Les identifiants de prévisualisation sont mémorisés dans Tools/.meshy_previews.json : relancer le
script réutilise le maillage déjà payé au lieu d'en générer un nouveau.

La clé est lue dans MESHY_API_KEY, jamais passée en argument ni affichée.

Exemples :
    python Tools/generate_chests.py                 # les trois coffres, fermés et ouverts
    python Tools/generate_chests.py --state closed  # seulement les versions fermées
    python Tools/generate_chests.py --force         # régénère les GLB existants
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
OUTPUT_DIR = ROOT / "Assets" / "Resources" / "Chests"
STATE_FILE = Path(__file__).resolve().parent / ".meshy_previews.json"

# Description de forme partagée : elle ne varie JAMAIS d'un palier à l'autre, c'est elle qui
# fabrique la parenté visuelle. Seule la finition change, via texture_prompt à l'étape refine.
SHAPE_PROMPTS = {
    "closed": (
        "A closed rugged metal supply crate for a motorcycle game, rounded rectangular box with "
        "heavy riveted corner brackets, a recessed seam running along the closed lid, a small "
        "embossed racing chevron badge centered on the front panel, chunky reinforced feet, "
        "stylized low-poly game asset, clean readable silhouette, neutral grey material"
    ),
    "open": (
        "An open rugged metal supply crate for a motorcycle game, same rounded rectangular box with "
        "heavy riveted corner brackets, lid open and tilted back on visible hinges, empty padded "
        "interior, a small embossed racing chevron badge centered on the front panel, chunky "
        "reinforced feet, stylized low-poly game asset, clean readable silhouette, neutral grey material"
    ),
}

# La progression se joue sur la matière et l'usure, pas sur la forme : bronze usé, acier propre,
# or ouvragé. Le liseré orange est commun aux trois, c'est le rattachement à l'identité du jeu.
TEXTURE_PROMPTS = {
    "bronze": (
        "Dark gunmetal body with worn scratched bronze trim and rivets, matte scuffed paint, dusty "
        "edges, a glowing orange light seam along the lid, weathered entry-tier crate, PBR materials"
    ),
    "argent": (
        "Dark gunmetal body with clean brushed steel trim and polished rivets, smooth semi-gloss "
        "paint, light wear on the corners, a glowing orange light seam along the lid, well-maintained "
        "mid-tier crate, PBR materials"
    ),
    "or": (
        "Dark gunmetal body with ornate polished gold trim, engraved gold corner brackets and gold "
        "rivets, deep glossy lacquered paint, pristine surface, a bright glowing orange light seam "
        "along the lid, premium top-tier crate, PBR materials"
    ),
}

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


def wait_for_task(task: str, key: str, label: str) -> dict:
    deadline = time.time() + POLL_TIMEOUT_SECONDS

    while time.time() < deadline:
        data = request("GET", f"{API_URL}/{task}", key)
        status = data.get("status")

        if status == "SUCCEEDED":
            return data
        if status in ("FAILED", "CANCELED"):
            message = (data.get("task_error") or {}).get("message", "sans détail")
            sys.exit(f"  {label} : tâche {status} — {message}")

        print(f"  {label} : {status} {data.get('progress', 0)}%", flush=True)
        time.sleep(POLL_SECONDS)

    sys.exit(f"  {label} : délai dépassé au bout de {POLL_TIMEOUT_SECONDS}s.")


def base_preview(state_name: str, key: str, state: dict) -> str:
    """Maillage partagé par les trois paliers, généré une seule fois puis mémorisé."""
    existing = state.get(f"preview_{state_name}")
    if existing:
        print(f"Maillage {state_name} : réutilisation de la prévisualisation déjà générée.")
        return existing

    print(f"\nMaillage {state_name} : génération de la forme commune aux trois coffres…", flush=True)
    response = request("POST", API_URL, key, {
        "mode": "preview",
        "prompt": SHAPE_PROMPTS[state_name],
        "topology": "triangle",
        "target_polycount": 30000,
        "should_remesh": True,
    })

    preview_id = task_id_from(response)
    wait_for_task(preview_id, key, f"{state_name} (forme)")

    state[f"preview_{state_name}"] = preview_id
    save_state(state)
    return preview_id


def download(url: str, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    with urllib.request.urlopen(url, timeout=300) as response, open(destination, "wb") as out:
        out.write(response.read())


def texture_variant(chest_id: str, state_name: str, preview_id: str, key: str) -> None:
    destination = OUTPUT_DIR / f"chest_{chest_id}_{state_name}.glb"
    label = f"{chest_id}/{state_name}"

    print(f"{label} : texturation sur le maillage commun…", flush=True)
    response = request("POST", API_URL, key, {
        "mode": "refine",
        "preview_task_id": preview_id,
        "texture_prompt": TEXTURE_PROMPTS[chest_id],
        "enable_pbr": True,
    })

    task = wait_for_task(task_id_from(response), key, label)
    glb_url = (task.get("model_urls") or {}).get("glb")
    if not glb_url:
        sys.exit(f"  {label} : aucune URL GLB dans la réponse.")

    download(glb_url, destination)
    print(f"  {label} : écrit dans {destination.name}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Génère les coffres 3D via Meshy.")
    parser.add_argument("--state", choices=sorted(SHAPE_PROMPTS), help="ne traiter qu'un état")
    parser.add_argument("--only", choices=sorted(TEXTURE_PROMPTS), help="ne traiter qu'un palier")
    parser.add_argument("--force", action="store_true", help="régénérer même si le GLB existe")
    args = parser.parse_args()

    key = api_key()
    state = load_state()

    states = [args.state] if args.state else list(SHAPE_PROMPTS)
    chests = [args.only] if args.only else list(TEXTURE_PROMPTS)

    for state_name in states:
        pending = [c for c in chests
                   if args.force or not (OUTPUT_DIR / f"chest_{c}_{state_name}.glb").exists()]

        if not pending:
            print(f"\nMaillage {state_name} : tous les GLB existent déjà, rien à faire.")
            continue

        preview_id = base_preview(state_name, key, state)
        for chest_id in pending:
            texture_variant(chest_id, state_name, preview_id, key)

    print("\nTerminé. Repasse sur Unity pour laisser importer les GLB.")


if __name__ == "__main__":
    main()
