# Boutique — vitrine hiérarchisée

Date : 2026-09-14

## Problème

L'onglet Boutique aligne quatre cartes de largeur égale : les trois lots de pièces et le retrait
des publicités. Rien ne les distingue — même panneau gris, même bouton rouge, même taille. Trois
conséquences :

- **Les lots n'ont aucun visuel.** `BuildCoinPreview` pose un halo flou et écrit le total en gros,
  faute de modèle 3D. Ce n'est plus vrai : `Assets/Resources/Rewards/coins.glb` existe depuis la
  génération des récompenses, et `ChestThumbnail` montre exactement comment l'afficher.
- **L'argument de vente est caché.** Le bonus (« +750 offertes, 15 % ») part derrière le bouton
  « i » parce que quatre cartes serrées ne laissent pas la place de l'écrire. C'est précisément ce
  qu'il faudrait montrer en premier.
- **Le retrait des publicités passe pour un lot de pièces.** Même carte, même bouton, alors que
  c'est un achat d'une autre nature — non consommable, une seule fois dans la vie du compte.

Le jeu est en paysage seulement (`allowedAutorotateToPortrait: 0`) : une seule mise en page à
dessiner.

## Ce qu'on produit

La mise en page validée en maquette :

- **Carte héros à gauche**, sur toute la hauteur de la rangée, pour le lot mis en avant : pile de
  pièces en 3D, nom, total, bonus écrit en clair, ruban « MEILLEURE OFFRE », bouton d'achat.
- **Deux cartes empilées à droite** pour les autres lots, mêmes éléments en plus petit.
- **Bande basse pleine largeur** pour le retrait des publicités, filet rouge à gauche, bouton
  secondaire — une offre de service, pas un lot.

## Découpage

`Storefront` promet aujourd'hui, dans son commentaire de tête, que les deux vitrines ont
« exactement la même bannière et les mêmes cartes ». Cette mise en page rompt la promesse. On
l'assume en changeant ce que `Storefront` promet : non plus une mise en page commune, mais des
**briques communes**.

- `BuildHeader`, `BuildRow`, `BuildCard` et `AddPreviewGlow` ne changent pas d'une ligne, sauf
  `BuildRow` qui gagne un paramètre optionnel de marge basse (valeur par défaut : le comportement
  actuel). L'onglet Coffres ne voit donc rien.
- Deux briques nouvelles : `BuildHeroCard` et `BuildBand`.
- `ShopMenu` compose sa page avec ces briques au lieu d'appeler `BuildRow` + trois `BuildCard`.

Un fichier nouveau, `CoinStackThumbnail.cs`, calqué sur `ChestThumbnail` : un `ModelPreviewRig` qui
charge `Rewards/coins` et le fait tourner lentement. Les rigs coupent leur caméra quand leur
`RawImage` est désactivé (`ModelPreviewRig.Update`), et les onglets cachés sont désactivés : les
trois piles ne coûtent rien hors de la boutique.

## Données

`CoinPack` gagne un champ, `Highlight` : le booléen qui dit quel lot porte le ruban. Déplacer la
mise en avant devient une ligne du catalogue, pas une ligne de code d'affichage. Le texte du ruban
reste une constante de `ShopMenu` — tant qu'il n'y en a qu'un, il n'a rien à faire dans les données.

Les totaux s'écrivent avec un séparateur de milliers : **19 000**, pas `19000`. Séparateur espace
ordinaire et non insécable : la police de l'UI est chargée dynamiquement et rien ne garantit
qu'elle porte U+00A0.

## Le bouton « i »

Sur la carte héros il disparaît : la description et le bonus tiennent enfin sur la carte. Sur les
deux petites cartes il reste, en haut à droite — leur description est du vrai contenu et n'a pas la
place de s'écrire.

## Ce qui ne change pas

L'onglet Coffres, `MonetizationManager`, les prix, et les identifiants produit (`coins_small`,
`remove_ads`…) : ce sont les clés d'achat déclarées sur les stores.

## Vérification

1. Le projet compile.
2. Les trois piles de pièces tournent dans leurs cartes ; aucune ne retombe sur le repli texte.
3. Le ruban est sur le Coffre-fort, et le déplacer dans le catalogue le déplace à l'écran.
4. Acheté, le retrait des publicités éteint sa bande et affiche « DÉJÀ ACTIF ».
5. Pendant une transaction, aucun bouton de la page n'est cliquable.
6. L'onglet Coffres est pixel pour pixel celui d'avant.
