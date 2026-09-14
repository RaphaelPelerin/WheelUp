#!/usr/bin/env python3
"""
Génère les maquettes 3D des trois cartes via l'API Meshy et les range là où le jeu les attend
(Assets/Resources/Maps/<MapId>.glb).

Stratégie de cohérence : une métropole, une montagne et un bord de mer sont trois objets
différents, il n'y a donc aucun maillage à partager comme pour les coffres. La parenté se joue sur
deux fragments répétés à l'identique dans les trois — STYLE pour la forme, FINISH pour la matière —
et surtout sur le socle : la même dalle hexagonale basse porte les trois scènes. C'est elle qui
fait les trois jetons d'une même collection au lieu de trois maquettes sans rapport.

Les trois sont traitées par vagues plutôt qu'une par une : les trois prévisualisations partent
ensemble, puis les trois texturations. Six tâches séquentielles prendraient une demi-heure, deux
vagues en prennent quelques minutes.

Les identifiants de prévisualisation sont mémorisés dans Tools/.meshy_maps.json : relancer le
script réutilise le maillage déjà payé au lieu d'en générer un nouveau.

La clé est lue dans MESHY_API_KEY, jamais passée en argument ni affichée.

Exemples :
    python Tools/generate_maps.py                    # les trois cartes
    python Tools/generate_maps.py --only Montagne    # une seule
    python Tools/generate_maps.py --force            # régénère les GLB existants
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
OUTPUT_DIR = ROOT / "Assets" / "Resources" / "Maps"
STATE_FILE = Path(__file__).resolve().parent / ".meshy_maps.json"

# Répété mot pour mot dans les trois prompts de forme. Le socle en est la pièce maîtresse : c'est
# le seul élément rigoureusement commun aux trois maquettes, et donc ce qui les fait lire comme une
# famille quand on les fait défiler l'une après l'autre dans le carrousel.
STYLE = (
    "stylized low-poly game asset, architectural scale model diorama, chunky simplified forms, "
    "clean readable silhouette, neutral grey material, the whole scene sitting on one low "
    "hexagonal base slab with a chamfered rim, nothing extending past the edge of the base, "
    "no background, no extra ground plane"
)

# Même rôle pour la matière. Le liseré rouge est celui du logo (UITheme.Brand, #FD1823) : c'est le
# rattachement à l'identité du jeu, et il court sur le socle, donc au même endroit sur les trois.
FINISH = (
    "PBR materials, matte finish, subtle wear along the edges, a bright red painted stripe around "
    "the chamfered rim of the base slab, dark neutral grey base slab, clean game-ready texturing"
)

# Les clés sont les valeurs de l'enum MapId : MapInfo.ModelResourcePath vaut "Maps/" + Id, et c'est
# sous ce nom exact que MapPreview ira chercher le fichier. Les renommer casserait le chargement.
SHAPE_PROMPTS = {
    "Metropole": (
        "A dense city block diorama, a tight grid of tall rectangular skyscrapers of varying "
        "heights packed close together, flat rooftops with small boxy vents and water tanks, "
        "straight streets cutting the grid into regular blocks, the tallest towers near the centre "
        "and lower buildings around the outer edge"
    ),
    "Montagne": (
        "A mountain diorama, one single steep rocky peak rising from the centre, a wide switchback "
        "road coiling around the slope from the base up to the summit, faceted angular rock faces, "
        "a scatter of small conifer trees on the lower slopes, bare rock and snow near the top"
    ),
    "CoteAzur": (
        "A coastal town diorama, a curved sandy bay along one side, calm flat water, a straight "
        "seafront promenade lined with a row of low buildings and a few taller apartment blocks "
        "behind them, a short pier reaching out into the water, small palm trees along the promenade"
    ),
}

TEXTURE_PROMPTS = {
    "Metropole": (
        "Cool dark grey concrete towers with lighter grey facades, rows of small dark windows, "
        "black asphalt streets with pale road markings, a few warm lit windows scattered across "
        "the towers"
    ),
    "Montagne": (
        "Warm grey faceted rock with darker crevices, dark asphalt switchback road with a pale "
        "edge line, deep green conifer trees, clean white snow on the summit"
    ),
    "CoteAzur": (
        "Warm off-white and sand coloured buildings with terracotta roofs, pale golden sand, clear "
        "turquoise water, light grey promenade paving, deep green palm fronds"
    ),
}

# Plafond impose par l'API : au-dela, la requete part en 400 sans qu'aucune tache soit creee.
MAX_PROMPT_CHARS = 800

TARGET_POLYCOUNT = 30000
POLL_SECONDS = 10
POLL_TIMEOUT_SECONDS = 1800


def api_key() -> str:
    key = os.environ.get("MESHY_API_KEY", "").strip()
    if not key:
        sys.exit("MESHY_API_KEY absente de l'environnement. Définis-la avant de lancer le script.")
    return key


def check_prompt(label: str, prompt: str) -> str:
    """Refuse un prompt trop long ici plutot que de laisser l'API le rejeter en 400."""
    if len(prompt) > MAX_PROMPT_CHARS:
        sys.exit(
            f"{label} : prompt de {len(prompt)} caracteres, maximum {MAX_PROMPT_CHARS}. "
            f"Raccourcis la description : STYLE et FINISH comptent dans le total."
        )
    return prompt


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
    """Attend une vague entiere de taches. tasks : {nom_carte: id_tache}."""
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
        # Ces tâches sont payées et continuent chez Meshy. Les nommer permet de les reprendre :
        # sortir sans les afficher revenait à jeter le crédit déjà dépensé.
        print(f"  {label} : délai dépassé. Tâches encore en cours chez Meshy :", flush=True)
        for pending_name, pending_task in tasks.items():
            print(f"    {pending_name} : {pending_task}", flush=True)
        print("  Relance sans --reshape pour les réutiliser au lieu d'en repayer.", flush=True)
    return done


def download(url: str, destination: Path) -> None:
    destination.parent.mkdir(parents=True, exist_ok=True)
    with urllib.request.urlopen(url, timeout=300) as response, open(destination, "wb") as out:
        out.write(response.read())


def main() -> None:
    parser = argparse.ArgumentParser(description="Génère les maquettes 3D des cartes via Meshy.")
    parser.add_argument("--only", choices=sorted(SHAPE_PROMPTS), help="ne traiter qu'une carte")
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
            "prompt": check_prompt(name, f"{SHAPE_PROMPTS[name]}, {STYLE}"),
            "topology": "triangle",
            "target_polycount": TARGET_POLYCOUNT,
            "should_remesh": True,
        })
        pending[name] = task_id_from(response)

        # Mémorisé tout de suite : la forme est facturée à la création, pas à la réussite. Attendre
        # la fin de la vague pour l'écrire perdait le crédit dès que l'attente tournait court.
        state[f"preview_{name}"] = pending[name]
        save_state(state)
        print(f"{name} : forme envoyée ({pending[name]}).", flush=True)

    if pending:
        print("\nVague 1 — formes :", flush=True)
        for name in wait_for_all(dict(pending), key, "formes"):
            previews[name] = pending[name]

    if not previews:
        sys.exit("Aucune forme utilisable, arrêt.")

    # Vague 2 : les textures, sur les maillages obtenus.
    refines = {}
    for name, preview_id in previews.items():
        response = request("POST", API_URL, key, {
            "mode": "refine",
            "preview_task_id": preview_id,
            "texture_prompt": check_prompt(name, f"{TEXTURE_PROMPTS[name]}, {FINISH}"),
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
