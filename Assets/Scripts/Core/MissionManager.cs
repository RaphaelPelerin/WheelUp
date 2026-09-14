using System;
using System.Collections.Generic;
using UnityEngine;
using WheelingMoto.Data;

namespace WheelingMoto.Core
{
    /// <summary>
    /// Une mission en cours : le modèle tiré, le palier de difficulté retenu, et l'avancement du
    /// joueur. C'est la forme que lisent l'interface et le bandeau de jeu ; la sauvegarde, elle,
    /// n'en retient que l'identifiant, le palier et la progression.
    /// </summary>
    public class ActiveMission
    {
        public readonly MissionTemplate Template;
        public readonly int Tier;

        public float Progress;

        /// <summary>
        /// Pièces déjà encaissées par le joueur. Une mission bouclée ne paie qu'au moment où il
        /// presse « Récupérer » : c'est ce drapeau qui garantit qu'elle ne paie pas deux fois.
        /// </summary>
        public bool Claimed;

        public ActiveMission(MissionTemplate template, int tier)
        {
            Template = template;
            Tier = tier;
        }

        public MissionMetric Metric => Template.Metric;
        public float Target => Template.Targets[Tier];
        public int Coins => Template.Coins[Tier];
        public string Label => Template.Describe(Tier);
        public bool Completed => Progress >= Target;

        /// <summary>Bouclée mais pas encore encaissée : c'est l'état qui allume le bouton « Récupérer ».</summary>
        public bool Claimable => Completed && !Claimed;

        public float Ratio => Target <= 0f ? 1f : Mathf.Clamp01(Progress / Target);

        /// <summary>« 240 m / 400 m ». La progression est bornée à la cible : afficher 512 / 400 donne l'air d'un bug.</summary>
        public string ProgressLabel =>
            $"{MissionCatalog.FormatValue(Metric, Mathf.Min(Progress, Target))} / {MissionCatalog.FormatValue(Metric, Target)}";
    }

    /// <summary>
    /// Tableau de missions du joueur : trois missions du jour, trois de la semaine, renouvelées
    /// automatiquement au changement de date. C'est la boucle économique du jeu — avant lui, aucune
    /// pièce n'entrait par le jeu, seulement par l'achat en argent réel.
    ///
    /// Boucler une mission et encaisser ses pièces sont deux gestes séparés : le bandeau de jeu
    /// annonce l'objectif atteint, et le joueur vient chercher son dû dans l'onglet Missions. Cette
    /// séparation donne une raison de revenir sur l'écran et un geste de récompense à accomplir, là
    /// où un versement silencieux passait inaperçu au milieu de la conduite.
    ///
    /// Rien ne se perd si le joueur ne vient pas tout de suite : une mission bouclée reste
    /// récupérable jusqu'au renouvellement de sa période.
    ///
    /// Persistance en PlayerPrefs, comme le reste du jeu, mais en une seule clé JSON : le tableau est
    /// un état structuré, l'éclater en vingt clés « mission_2_progress » le rendrait illisible et
    /// impossible à faire évoluer.
    /// </summary>
    public static class MissionManager
    {
        const string SaveKey = "missions_board";

        /// <summary>Coffre offert quand les trois missions du jour sont bouclées.</summary>
        const string DailyChestId = "bronze";

        /// <summary>Coffre offert quand les trois missions de la semaine sont bouclées.</summary>
        const string WeeklyChestId = "argent";

        /// <summary>Émis à chaque changement du tableau : l'onglet Missions et la pastille de la barre latérale s'y abonnent.</summary>
        public static event Action Changed;

        static List<ActiveMission> daily;
        static List<ActiveMission> weekly;
        static BoardSave board;

        public static IReadOnlyList<ActiveMission> Daily
        {
            get { EnsureFresh(); return daily; }
        }

        public static IReadOnlyList<ActiveMission> Weekly
        {
            get { EnsureFresh(); return weekly; }
        }

        public static IReadOnlyList<ActiveMission> Missions(MissionScope scope) =>
            scope == MissionScope.Weekly ? Weekly : Daily;

        // ---------------------------------------------------------------- progression

        /// <summary>
        /// Ajoute une quantité à toutes les missions qui suivent cette grandeur. Les rapports venant
        /// de la conduite arrivent par paquets et non image par image : voir MissionTracker, qui
        /// tamponne avant de pousser ici.
        /// </summary>
        public static void Advance(MissionMetric metric, float amount)
        {
            if (amount <= 0f) return;
            EnsureFresh();

            bool changed = Apply(daily, metric, amount, MissionValueKind.Cumulative);
            changed |= Apply(weekly, metric, amount, MissionValueKind.Cumulative);

            if (changed) Save();
        }

        /// <summary>
        /// Signale un record : plus long wheeling, vitesse de pointe. Contrairement à
        /// <see cref="Advance"/>, la valeur remplace la progression au lieu de s'y ajouter, et
        /// seulement si elle est meilleure.
        /// </summary>
        public static void AdvanceBest(MissionMetric metric, float value)
        {
            if (value <= 0f) return;
            EnsureFresh();

            bool changed = Apply(daily, metric, value, MissionValueKind.Record);
            changed |= Apply(weekly, metric, value, MissionValueKind.Record);

            if (changed) Save();
        }

        static bool Apply(List<ActiveMission> missions, MissionMetric metric, float value, MissionValueKind kind)
        {
            bool changed = false;

            foreach (var mission in missions)
            {
                if (mission.Metric != metric) continue;
                if (MissionCatalog.ValueKind(metric) != kind) continue;

                // Une fois la cible atteinte, la mission cesse de compter : continuer à empiler
                // ferait afficher « 1 240 m / 400 m » et n'avancerait rien.
                if (mission.Completed) continue;

                if (kind == MissionValueKind.Record)
                {
                    if (value <= mission.Progress) continue;
                    mission.Progress = value;
                }
                else
                {
                    mission.Progress += value;
                }

                changed = true;
                if (mission.Completed) Announce(mission);
            }

            return changed;
        }

        /// <summary>
        /// Annonce la mission bouclée sans rien verser : les pièces attendent que le joueur les
        /// récupère. Le bandeau de jeu le dit explicitement, sinon le joueur croirait avoir déjà
        /// encaissé et ne reviendrait jamais les chercher.
        /// </summary>
        static void Announce(ActiveMission mission)
        {
            // Une mission bouclée est un acquis : on l'écrit sur le disque tout de suite plutôt que
            // d'attendre la fermeture de l'application, qui peut ne jamais venir sur mobile.
            Save();
            PlayerPrefs.Save();

            RewardFeed.Raise(
                mission.Template.Scope == MissionScope.Weekly
                    ? "MISSION DE LA SEMAINE · À RÉCUPÉRER"
                    : "MISSION ACCOMPLIE · À RÉCUPÉRER",
                mission.Label, mission.Coins, pending: true);
        }

        /// <summary>
        /// Encaisse les pièces d'une mission bouclée. Retourne false si elle ne l'est pas, ou si elle
        /// a déjà payé — ce qui rend un double appui sans effet.
        /// </summary>
        public static bool Claim(ActiveMission mission)
        {
            if (mission == null || !mission.Claimable) return false;

            mission.Claimed = true;
            EconomyManager.AddCoins(mission.Coins);

            // L'XP tombe à la récupération, pas à l'achèvement : boucler et encaisser sont deux gestes
            // séparés dans ce jeu, et c'est l'encaissement qui paie. Les deux monnaies suivent donc la
            // même règle, sans quoi le joueur monterait de niveau sans rien avoir réclamé.
            LevelManager.AddXp(LevelRewardCatalog.MissionXp(mission.Template.Scope, mission.Tier));

            Save();
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>Encaisse d'un coup tout ce qui est récupérable, et rend le total versé.</summary>
        public static int ClaimAll(MissionScope scope)
        {
            int total = 0;
            foreach (var mission in Missions(scope))
            {
                if (Claim(mission)) total += mission.Coins;
            }
            return total;
        }

        /// <summary>Missions bouclées dont les pièces dorment encore, pour la pastille de la barre latérale.</summary>
        public static int PendingClaims()
        {
            int count = 0;
            foreach (var mission in Daily)
            {
                if (mission.Claimable) count++;
            }
            foreach (var mission in Weekly)
            {
                if (mission.Claimable) count++;
            }
            return count;
        }

        // ---------------------------------------------------------------- coffres de complétion

        /// <summary>
        /// Missions dont la cible est atteinte. C'est ce compte qui remplit la jauge du coffre, et
        /// non celui des missions encaissées : la jauge suit ce que le joueur a fait, pas ce qu'il a
        /// pensé à venir chercher.
        /// </summary>
        public static int CompletedCount(MissionScope scope)
        {
            int count = 0;
            foreach (var mission in Missions(scope))
            {
                if (mission.Completed) count++;
            }
            return count;
        }

        public static bool AllCompleted(MissionScope scope)
        {
            var missions = Missions(scope);
            return missions.Count > 0 && CompletedCount(scope) == missions.Count;
        }

        /// <summary>Coffre promis par ce rythme, que le joueur l'ait mérité ou non : la carte l'annonce d'avance.</summary>
        public static ChestInfo ChestFor(MissionScope scope) =>
            ChestCatalog.Find(scope == MissionScope.Weekly ? WeeklyChestId : DailyChestId);

        public static bool ChestTaken(MissionScope scope)
        {
            EnsureFresh();
            return scope == MissionScope.Weekly ? board.WeeklyChestTaken : board.DailyChestTaken;
        }

        /// <summary>Le coffre est gagné et pas encore ouvert : l'onglet Missions affiche le bouton.</summary>
        public static bool ChestReady(MissionScope scope) => AllCompleted(scope) && !ChestTaken(scope);

        /// <summary>
        /// Marque le coffre comme pris et le rend à l'appelant, à charge pour lui de le faire ouvrir.
        /// Retourne null si rien n'est à prendre : c'est ce qui empêche un double clic de délivrer
        /// deux coffres.
        /// </summary>
        public static ChestInfo TakeChest(MissionScope scope)
        {
            if (!ChestReady(scope)) return null;

            if (scope == MissionScope.Weekly) board.WeeklyChestTaken = true;
            else board.DailyChestTaken = true;

            Save();
            PlayerPrefs.Save();
            return ChestFor(scope);
        }

        /// <summary>Nombre de coffres en attente, pour la pastille rouge de la barre latérale.</summary>
        public static int PendingRewards()
        {
            int count = 0;
            if (ChestReady(MissionScope.Daily)) count++;
            if (ChestReady(MissionScope.Weekly)) count++;
            return count;
        }

        // ---------------------------------------------------------------- renouvellement

        /// <summary>
        /// Charge le tableau si besoin et le renouvelle quand la date a tourné. Appelé au début de
        /// chaque lecture : le jeu peut rester ouvert au passage de minuit, et les missions doivent
        /// suivre sans qu'on ait à relancer l'application.
        /// </summary>
        static void EnsureFresh()
        {
            if (board == null) Load();

            var now = DateTime.Now;

            // Horloge reculée : on ne renouvelle rien. Sans ce garde-fou, reculer puis avancer la
            // date du téléphone redistribue des missions à volonté, donc des pièces à volonté.
            long ticks = now.Ticks;
            bool clockWentBack = ticks < board.LastSeenTicks;
            board.LastSeenTicks = Math.Max(board.LastSeenTicks, ticks);

            if (clockWentBack) return;

            bool rolled = false;

            string today = DayStamp(now);
            if (board.DailyStamp != today)
            {
                board.DailyStamp = today;
                board.DailyChestTaken = false;
                daily = Draw(MissionScope.Daily, today);
                rolled = true;
            }

            string thisWeek = WeekStamp(now);
            if (board.WeeklyStamp != thisWeek)
            {
                board.WeeklyStamp = thisWeek;
                board.WeeklyChestTaken = false;
                weekly = Draw(MissionScope.Weekly, thisWeek);
                rolled = true;
            }

            if (rolled) Save();
        }

        /// <summary>
        /// Tire les missions d'une période. Le tirage est semé par la date : deux appareils voient
        /// les mêmes missions le même jour, et surtout un même appareil retrouve les siennes si la
        /// sauvegarde est perdue en cours de journée.
        ///
        /// Les paliers 0, 1 et 2 sont distribués un par mission : il y a donc toujours une mission
        /// facile dans la journée, quelle que soit la difficulté des deux autres.
        /// </summary>
        static List<ActiveMission> Draw(MissionScope scope, string stamp)
        {
            var pool = MissionCatalog.Pool(scope);
            int slots = Mathf.Min(MissionCatalog.Slots(scope), pool.Length);
            var rng = new System.Random(StableHash(stamp + "|" + scope));

            var order = new List<int>(pool.Length);
            for (int i = 0; i < pool.Length; i++) order.Add(i);
            Shuffle(order, rng);

            var tiers = new List<int>(slots);
            for (int i = 0; i < slots; i++) tiers.Add(i);
            Shuffle(tiers, rng);

            var missions = new List<ActiveMission>(slots);
            for (int i = 0; i < slots; i++)
            {
                var template = pool[order[i]];
                missions.Add(new ActiveMission(template, Mathf.Clamp(tiers[i], 0, template.TierCount - 1)));
            }

            return missions;
        }

        static void Shuffle(List<int> values, System.Random rng)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        /// <summary>
        /// Empreinte stable d'une chaîne. string.GetHashCode() ne convient pas : il n'est pas garanti
        /// constant d'une exécution à l'autre, et le tirage du jour changerait à chaque lancement.
        /// </summary>
        static int StableHash(string value)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in value) hash = hash * 31 + c;
                return hash;
            }
        }

        /// <summary>
        /// Temps restant avant le renouvellement, pour l'affichage. Le jour bascule à minuit, la
        /// semaine au lundi qui suit : ce sont les mêmes bornes que celles qui servent aux empreintes
        /// ci-dessous, donc le compte à rebours ne peut pas mentir sur la date du prochain tirage.
        /// </summary>
        public static TimeSpan TimeUntilReset(MissionScope scope)
        {
            var now = DateTime.Now;

            if (scope == MissionScope.Weekly)
            {
                int daysUntilMonday = (8 - (int)now.DayOfWeek) % 7;
                if (daysUntilMonday == 0) daysUntilMonday = 7;
                return now.Date.AddDays(daysUntilMonday) - now;
            }

            return now.Date.AddDays(1) - now;
        }

        static string DayStamp(DateTime date) => date.ToString("yyyy-MM-dd");

        /// <summary>
        /// Semaine au format « 2026-S37 », lundi pour premier jour. Calculée à la main plutôt que via
        /// la culture courante : le découpage des semaines dépend des paramètres régionaux, et le
        /// tableau changerait de rythme d'un appareil à l'autre.
        /// </summary>
        static string WeekStamp(DateTime date)
        {
            int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
            var monday = date.Date.AddDays(-daysSinceMonday);
            return $"{monday:yyyy-MM-dd}-S";
        }

        // ---------------------------------------------------------------- sauvegarde

        [Serializable]
        class MissionSave
        {
            public string Id;
            public int Tier;
            public float Progress;
            public bool Claimed;
        }

        [Serializable]
        class BoardSave
        {
            public string DailyStamp;
            public string WeeklyStamp;
            public long LastSeenTicks;
            public bool DailyChestTaken;
            public bool WeeklyChestTaken;
            public List<MissionSave> Daily = new List<MissionSave>();
            public List<MissionSave> Weekly = new List<MissionSave>();
        }

        static void Load()
        {
            board = null;

            string json = PlayerPrefs.GetString(SaveKey, null);
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    board = JsonUtility.FromJson<BoardSave>(json);
                }
                catch (Exception e)
                {
                    // Sauvegarde corrompue ou écrite par une version antérieure : on repart d'un
                    // tableau neuf plutôt que de planter le menu au démarrage.
                    Debug.LogWarning("Tableau de missions illisible, remis à zéro : " + e.Message);
                    board = null;
                }
            }

            if (board == null) board = new BoardSave();

            daily = Restore(board.Daily, MissionScope.Daily);
            weekly = Restore(board.Weekly, MissionScope.Weekly);
        }

        /// <summary>
        /// Reconstruit les missions depuis la sauvegarde. Une ligne dont le modèle a disparu du
        /// catalogue est ignorée : retirer une mission d'une mise à jour à l'autre ne doit pas
        /// bloquer le joueur sur un tableau qu'il ne peut plus finir — le renouvellement suivant
        /// remplira les places vides.
        /// </summary>
        static List<ActiveMission> Restore(List<MissionSave> saved, MissionScope scope)
        {
            var missions = new List<ActiveMission>();
            if (saved == null) return missions;

            foreach (var entry in saved)
            {
                var template = MissionCatalog.Find(entry.Id);
                if (template == null || template.Scope != scope) continue;

                var mission = new ActiveMission(template, Mathf.Clamp(entry.Tier, 0, template.TierCount - 1))
                {
                    Progress = entry.Progress,
                    Claimed = entry.Claimed,
                };
                missions.Add(mission);
            }

            return missions;
        }

        static void Save()
        {
            board.Daily = Capture(daily);
            board.Weekly = Capture(weekly);

            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(board));
            Changed?.Invoke();
        }

        static List<MissionSave> Capture(List<ActiveMission> missions)
        {
            var saved = new List<MissionSave>();
            if (missions == null) return saved;

            foreach (var mission in missions)
            {
                saved.Add(new MissionSave
                {
                    Id = mission.Template.Id,
                    Tier = mission.Tier,
                    Progress = mission.Progress,
                    Claimed = mission.Claimed,
                });
            }
            return saved;
        }

        /// <summary>Remet le tableau à zéro. Réservé aux outils de test.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            PlayerPrefs.Save();
            board = null;
            EnsureFresh();
        }
    }
}
