using System;
using System.Collections.Generic;
using UnityEngine;
using WheelingMoto.Data;

namespace WheelingMoto.Core
{
    /// <summary>
    /// Progression permanente du joueur : les succès et leurs paliers. Ils se lisent entièrement
    /// dans <see cref="PlayerStats"/> — ce gestionnaire ne retient que ce qui a déjà été payé, pour
    /// ne jamais payer deux fois.
    ///
    /// Ce découpage a une conséquence utile : effacer la mémoire des paliers versés sans toucher aux
    /// compteurs reverse à nouveau tout ce qui est mérité, et ajouter un palier à un succès existant
    /// le rend immédiatement atteignable par les joueurs qui l'ont déjà dépassé sans le savoir.
    /// </summary>
    public static class AchievementManager
    {
        const string SaveKey = "achievements";

        /// <summary>Émis quand un palier est franchi ou un coffre récupéré.</summary>
        public static event Action Changed;

        static Dictionary<string, Entry> entries;

        // ---------------------------------------------------------------- lecture

        /// <summary>Avancement brut du joueur sur la grandeur suivie par ce succès.</summary>
        public static float Progress(AchievementInfo achievement) => PlayerStats.Get(achievement.Metric);

        /// <summary>Nombre de paliers dont la cible est atteinte, payés ou non.</summary>
        public static int ReachedTier(AchievementInfo achievement)
        {
            float progress = Progress(achievement);

            int reached = 0;
            for (int i = 0; i < achievement.TierCount; i++)
            {
                if (progress >= achievement.Targets[i]) reached = i + 1;
            }
            return reached;
        }

        /// <summary>Nombre de paliers déjà payés. C'est cette valeur qui est persistée.</summary>
        public static int RewardedTier(AchievementInfo achievement) => Get(achievement.Id).Tier;

        public static bool IsComplete(AchievementInfo achievement) => RewardedTier(achievement) >= achievement.TierCount;

        /// <summary>Palier en cours, borné au dernier : un succès terminé continue d'afficher sa dernière cible.</summary>
        public static int CurrentTier(AchievementInfo achievement) =>
            Mathf.Min(RewardedTier(achievement), achievement.TierCount - 1);

        /// <summary>Part du palier en cours déjà parcourue, de 0 à 1.</summary>
        public static float Ratio(AchievementInfo achievement)
        {
            if (IsComplete(achievement)) return 1f;

            int tier = CurrentTier(achievement);
            float floor = tier == 0 ? 0f : achievement.Targets[tier - 1];
            float span = achievement.Targets[tier] - floor;

            return span <= 0f ? 1f : Mathf.Clamp01((Progress(achievement) - floor) / span);
        }

        public static ChestInfo ChestFor(AchievementInfo achievement) => ChestCatalog.Find(achievement.ChestId);

        public static bool ChestTaken(AchievementInfo achievement) => Get(achievement.Id).ChestTaken;

        public static bool ChestReady(AchievementInfo achievement) =>
            IsComplete(achievement) && !ChestTaken(achievement);

        /// <summary>Coffres de succès en attente, pour la pastille de la barre latérale.</summary>
        public static int PendingRewards()
        {
            int count = 0;
            foreach (var achievement in AchievementCatalog.All)
            {
                if (ChestReady(achievement)) count++;
            }
            return count;
        }

        /// <summary>Paliers franchis sur l'ensemble des succès, pour l'en-tête de l'onglet.</summary>
        public static void Totals(out int unlocked, out int total)
        {
            unlocked = 0;
            total = 0;

            foreach (var achievement in AchievementCatalog.All)
            {
                unlocked += RewardedTier(achievement);
                total += achievement.TierCount;
            }
        }

        // ---------------------------------------------------------------- progression

        /// <summary>
        /// Verse ce qui est dû depuis le dernier passage. Appelé après chaque vidage du tampon de
        /// mesures : un joueur qui franchit deux paliers d'un coup — ce qui arrive au tout premier
        /// lancement, ou après l'ajout d'un succès — est payé pour les deux, dans l'ordre.
        /// </summary>
        public static void Evaluate()
        {
            EnsureLoaded();
            bool granted = false;

            foreach (var achievement in AchievementCatalog.All)
            {
                var entry = Get(achievement.Id);
                int reached = ReachedTier(achievement);

                while (entry.Tier < reached)
                {
                    int tier = entry.Tier;
                    entry.Tier = tier + 1;
                    granted = true;

                    EconomyManager.AddCoins(achievement.Coins[tier]);
                    RewardFeed.Raise("SUCCÈS DÉBLOQUÉ",
                        $"{achievement.Name} — {AchievementCatalog.TierLabel(tier)}", achievement.Coins[tier]);
                }
            }

            if (!granted) return;

            Save();
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Marque le coffre du succès comme pris et le rend à l'appelant. Retourne null si rien n'est
        /// à prendre, ce qui rend un double clic sans effet.
        /// </summary>
        public static ChestInfo TakeChest(AchievementInfo achievement)
        {
            if (!ChestReady(achievement)) return null;

            Get(achievement.Id).ChestTaken = true;
            Save();
            PlayerPrefs.Save();

            return ChestFor(achievement);
        }

        // ---------------------------------------------------------------- sauvegarde

        [Serializable]
        class Entry
        {
            public string Id;
            public int Tier;
            public bool ChestTaken;
        }

        [Serializable]
        class AchievementSave
        {
            public List<Entry> Entries = new List<Entry>();
        }

        static Entry Get(string id)
        {
            EnsureLoaded();

            if (!entries.TryGetValue(id, out var entry))
            {
                entry = new Entry { Id = id };
                entries[id] = entry;
            }
            return entry;
        }

        static void EnsureLoaded()
        {
            if (entries != null) return;

            entries = new Dictionary<string, Entry>();

            string json = PlayerPrefs.GetString(SaveKey, null);
            if (string.IsNullOrEmpty(json)) return;

            AchievementSave saved = null;
            try
            {
                saved = JsonUtility.FromJson<AchievementSave>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Succès illisibles, remis à zéro : " + e.Message);
            }

            if (saved?.Entries == null) return;

            foreach (var entry in saved.Entries)
            {
                // Un succès retiré du catalogue est oublié, mais on garde la ligne inoffensive plutôt
                // que de la filtrer : la réintroduire plus tard doit retrouver la progression.
                if (!string.IsNullOrEmpty(entry.Id)) entries[entry.Id] = entry;
            }
        }

        static void Save()
        {
            var saved = new AchievementSave();
            foreach (var pair in entries) saved.Entries.Add(pair.Value);

            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(saved));
            Changed?.Invoke();
        }

        /// <summary>Oublie les paliers versés et les coffres pris. Réservé aux outils de test.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            entries = null;
            Changed?.Invoke();
        }
    }
}
