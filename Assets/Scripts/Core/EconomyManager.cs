using System;
using UnityEngine;

namespace WheelingMoto.Core
{
    /// <summary>Monnaie du jeu, gagnée en Défis/Course, dépensée dans le Garage.</summary>
    public static class EconomyManager
    {
        const string KeyCoins = "economy_coins";

        /// <summary>Émis à chaque variation du solde : le badge de pièces du menu s'y abonne.</summary>
        public static event Action Changed;

        public static int Coins
        {
            get => PlayerPrefs.GetInt(KeyCoins, 500);
            private set
            {
                PlayerPrefs.SetInt(KeyCoins, value);
                Changed?.Invoke();
            }
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

        /// <summary>Fixe le solde directement. Réservé aux outils de test.</summary>
        public static void SetCoins(int amount)
        {
            Coins = Mathf.Max(0, amount);
            PlayerPrefs.Save();
        }
    }
}
