using UnityEngine;

namespace WheelingMoto.Core
{
    /// <summary>Motos possédées et niveaux d'amélioration achetés, persistés via PlayerPrefs.</summary>
    public static class GarageOwnership
    {
        public static bool IsOwned(string motoName) => PlayerPrefs.GetInt("owned_" + motoName, 0) == 1;

        public static void SetOwned(string motoName) => PlayerPrefs.SetInt("owned_" + motoName, 1);

        public static int GetUpgradeLevel(string motoName, string slotId)
        {
            return PlayerPrefs.GetInt($"upgrade_{motoName}_{slotId}", 0);
        }

        public static void SetUpgradeLevel(string motoName, string slotId, int level)
        {
            PlayerPrefs.SetInt($"upgrade_{motoName}_{slotId}", level);
        }
    }
}
