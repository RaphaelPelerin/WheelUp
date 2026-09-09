using UnityEngine;

namespace WheelingMoto.Core
{
    /// <summary>Motos possédées et niveau de préparation moteur, persistés via PlayerPrefs.</summary>
    public static class GarageOwnership
    {
        public static bool IsOwned(string motoName) => PlayerPrefs.GetInt("owned_" + motoName, 0) == 1;

        public static void SetOwned(string motoName) => PlayerPrefs.SetInt("owned_" + motoName, 1);

        public static int GetTuningLevel(string motoName) => PlayerPrefs.GetInt("tuning_" + motoName, 1);

        public static void SetTuningLevel(string motoName, int level) => PlayerPrefs.SetInt("tuning_" + motoName, level);
    }
}
