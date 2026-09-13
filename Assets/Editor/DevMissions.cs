using UnityEditor;
using UnityEngine;
using WheelingMoto.Core;
using WheelingMoto.Data;

namespace WheelingMoto.EditorTools
{
    /// <summary>
    /// Raccourcis de test du tableau de missions. Sans eux, vérifier le coffre de complétion
    /// demanderait de rouler jusqu'à boucler trois missions, et vérifier le renouvellement
    /// d'avancer l'horloge de Windows.
    ///
    /// Tout passe par l'interface publique de <see cref="MissionManager"/> : la progression forcée
    /// emprunte le même chemin que celle du jeu, donc elle verse les pièces, déclenche le bandeau et
    /// débloque le coffre exactement comme une vraie partie.
    /// </summary>
    public static class DevMissions
    {
        const string Menu = "Tools/Wheeling Moto/Missions/";

        [MenuItem(Menu + "Boucler les missions du jour", priority = 0)]
        static void CompleteDaily() => CompleteAll(MissionScope.Daily);

        [MenuItem(Menu + "Boucler les missions de la semaine", priority = 1)]
        static void CompleteWeekly() => CompleteAll(MissionScope.Weekly);

        [MenuItem(Menu + "Tout récupérer", priority = 2)]
        static void ClaimEverything()
        {
            int total = MissionManager.ClaimAll(MissionScope.Daily) + MissionManager.ClaimAll(MissionScope.Weekly);
            PlayerPrefs.Save();
            Debug.Log($"[WheelUp] {total} pièces encaissées.");
        }

        [MenuItem(Menu + "Nouveau tirage (remet tout à zéro)", priority = 20)]
        static void Reroll()
        {
            MissionManager.ResetAll();
            Debug.Log("[WheelUp] Tableau de missions retiré au sort.");
            Dump();
        }

        [MenuItem(Menu + "Afficher le tableau", priority = 21)]
        static void Show() => Dump();

        const string AchievementMenu = "Tools/Wheeling Moto/Succès/";

        /// <summary>
        /// Rejoue les compteurs à vie pour amener chaque famille au bout. Passe par les mêmes
        /// rapports que le jeu : les paliers tombent un par un, chacun avec son versement et son
        /// bandeau, et les coffres deviennent récupérables dans l'onglet.
        /// </summary>
        [MenuItem(AchievementMenu + "Débloquer tous les succès", priority = 0)]
        static void UnlockAchievements()
        {
            foreach (var achievement in AchievementCatalog.All)
            {
                float target = achievement.Targets[achievement.TierCount - 1];

                if (MissionCatalog.ValueKind(achievement.Metric) == MissionValueKind.Record)
                {
                    PlayerStats.Best(achievement.Metric, target);
                }
                else
                {
                    PlayerStats.Add(achievement.Metric, target - PlayerStats.Get(achievement.Metric));
                }
            }

            AchievementManager.Evaluate();
            PlayerPrefs.Save();

            AchievementManager.Totals(out int unlocked, out int total);
            Debug.Log($"[WheelUp] Succès : {unlocked} / {total} paliers, {AchievementManager.PendingRewards()} coffres à récupérer.");
        }

        [MenuItem(AchievementMenu + "Oublier les paliers versés", priority = 20)]
        static void ResetAchievements()
        {
            AchievementManager.ResetAll();
            Debug.Log("[WheelUp] Paliers oubliés. Les compteurs à vie sont intacts : tout sera reversé au prochain calcul.");
        }

        [MenuItem(AchievementMenu + "Effacer les compteurs à vie", priority = 21)]
        static void ResetStats()
        {
            PlayerStats.ResetAll();
            AchievementManager.ResetAll();
            Debug.Log("[WheelUp] Compteurs à vie et succès remis à zéro.");
        }

        [MenuItem(AchievementMenu + "Afficher les succès", priority = 22)]
        static void ShowAchievements()
        {
            AchievementManager.Totals(out int unlocked, out int total);
            var lines = $"[WheelUp] Succès — {unlocked} / {total} paliers";

            foreach (var achievement in AchievementCatalog.All)
            {
                lines += $"\n  {achievement.Name} : palier {AchievementManager.RewardedTier(achievement)}"
                    + $" / {achievement.TierCount}"
                    + $"  —  {MissionCatalog.FormatValue(achievement.Metric, AchievementManager.Progress(achievement))}"
                    + (AchievementManager.ChestReady(achievement) ? "  (coffre à prendre)" : string.Empty);
            }

            Debug.Log(lines);
        }

        static void CompleteAll(MissionScope scope)
        {
            foreach (var mission in MissionManager.Missions(scope))
            {
                if (mission.Completed) continue;

                if (MissionCatalog.ValueKind(mission.Metric) == MissionValueKind.Record)
                {
                    MissionManager.AdvanceBest(mission.Metric, mission.Target);
                }
                else
                {
                    MissionManager.Advance(mission.Metric, mission.Target - mission.Progress);
                }
            }

            PlayerPrefs.Save();
            Debug.Log($"[WheelUp] Missions « {Label(scope)} » bouclées. Coffre à réclamer : {MissionManager.ChestReady(scope)}.");
        }

        static void Dump()
        {
            Report(MissionScope.Daily);
            Report(MissionScope.Weekly);
        }

        static void Report(MissionScope scope)
        {
            var lines = $"[WheelUp] Missions {Label(scope)} — renouvellement dans {MissionManager.TimeUntilReset(scope):hh\\hmm}";

            foreach (var mission in MissionManager.Missions(scope))
            {
                string state = mission.Claimed ? "encaissée" : mission.Completed ? "À RÉCUPÉRER" : "en cours";
                lines += $"\n  {mission.Label}  —  {mission.ProgressLabel}  (+{mission.Coins}, {state})";
            }

            var chest = MissionManager.ChestFor(scope);
            lines += $"\n  Coffre : {(chest != null ? chest.Name : "aucun")}"
                + $" — {(MissionManager.ChestTaken(scope) ? "déjà pris" : MissionManager.ChestReady(scope) ? "à réclamer" : "verrouillé")}";

            Debug.Log(lines);
        }

        static string Label(MissionScope scope) => scope == MissionScope.Weekly ? "de la semaine" : "du jour";
    }
}
