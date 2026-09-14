using System;
using System.Collections.Generic;
using UnityEngine;
using WheelingMoto.Data;

namespace WheelingMoto.Core
{
    /// <summary>
    /// Niveau du pilote : la seule progression du jeu qui ne redescend jamais et qui ouvre du
    /// contenu sans le faire payer. Les missions et les prouesses la nourrissent, les paliers la
    /// récompensent — une moto au choix tous les cinq niveaux jusqu'au vingt-cinquième, des coffres
    /// entre les paliers puis de l'or ensuite (voir <see cref="LevelRewardCatalog"/>).
    ///
    /// Un seul nombre est sauvegardé : l'XP total. Le niveau s'en déduit par la courbe. Stocker les
    /// deux séparément les laisserait diverger dès qu'une sauvegarde partirait de travers, et c'est
    /// la panne classique de ce genre de système : un joueur niveau 12 avec l'XP d'un niveau 3.
    ///
    /// Les récompenses gagnées ne sont pas distribuées ici. Monter de niveau peut arriver au milieu
    /// d'un wheeling, et on ne coupe pas une partie pour présenter un écran de choix : le palier
    /// reste dû, et le joueur va le chercher lui-même sur la route des paliers quand il veut.
    /// </summary>
    public static class LevelManager
    {
        const string KeyTotalXp = "level_total_xp";

        /// <summary>Dernier palier dont le joueur a réellement encaissé la récompense.</summary>
        const string KeyClaimedThrough = "level_claimed_through";

        // Anciennes files de récompenses dues, lues une dernière fois par Migrate puis effacées.
        const string KeyPendingChoices = "level_pending_choices";
        const string KeyPendingChests = "level_pending_chests";

        static bool migrated;

        /// <summary>Coût du premier niveau, en XP. La suite s'en déduit par <see cref="LevelCostStep"/>.</summary>
        public const int FirstLevelCost = 120;

        /// <summary>Ce que chaque niveau coûte de plus que le précédent.</summary>
        public const int LevelCostStep = 15;

        /// <summary>
        /// Niveau maximal du jeu, et bout de la route des paliers. Une fois atteint, l'XP continue
        /// d'entrer mais ne fait plus monter : la jauge reste pleine et plus aucune récompense n'est
        /// due. Il sert aussi de garde-fou à la boucle de <see cref="Level"/>, qu'une sauvegarde
        /// corrompue ne doit pas faire tourner sans fin.
        /// </summary>
        public const int MaxLevel = 100;

        /// <summary>Émis à chaque gain d'XP : la jauge du menu et la pop-up de progression s'y abonnent.</summary>
        public static event Action Changed;

        public static int TotalXp
        {
            get => PlayerPrefs.GetInt(KeyTotalXp, 0);
            private set => PlayerPrefs.SetInt(KeyTotalXp, Mathf.Max(0, value));
        }

        /// <summary>Niveau atteint, déduit de l'XP total. Commence à 1, jamais à 0 : on ne joue pas au niveau zéro.</summary>
        public static int Level
        {
            get
            {
                int xp = TotalXp;
                int level = 1;
                while (level < MaxLevel && xp >= XpToReach(level + 1)) level++;
                return level;
            }
        }

        /// <summary>XP cumulé qu'il faut avoir amassé pour atteindre <paramref name="level"/>.</summary>
        public static int XpToReach(int level)
        {
            // Somme des coûts de chaque niveau franchi : FirstLevelCost par palier, plus LevelCostStep
            // qui s'ajoute une fois de plus à chaque fois. Écrit sous forme close plutôt qu'en boucle,
            // parce que la jauge l'appelle à chaque image.
            int steps = Mathf.Max(0, level - 1);
            return FirstLevelCost * steps + LevelCostStep * steps * (steps - 1) / 2;
        }

        /// <summary>Vrai une fois le plafond atteint : il n'y a plus de niveau suivant à viser.</summary>
        public static bool IsMaxLevel => Level >= MaxLevel;

        /// <summary>
        /// Coût du niveau en cours, en XP : la longueur totale de la jauge affichée. Zéro au plafond,
        /// où il n'y a plus de palier suivant à payer.
        /// </summary>
        public static int XpForNextLevel
        {
            get
            {
                int level = Level;
                return level >= MaxLevel ? 0 : XpToReach(level + 1) - XpToReach(level);
            }
        }

        /// <summary>XP déjà acquis dans le niveau en cours : le remplissage de la jauge.</summary>
        public static int XpIntoLevel => TotalXp - XpToReach(Level);

        /// <summary>Remplissage de la jauge, de 0 à 1. Pleine au plafond.</summary>
        public static float Ratio
        {
            get
            {
                if (IsMaxLevel) return 1f;

                int span = XpForNextLevel;
                return span <= 0 ? 1f : Mathf.Clamp01((float)XpIntoLevel / span);
            }
        }

        /// <summary>
        /// Verse de l'XP et met en attente tout ce que les niveaux franchis rapportent.
        ///
        /// Le franchissement multiple est traité : un versement généreux — trois missions récupérées
        /// d'affilée, une session de prouesses exceptionnelle — peut passer deux niveaux d'un coup, et
        /// les deux récompenses sont dues. Les compter une seule fois ferait disparaître un coffre
        /// sans que personne ne s'en aperçoive.
        /// </summary>
        public static void AddXp(int amount)
        {
            if (amount <= 0) return;

            int before = Level;
            TotalXp += amount;
            int after = Level;

            for (int level = before + 1; level <= after; level++)
            {
                Announce(level, LevelRewardCatalog.RewardFor(level));
            }

            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>
        /// Annonce la montée sur le canal commun des récompenses. Le bandeau de conduite et le bilan
        /// de session l'affichent sans rien savoir des niveaux, exactement comme ils affichent déjà
        /// missions et succès — c'est ce que RewardFeed existe pour permettre.
        ///
        /// Annoncée « en attente », parce qu'elle l'est : la montée se voit tout de suite, la moto ou
        /// le coffre se récupère au menu.
        /// </summary>
        static void Announce(int level, LevelReward reward)
        {
            string label = string.Empty;
            if (reward != null)
            {
                label = reward.IsMotoChoice
                    ? "Moto au choix"
                    : ChestCatalog.Find(reward.ChestId)?.Name ?? reward.ChestId;
            }

            RewardFeed.Raise($"NIVEAU {level}", label, 0, pending: true);
        }

        // ---------------------------------------------------------------- récompenses à récupérer

        /// <summary>
        /// Dernier palier encaissé. Tout ce qui se trouve entre lui et <see cref="Level"/> est dû au
        /// joueur et l'attend sur la route des paliers.
        ///
        /// Un seul nombre là encore, et pour la raison qui fait déjà ne stocker que l'XP : ce qui est
        /// dû se déduit du niveau et de ce compteur, au lieu d'être recopié dans une file qu'il
        /// faudrait tenir d'accord avec le niveau. Les deux finissent toujours par diverger, et le
        /// joueur se retrouve avec un coffre dû que plus aucun palier ne réclame.
        ///
        /// Conséquence voulue : les paliers se récupèrent dans l'ordre. On ne saute pas le coffre du
        /// huitième pour aller chercher la moto du dixième.
        /// </summary>
        public static int ClaimedThrough
        {
            get
            {
                Migrate();

                // Borné par le niveau : une sauvegarde qui aurait pris de l'avance se répare d'elle-
                // même, au lieu de rendre déjà encaissés des paliers que le joueur n'a pas atteints.
                return Mathf.Clamp(PlayerPrefs.GetInt(KeyClaimedThrough, 1), 1, Level);
            }
        }

        /// <summary>Nombre de récompenses qui attendent le joueur.</summary>
        public static int ClaimableCount => Mathf.Max(0, Level - ClaimedThrough);

        /// <summary>Vrai si quelque chose attend d'être récupéré.</summary>
        public static bool HasPendingRewards => ClaimableCount > 0;

        /// <summary>Palier prêt à être récupéré, ou zéro s'il n'y en a aucun.</summary>
        public static int NextClaimable
        {
            get
            {
                int claimed = ClaimedThrough;
                return claimed < Level ? claimed + 1 : 0;
            }
        }

        /// <summary>
        /// Vrai si ce palier est atteint et pas encore encaissé : c'est ce que la route interroge
        /// pour savoir quel nœud porte un bouton.
        /// </summary>
        public static bool IsClaimable(int level) => level > ClaimedThrough && level <= Level;

        /// <summary>
        /// Enregistre un palier comme encaissé. Appelé une fois la récompense réellement remise —
        /// moto équipée, coffre ouvert — et jamais avant : tant que le joueur n'a rien reçu, fermer
        /// l'application doit lui rendre son dû.
        /// </summary>
        public static void Claim(int level)
        {
            // Seul le prochain palier dû s'encaisse. Accepter un palier plus loin avancerait le
            // compteur par-dessus tout ce qui le précède, et le joueur perdrait les coffres du chemin
            // sans que rien ne le lui dise.
            if (level != NextClaimable) return;

            PlayerPrefs.SetInt(KeyClaimedThrough, level);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        // ------------------------------------------------------------------------------ outillage

        /// <summary>Remet la progression à zéro. Réservé aux outils de test.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(KeyTotalXp);
            PlayerPrefs.DeleteKey(KeyClaimedThrough);
            PlayerPrefs.DeleteKey(KeyPendingChoices);
            PlayerPrefs.DeleteKey(KeyPendingChests);
            PlayerPrefs.Save();
            migrated = false;
            Changed?.Invoke();
        }

        /// <summary>
        /// Convertit les anciennes files de récompenses dues en compteur de paliers encaissés. Les
        /// files ne retenaient pas de quel palier venait chaque lot : on recule donc d'autant de
        /// paliers qu'il restait d'entrées, ce qui rend au joueur exactement son dû.
        ///
        /// Sans file ni compteur, la progression est considérée à jour. Partir de un rendrait
        /// réclamables tous les paliers encaissés depuis la première partie.
        /// </summary>
        static void Migrate()
        {
            if (migrated) return;
            migrated = true;

            bool hasQueues = PlayerPrefs.HasKey(KeyPendingChoices) || PlayerPrefs.HasKey(KeyPendingChests);
            bool hasCounter = PlayerPrefs.HasKey(KeyClaimedThrough);
            if (hasCounter && !hasQueues) return;

            int owed = Read(KeyPendingChoices).Count + Read(KeyPendingChests).Count;
            int claimed = hasCounter
                ? PlayerPrefs.GetInt(KeyClaimedThrough, 1)
                : Mathf.Max(1, Level - owed);

            PlayerPrefs.SetInt(KeyClaimedThrough, claimed);
            PlayerPrefs.DeleteKey(KeyPendingChoices);
            PlayerPrefs.DeleteKey(KeyPendingChests);
            PlayerPrefs.Save();
        }

        // Les anciennes files tenaient dans une chaîne séparée par des virgules. Seule la migration
        // les lit encore.
        static List<string> Read(string key)
        {
            var list = new List<string>();
            string raw = PlayerPrefs.GetString(key, string.Empty);
            if (string.IsNullOrEmpty(raw)) return list;

            foreach (string part in raw.Split(','))
            {
                if (!string.IsNullOrEmpty(part)) list.Add(part);
            }
            return list;
        }
    }
}
