# Maquettes 3D des trois cartes — design

Date : 2026-09-14

## Problème

Le carrousel de l'onglet JOUER — le premier écran du menu, celui qu'on voit en lançant le jeu —
montre les trois cartes du GDD. Aucune n'a de maquette : `Assets/Resources/Maps/` est vide, et
`MapPreview.Show()` retombe donc sur le simple nom de la carte en texte. Le joueur choisit un décor
sans jamais le voir.

Le carrousel, lui, est complet. Il n'y a rien à recoder : il ne manque que les trois fichiers.

## Ce qu'on produit

Trois GLB dans `Assets/Resources/Maps/`, nommés sur l'enum `MapId` parce que
`MapInfo.ModelResourcePath` vaut `"Maps/" + Id` :

    Metropole.glb    Montagne.glb    CoteAzur.glb

Aucune ligne de C#. Les trois fichiers déposés là suffisent.

## Corrélation entre les trois

Les coffres partagent un maillage : une seule forme générée, texturée trois fois. Impossible ici —
une métropole, une montagne et un bord de mer sont trois objets différents.

La parenté se fabrique donc comme pour les icônes de récompense, par des fragments de prompt
répétés mot pour mot dans les trois, plus un dispositif propre aux dioramas :

- `STYLE` — low-poly stylisé, maquette d'architecte, formes simplifiées, silhouette lisible,
  matériau gris neutre, **et surtout** la dalle hexagonale basse à bord chanfreiné qui porte la
  scène. Rien ne dépasse du socle.
- `FINISH` — PBR mat, usure légère sur les arêtes, liseré rouge sur l'arête du socle, dalle gris
  neutre sombre.

Le socle identique est le dispositif principal : il fait des trois cartes trois jetons de la même
collection, ce qui se lit immédiatement quand on les fait défiler l'une après l'autre. Le style
partagé seul aurait donné trois silhouettes sans point commun structurel.

Seul le contenu posé sur la dalle varie : damier de gratte-ciels, pic et lacets, front de mer.

## Couleur d'accent

Rouge de marque `#FD1823`, celui de `UITheme.Brand`.

Les prompts des coffres et des récompenses demandent de l'orange. C'est un écart assumé : l'accent
réel du jeu est le rouge du logo, et une maquette de carte est vue à côté du menu, pas à côté d'un
coffre. Si l'orange devait revenir, c'est `FINISH` qu'on change, à un seul endroit.

## Le script

`Tools/generate_maps.py`, calqué sur `generate_rewards.py` dont il reprend la structure :

- Deux vagues plutôt que six tâches en file : les trois formes partent ensemble, puis les trois
  texturations. Quelques minutes au lieu d'une demi-heure.
- L'identifiant de chaque prévisualisation est écrit dans `Tools/.meshy_maps.json` **dès la
  création**, pas à la réussite : la forme est facturée au départ, et une attente qui tourne court
  ne doit pas jeter le crédit.
- Garde-fou à 800 caractères par prompt, vérifié avant l'envoi : au-delà l'API répond 400 sans
  créer de tâche.
- `--only`, `--force`, `--reshape`, mêmes rôles que dans les deux autres scripts.
- La clé est lue dans `MESHY_API_KEY`, jamais passée en argument ni affichée.

## Vérification

1. Les trois GLB existent et pèsent un poids plausible.
2. Unity les importe.
3. Le jeu lancé : les trois maquettes tournent dans le carrousel, le repli texte a disparu.
4. Le cadrage de `MapPreview` (`heightFactor: 0.30f`) tient avec un socle — c'est la seule mesure
   qui n'a jamais été éprouvée sur un modèle réel.

## Coût

Six tâches Meshy facturées, environ dix minutes. Une relance ne repaie que les texturations.
