using UnityEngine;

namespace WheelingMoto.Core
{
    /// <summary>Monnaie du jeu, gagnée en Défis/Course, dépensée dans le Garage.</summary>
    public static class EconomyManager
    {
        const string KeyCoins = "economy_coins";

        public static int Coins
        {
            get => PlayerPrefs.GetInt(KeyCoins, 500);
            private set => PlayerPrefs.SetInt(KeyCoins, value);
        }

        public static void AddCoins(int amount)
        {
            if (amount <= 0) return;
            Coins += amount;
        }

        public static bool SpendCoins(int amount)
        {
            if (amount <= 0 || Coins < amount) return false;
            Coins -= amount;
            return true;
        }
    }
}
