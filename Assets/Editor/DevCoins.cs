using UnityEditor;
using UnityEngine;
using WheelingMoto.Core;

namespace WheelingMoto.EditorTools
{
    /// <summary>
    /// Raccourcis de test : créditer des pièces sans avoir à les gagner en jeu, et remettre la
    /// progression à zéro. Fonctionne en Play mode comme à l'arrêt ; le badge du menu se met à jour
    /// tout seul puisqu'il écoute <see cref="EconomyManager.Changed"/>.
    /// </summary>
    public static class DevCoins
    {
        const string Menu = "Tools/Wheeling Moto/Pièces/";

        [MenuItem(Menu + "+1 000 _F9", priority = 0)]
        static void AddThousand() => Grant(1000);

        [MenuItem(Menu + "+10 000", priority = 1)]
        static void AddTenThousand() => Grant(10000);

        [MenuItem(Menu + "+100 000", priority = 2)]
        static void AddHundredThousand() => Grant(100000);

        [MenuItem(Menu + "Remettre à 500 (solde de départ)", priority = 20)]
        static void ResetBalance()
        {
            EconomyManager.SetCoins(500);
            Debug.Log("[WheelUp] Solde remis à 500 pièces.");
        }

        [MenuItem(Menu + "Effacer toute la progression...", priority = 21)]
        static void WipeProgress()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Effacer toute la progression",
                "Supprime les pièces, les motos possédées, les améliorations, les peintures et les réglages. "
                + "Cette action est irréversible.",
                "Effacer", "Annuler");

            if (!confirmed) return;

            PlayerPrefs.DeleteAll();
            EconomyManager.SetCoins(500); // réécrit le solde de départ et rafraîchit le badge
            Debug.Log("[WheelUp] Progression effacée, solde remis à 500 pièces.");
        }

        static void Grant(int amount)
        {
            EconomyManager.AddCoins(amount);
            PlayerPrefs.Save();
            Debug.Log($"[WheelUp] +{amount} pièces — nouveau solde : {EconomyManager.Coins}.");
        }
    }
}
