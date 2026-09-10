using UnityEngine;

namespace WheelingMoto.Data
{
    /// <summary>Un lot de pièces vendu en boutique.</summary>
    public class CoinPack
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string Description;
        /// <summary>Pièces créditées avant bonus.</summary>
        public readonly int Coins;
        /// <summary>Pièces offertes en plus, mises en avant sur la carte. Zéro pour le premier lot.</summary>
        public readonly int Bonus;
        /// <summary>Prix affiché. Le vrai prix viendra du store une fois Unity IAP branché.</summary>
        public readonly string Price;
        public readonly Color Swatch;

        public int Total => Coins + Bonus;

        public CoinPack(string id, string name, string description, int coins, int bonus, string price, Color swatch)
        {
            Id = id;
            Name = name;
            Description = description;
            Coins = coins;
            Bonus = bonus;
            Price = price;
            Swatch = swatch;
        }
    }

    /// <summary>
    /// Lots de pièces de la boutique. Les identifiants servent de clé d'achat : ils devront
    /// correspondre aux références produit déclarées sur l'App Store et le Play Store, on ne les
    /// renomme donc pas une fois la boutique en ligne.
    /// </summary>
    public static class CoinPackCatalog
    {
        public static readonly CoinPack[] All =
        {
            new CoinPack("coins_small", "Poignée de pièces",
                "De quoi s'offrir une caisse bronze tout de suite.",
                1000, 0, "1,99 €", new Color(0.85f, 0.68f, 0.32f)),

            new CoinPack("coins_medium", "Sacoche de pièces",
                "Le lot d'appoint : une caisse argent et de la marge.",
                5000, 750, "6,99 €", new Color(0.78f, 0.80f, 0.84f)),

            new CoinPack("coins_large", "Coffre-fort",
                "Le meilleur rapport : plusieurs caisses or d'affilée.",
                15000, 4000, "17,99 €", new Color(0.95f, 0.80f, 0.30f)),
        };
    }
}
