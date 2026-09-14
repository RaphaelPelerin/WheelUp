using System.Collections.Generic;
using UnityEngine;
using WheelingMoto.Data;

namespace WheelingMoto.Core
{
    /// <summary>
    /// Bilan d'une session de conduite : ce que le joueur a fait entre le lancement de la partie et
    /// son retour au menu. Sert uniquement à l'écran de récapitulatif ; les missions, elles, gardent
    /// leur propre avancement sur la journée et la semaine.
    /// </summary>
    public class RunStats
    {
        readonly Dictionary<MissionMetric, float> values = new Dictionary<MissionMetric, float>();

        /// <summary>
        /// Récompenses tombées pendant cette session, dans l'ordre. Missions et succès confondus :
        /// le bilan les présente de la même façon, et rien ne justifie de les distinguer ici.
        /// </summary>
        public readonly List<RewardNotice> Earned = new List<RewardNotice>();

        /// <summary>Pièces réellement créditées pendant la session : les paliers de succès, qui paient sur-le-champ.</summary>
        public int CoinsEarned;

        /// <summary>Pièces des missions bouclées, qui attendent d'être récupérées dans le menu.</summary>
        public int CoinsPending;

        /// <summary>Points de prouesse de la session, tenus à jour par le collecteur.</summary>
        public int StuntPoints;

        /// <summary>XP tiré de ces points à la clôture de la session.</summary>
        public int XpEarned;

        public float Get(MissionMetric metric) => values.TryGetValue(metric, out float value) ? value : 0f;

        public void Add(MissionMetric metric, float amount) => values[metric] = Get(metric) + amount;

        public void Best(MissionMetric metric, float value)
        {
            if (value > Get(metric)) values[metric] = value;
        }

        /// <summary>Vrai si la session mérite un récapitulatif : sortir aussitôt après être entré n'en mérite pas.</summary>
        public bool Meaningful => Get(MissionMetric.Distance) > 20f || Earned.Count > 0;
    }

    /// <summary>
    /// Point d'entrée unique du gameplay vers les missions. Le contrôleur de moto et les menus ne
    /// connaissent que cette classe : ils rapportent des grandeurs, ils ignorent qu'il existe un
    /// tableau de missions, des paliers ou des pièces.
    ///
    /// Les rapports de conduite tombent à chaque image — distance, temps d'équilibre. Les pousser
    /// tels quels dans <see cref="MissionManager"/> sérialiserait le tableau soixante fois par
    /// seconde. Ils sont donc tamponnés ici et vidés par paquets (voir <see cref="Flush"/>), ce qui
    /// ramène l'écriture à deux fois par seconde sans que le joueur voie la différence.
    /// </summary>
    public static class MissionTracker
    {
        static readonly Dictionary<MissionMetric, float> pendingTotals = new Dictionary<MissionMetric, float>();
        static readonly Dictionary<MissionMetric, float> pendingBests = new Dictionary<MissionMetric, float>();

        static MissionTracker()
        {
            // Abonnement permanent au canal de récompenses : c'est le tracker qui sait si une session
            // est en cours, donc lui qui peut ranger le gain dans le bilan à montrer au retour au menu.
            RewardFeed.Granted += OnRewardGranted;
        }

        /// <summary>Session en cours, ou null hors partie.</summary>
        public static RunStats CurrentRun { get; private set; }

        /// <summary>Dernier bilan clos, gardé le temps que l'écran de récapitulatif l'affiche.</summary>
        public static RunStats LastRun { get; private set; }

        public static void BeginRun()
        {
            pendingTotals.Clear();
            pendingBests.Clear();
            CurrentRun = new RunStats();

            // Une partie lancée compte immédiatement : le joueur ne doit pas avoir à rouler pour que
            // « lance 3 parties » avance.
            ReportEvent(MissionMetric.SessionsPlayed);
        }

        /// <summary>
        /// Recopie le total de prouesses de la session en cours. Appelé pendant la conduite plutôt
        /// qu'à la clôture : le collecteur disparaît avec la scène, et à ce moment-là le bilan est
        /// déjà figé.
        /// </summary>
        public static void ReportStuntTotal(int points)
        {
            if (CurrentRun != null) CurrentRun.StuntPoints = points;
        }

        /// <summary>Clôt la session, vide le tampon et rend le bilan à afficher.</summary>
        public static RunStats EndRun()
        {
            Flush();
            PlayerPrefs.Save();

            LastRun = CurrentRun;
            CurrentRun = null;
            return LastRun;
        }

        /// <summary>Ajoute une quantité à une grandeur cumulative (distance, durée, compteur).</summary>
        public static void Report(MissionMetric metric, float amount)
        {
            if (amount <= 0f || float.IsNaN(amount)) return;

            pendingTotals.TryGetValue(metric, out float current);
            pendingTotals[metric] = current + amount;
            CurrentRun?.Add(metric, amount);
        }

        /// <summary>Signale un record : plus long wheeling, vitesse de pointe.</summary>
        public static void ReportBest(MissionMetric metric, float value)
        {
            if (value <= 0f || float.IsNaN(value)) return;

            pendingBests.TryGetValue(metric, out float current);
            if (value > current) pendingBests[metric] = value;
            CurrentRun?.Best(metric, value);
        }

        /// <summary>
        /// Rapporte un fait isolé et le pousse tout de suite : coffre ouvert, amélioration achetée.
        /// Ces événements viennent du menu, où il n'y a pas de boucle de jeu pour vider le tampon.
        /// </summary>
        public static void ReportEvent(MissionMetric metric, float amount = 1f)
        {
            Report(metric, amount);
            Flush();
        }

        /// <summary>
        /// Pousse le tampon vers les deux systèmes de progression : le tableau du jour, qui se
        /// renouvelle, et les compteurs à vie, qui portent les succès. Une même mesure ne traverse le
        /// gameplay qu'une fois et alimente les deux — c'est tout l'intérêt de passer par ici.
        /// </summary>
        public static void Flush()
        {
            bool pushed = false;

            if (pendingTotals.Count > 0)
            {
                foreach (var pair in pendingTotals)
                {
                    MissionManager.Advance(pair.Key, pair.Value);
                    PlayerStats.Add(pair.Key, pair.Value);
                }
                pendingTotals.Clear();
                pushed = true;
            }

            if (pendingBests.Count > 0)
            {
                foreach (var pair in pendingBests)
                {
                    MissionManager.AdvanceBest(pair.Key, pair.Value);
                    PlayerStats.Best(pair.Key, pair.Value);
                }
                pendingBests.Clear();
                pushed = true;
            }

            // Évalué après coup, une seule fois : les paliers se lisent dans les compteurs à vie, qui
            // viennent d'être mis à jour.
            if (pushed) AchievementManager.Evaluate();
        }

        static void OnRewardGranted(RewardNotice notice)
        {
            if (CurrentRun == null) return;

            CurrentRun.Earned.Add(notice);

            if (notice.Pending) CurrentRun.CoinsPending += notice.Coins;
            else CurrentRun.CoinsEarned += notice.Coins;
        }
    }
}
