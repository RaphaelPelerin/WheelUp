# Progression par niveaux : XP, paliers de motos et coffres

Conception validée le 2026-09-14.

## Intention

Le jeu sait déjà mesurer ce que fait le joueur (14 grandeurs, missions du jour et de la
semaine, succès au long cours) et il sait déjà le payer en pièces. Ce qui manque, c'est une
progression qui **s'accumule sans jamais redescendre** et qui **ouvre du contenu autrement
qu'en payant**.

Deux manques précis que cette conception comble :

- Les points de prouesse de `StuntScorer` meurent à la fin de chaque session. Ils sont
  calculés finement — multiplicateur, bonus d'équilibre, de risque, de lenteur — et ne
  servent à rien.
- Les motos ne s'obtiennent qu'en accumulant des pièces. Aucun jalon, aucun moment de
  choix, rien qui récompense la durée.

## Portée

Cette spec couvre le moteur d'XP, les récompenses de niveau et l'écran de choix de moto.

**Hors portée, pour une passe ultérieure :** les parcours de missions enchaînés
(« Apprenti » → « Pilote » → « Cascadeur »). Ils se grefferont sur ce moteur en donnant de
l'XP comme le reste.

## Contrainte assumée

**Une seule moto du catalogue a un modèle 3D** (`Motos/KawasakiNinjaH2`). Les 15 motos
proposées aux paliers s'afficheront donc toutes avec ce modèle jusqu'à ce que les autres
soient produits.

C'est jouable — chaque moto garde sa fiche technique, donc son comportement réel — mais ce
n'est pas présentable en l'état. Le système est construit maintenant et se corrigera seul à
mesure que les modèles arrivent. Aucune ligne de cette spec ne dépend de leur présence.

---

## 1. `LevelManager` — le moteur

Classe statique dans `Assets/Scripts/Core/`, sur le modèle d'`EconomyManager`.

### État persisté

**Un seul nombre : l'XP total.** Le niveau s'en déduit par la courbe. Stocker les deux
séparément les laisserait diverger — c'est la panne classique de ce genre de système.

Clé PlayerPrefs : `level_total_xp`.

Une seconde clé, `level_pending_choices`, retient les paliers de moto dus mais pas encore
honorés (voir §4).

### Interface

```csharp
public static event Action Changed;

public static int TotalXp { get; }
public static int Level { get; }          // déduit de TotalXp
public static int XpIntoLevel { get; }    // progression dans le niveau courant
public static int XpForNextLevel { get; } // coût du niveau courant
public static float Ratio { get; }        // 0..1, pour la jauge

public static void AddXp(int amount);
```

`AddXp` détecte les franchissements de niveau et met en file les récompenses gagnées. Il
doit gérer le **franchissement multiple** : un gros versement d'XP peut faire passer deux
niveaux d'un coup, et les deux récompenses sont dues.

### Courbe

Coût pour passer du niveau `n` au niveau `n+1` :

```
120 + 15 × (n − 1)
```

Niveau 2 : 120 XP. Niveau 3 : 135. Niveau 4 : 150. Et ainsi de suite.

Cumul pour atteindre le niveau 25 : **7 020 XP**.

---

## 2. Sources d'XP

| Source | XP | Où le brancher |
|---|---|---|
| Mission du jour récupérée | 60 / 100 / 160 selon le palier | `MissionManager.Claim()` |
| Mission de la semaine récupérée | 200 / 350 / 500 selon le palier | `MissionManager.Claim()` |
| Points de prouesse | 1 XP pour 100 points | Fin de session, au récapitulatif |

L'XP est versé **à la récupération de la mission, pas à son achèvement**. Ça respecte la
séparation déjà en place dans `MissionManager` : boucler et encaisser sont deux gestes, et
c'est l'encaissement qui paie.

Revenu attendu pour un joueur qui revient chaque jour : **≈ 500 XP/jour** (315 de missions
du jour, 150 de missions hebdomadaires amorties, 50 de prouesses).

### Rythme visé

| Jalon | XP cumulé | Jour |
|---|---|---|
| Niveau 5 — moto 1 | 570 | 1–2 |
| Niveau 10 — moto 2 | 1 620 | 3–4 |
| Niveau 15 — moto 3 | 3 045 | 6 |
| Niveau 20 — moto 4 | 4 845 | 10 |
| Niveau 25 — moto 5 | 7 020 | **14** |

Au-delà du niveau 25, un niveau coûte environ une journée de jeu, donc **un coffre or tous
les 5 à 6 jours**.

Ces délais supposent un joueur quotidien qui récupère ses six missions. Un joueur qui vient
un jour sur deux mettra le double : c'est inhérent à une progression adossée à des missions
quotidiennes, et c'est assumé.

---

## 3. `LevelRewardCatalog` — la table des récompenses

Classe statique dans `Assets/Scripts/Data/`, sur le modèle de `ChestCatalog`.

Une seule fonction pure, `RewardFor(int level)`, qui applique cette règle :

| Condition | Récompense |
|---|---|
| Niveau multiple de 5, et ≤ 25 | **Choix entre 3 motos**, palier = niveau / 5 |
| Niveau multiple de 5, et > 25 | Coffre **or** |
| Niveau pair | Coffre **argent** |
| Niveau impair | Coffre **bronze** |

Déroulé des premiers niveaux : 2 argent, 3 bronze, 4 argent, **5 moto**, 6 argent,
7 bronze, 8 argent, 9 bronze, **10 moto**… puis à partir de 26 : 26 argent, 27 bronze,
28 argent, 29 bronze, **30 or**.

Le niveau 1 est l'état de départ : il ne verse rien.

### Les cinq paliers

Trois motos par palier, choisies pour être **équivalentes en puissance**. Un palier dont
une moto domine les deux autres ne propose pas un choix mais un piège.

| Palier | Niv. | Moto | ch | kg | ch/kg | V max |
|---|---|---|---|---|---|---|
| 1 | 5 | Derbi Senda X-Treme | 8,5 | 98 | 0,087 | 80 |
| | | Rieju MRT 50 | 8 | 94 | 0,085 | 80 |
| | | Honda MSX125 Grom | 9,8 | 103 | 0,095 | 95 |
| 2 | 10 | Yamaha MT-125 | 15 | 142 | 0,106 | 120 |
| | | KTM 125 Duke | 15 | 147 | 0,102 | 118 |
| | | Aprilia RS 125 | 15 | 144 | 0,104 | 130 |
| 3 | 15 | Kawasaki Z650 | 68 | 187 | 0,364 | 200 |
| | | Yamaha MT-07 | 73 | 184 | 0,397 | 214 |
| | | Husqvarna 701 Supermoto | 74 | 165 | 0,448 | 190 |
| 4 | 20 | Ducati Hypermotard 950 | 114 | 200 | 0,570 | 225 |
| | | Yamaha YZF-R6 | 118 | 190 | 0,621 | 260 |
| | | Yamaha MT-09 | 119 | 189 | 0,630 | 230 |
| 5 | 25 | Yamaha YZF-R1 | 200 | 201 | 0,995 | 299 |
| | | Kawasaki ZX-10R | 203 | 207 | 0,981 | 299 |
| | | BMW S1000RR | 207 | 197 | 1,051 | 299 |

L'écart de puissance interne tombe à 4 % au palier 4 et 3,5 % au palier 5 ; il est nul au
palier 2, où les trois 125 font exactement 15 ch.

Ce qui distingue les trois, c'est le caractère. Au palier 3, la Husqvarna est 20 kg plus
légère mais plafonne 24 km/h sous la MT-07 : supermotard contre roadster. Au palier 4,
trois catégories à puissance égale. Le palier 5 n'est que des sportives, et c'est
délibéré : c'est la classe superbike, c'est le but du parcours.

Le palier 1 emprunte la Grom aux 125 : le catalogue ne contient que trois mécaboites et la
Beta RR 50 est la moto de départ. À 9,8 ch pour 103 kg elle reste dans la classe.

### Motos hors paliers

Restent achetables aux pièces uniquement : KTM 125 EXC, Yamaha WR450F, Kawasaki Z900,
Triumph Street Triple 765, Honda CBR600RR, et la **Kawasaki Ninja H2 à 15 000**, qui reste
le trophée qu'on ne peut que s'offrir.

Les deux motos **non choisies** à un palier restent au garage à leur prix. Le niveau en
offre une ; il n'en ferme aucune.

---

## 4. L'écran de choix

`MotoChoiceScreen`, dans `Assets/Scripts/UI/`, bâti sur le modèle de
`ChestOpeningScreen` : superposition plein écran posée sur le canvas racine, et non dans la
zone sûre, pour couvrir jusqu'aux bords.

Trois cartes, chacune avec l'aperçu 3D (`MotoPreview`, repli sur le nom écrit si le modèle
manque), le nom, la catégorie et la fiche technique.

**La fiche ne doit pas afficher de score global de puissance.** Il serait identique sur les
trois et rendrait le choix illisible. Elle montre ce qui diffère réellement : poids,
vitesse de pointe, freinage, maniabilité.

### Quand il s'ouvre

**Au menu principal, jamais en conduite.** Monter de niveau au milieu d'un wheeling ne doit
pas interrompre la partie.

`LevelManager` met les paliers dus dans `level_pending_choices`. `MainMenuController` les
présente à l'entrée, exactement comme il présente déjà l'ouverture d'un coffre. Un choix en
attente **survit donc à la fermeture de l'application** : il est persisté, pas gardé en
mémoire.

À la validation : `GarageOwnership.Grant(nom)`, puis `Loadout.Select(moto)` pour équiper
tout de suite la moto choisie — sans quoi le joueur repartirait sur l'ancienne sans
comprendre pourquoi.

---

## 5. Corrections rendues nécessaires

### La moto de départ change

La Ninja H2 est aujourd'hui la seule `OwnedByDefault`. Commencer avec la meilleure moto du
jeu viderait les cinq paliers de leur sens. Le commentaire du catalogue prévoit déjà son
retrait.

**La Beta RR 50 Motard devient la moto offerte.** Elle est déjà à 0 pièce : c'est sa
vocation.

### Le repli de `RideableModelPath()` doit changer

Retirer `OwnedByDefault` de la H2 casse `Loadout.RideableModelPath()`. Il retombe
aujourd'hui sur `MotoCatalog.Default`, qui parcourt le catalogue à la recherche d'une moto
**à la fois offerte et pourvue d'un modèle**. Sans la H2, plus aucune ne remplit les deux
conditions : `Default` renvoie `All[0]` (la Beta RR 50, sans modèle), le chemin est nul, et
**plus aucune moto ne s'affiche**.

Correction : le repli cherche **la première moto du catalogue ayant un modèle**, sans
exiger qu'elle soit possédée. Le modèle de la H2 reste le figurant universel sans que la H2
appartienne au joueur.

C'est une correction ciblée, exigée par le changement de moto de départ — pas un
remaniement opportuniste.

---

## 6. Affichage

- **Jauge d'XP** dans la barre latérale du menu, sous le logo : niveau courant et
  progression vers le suivant. Elle s'abonne à `LevelManager.Changed`, comme le badge de
  pièces s'abonne à `EconomyManager.Changed`. La jauge est un **bouton**.
- **Pop-up de progression** (`LevelPopup`), ouverte par cette jauge, sur le modèle
  d'`InfoPopup` : un seul exemplaire par canvas, voile sombre, appui n'importe où pour
  fermer. Elle montre le niveau, l'XP restant avant le suivant, et la liste des prochains
  paliers avec ce qu'ils donnent — c'est là que le joueur voit qu'une moto l'attend au
  niveau 15.
- **Montée de niveau en jeu** : un toast, via `MissionToast` déjà en place. Il annonce, il
  ne distribue pas.
- **Récupération** au menu : coffres par le circuit existant, motos par `MotoChoiceScreen`.

---

## 7. Tests

Le projet n'a aujourd'hui aucun test ni `.asmdef`. Cette fonctionnalité est le bon endroit
pour en poser, parce que sa logique est purement calculatoire et se teste sans lancer le
jeu.

À couvrir :

- **Courbe** — `Level` déduit de `TotalXp` sur les bornes : 0 XP → niveau 1 ; 119 → 1 ;
  120 → 2 ; 7 020 → 25.
- **Franchissement multiple** — un `AddXp` qui traverse deux niveaux met bien deux
  récompenses en file.
- **Table des récompenses** — `RewardFor` sur 2..35, en vérifiant l'alternance
  bronze/argent, les paliers de moto à 5/10/15/20/25 et l'or à 30 et 35.
- **Paliers** — les trois motos de chaque palier existent au catalogue, et leur écart de
  puissance interne reste sous 25 %. Ce test protège l'équivalence contre une retouche
  distraite du catalogue.
- **Non-régression du modèle** — toute moto proposée à un palier retourne un
  `RideableModelPath()` non nul. Ce test échouerait aujourd'hui sans la correction du §5.

---

## Ce qui reste ouvert

Rien ne bloque l'implémentation. Deux points à éprouver en jeu plutôt que sur le papier :

- Le rythme réel dépend du palier moyen des missions tirées. Les 500 XP/jour sont une
  estimation ; il faudra mesurer sur une vraie semaine et ajuster les trois constantes
  d'XP, pas la courbe.
- L'équivalence des paliers est établie sur les fiches techniques. Deux motos aux chiffres
  voisins peuvent se conduire différemment une fois le contrôleur passé dessus.
