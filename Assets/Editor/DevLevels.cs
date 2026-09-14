using UnityEditor;
using UnityEngine;
using WheelingMoto.Core;
using WheelingMoto.Data;

namespace WheelingMoto.EditorTools
{
    /// <summary>
    /// Raccourcis de test de la progression par niveaux. Sans eux, éprouver le palier du niveau 25
    /// demanderait les 7 020 XP du parcours complet, soit deux semaines de jeu quotidien : les
    /// écrans de choix de moto ne seraient jamais vus avant la sortie.
    ///
    /// Les récompenses franchies ne sont pas distribuées ici, pas plus qu'en jeu : monter de niveau
    /// rend un palier récupérable, et le joueur va le chercher sur la route des paliers. Sauter au
    /// niveau 25 depuis zéro laisse donc vingt-quatre paliers dus, à encaisser dans l'ordre — ce qui
    /// est précisément le cas limite qu'il faut savoir éprouver.
    /// </summary>
    public static class DevLevels
    {
        const string Menu = "Tools/Wheeling Moto/Niveaux/";

        [MenuItem(Menu + "+500 XP", priority = 0)]
        static void AddSmall() => Grant(500);

        [MenuItem(Menu + "+2 000 XP", priority = 1)]
        static void AddLarge() => Grant(2000);

        [MenuItem(Menu + "Monter d'un niveau", priority = 2)]
        static void NextLevel()
        {
            if (LevelManager.IsMaxLevel)
            {
                Debug.LogWarning($"[WheelUp] Déjà au niveau maximal ({LevelManager.MaxLevel}).");
                return;
            }

            int missing = LevelManager.XpForNextLevel - LevelManager.XpIntoLevel;
            Grant(Mathf.Max(1, missing));
        }

        [MenuItem(Menu + "Aller au niveau 5 — 1er palier moto", priority = 20)]
        static void GoToFirstTier() => GoTo(LevelRewardCatalog.LevelsPerTier);

        [MenuItem(Menu + "Aller au niveau 25 — dernier palier moto", priority = 21)]
        static void GoToLastTier() => GoTo(LevelRewardCatalog.LastMotoLevel);

        [MenuItem(Menu + "Aller au niveau 30 — 1er coffre or", priority = 22)]
        static void GoToFirstGold() => GoTo(LevelRewardCatalog.LastMotoLevel + LevelRewardCatalog.LevelsPerTier);

        [MenuItem(Menu + "Remettre la progression de niveau à zéro", priority = 40)]
        static void Reset()
        {
            LevelManager.ResetAll();
            Debug.Log("[WheelUp] Progression de niveau effacée — retour au niveau 1, rien d'encaissé.");
        }

        [MenuItem(Menu + "Afficher l'état", priority = 41)]
        static void Dump()
        {
            int next = LevelManager.NextClaimable;

            Debug.Log(
                $"[WheelUp] Niveau {LevelManager.Level} — {LevelManager.XpIntoLevel} / "
                + $"{LevelManager.XpForNextLevel} XP dans le niveau, {LevelManager.TotalXp} XP au total. "
                + $"Encaissé jusqu'au palier {LevelManager.ClaimedThrough}, "
                + $"{LevelManager.ClaimableCount} en attente sur la route. "
                + $"Prochain à récupérer : {(next == 0 ? "aucun" : Describe(next))}");
        }

        /// <summary>Ce que donne un palier, en clair, pour l'affichage d'état.</summary>
        static string Describe(int level)
        {
            var reward = LevelRewardCatalog.RewardFor(level);
            if (reward == null) return $"niveau {level}";

            return reward.IsMotoChoice
                ? $"niveau {level} — moto au choix, palier {reward.MotoTier}"
                : $"niveau {level} — coffre {reward.ChestId}";
        }

        /// <summary>
        /// Amène au niveau visé sans jamais retirer d'XP : redescendre laisserait le compteur des
        /// paliers encaissés au-dessus du niveau atteint, donc une progression incohérente. Pour
        /// revenir en arrière, il faut passer par la remise à zéro.
        /// </summary>
        static void GoTo(int level)
        {
            int missing = LevelManager.XpToReach(level) - LevelManager.TotalXp;
            if (missing <= 0)
            {
                Debug.LogWarning(
                    $"[WheelUp] Déjà au niveau {LevelManager.Level} : le niveau {level} est derrière. "
                    + "Passe par « Remettre la progression de niveau à zéro » pour y revenir.");
                return;
            }

            Grant(missing);
        }

        static void Grant(int amount)
        {
            LevelManager.AddXp(amount);
            PlayerPrefs.Save();
            Debug.Log($"[WheelUp] +{amount} XP — niveau {LevelManager.Level}, {LevelManager.TotalXp} XP au total.");
        }
    }
}
