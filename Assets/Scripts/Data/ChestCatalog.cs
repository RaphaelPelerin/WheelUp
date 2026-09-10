using UnityEngine;

namespace WheelingMoto.Data
{
    public enum ChestRewardKind
    {
        Coins,
        Moto,
        Upgrade,
        Paint,
    }

    public enum ChestRarity
    {
        Commun,
        Rare,
        Epique,
        Legendaire,
    }

    /// <summary>Une entrée de la table de butin : un type de gain, son poids de tirage et son intervalle.</summary>
    public class ChestLootEntry
    {
        public readonly ChestRewardKind Kind;
        public readonly int Weight;
        public readonly int MinAmount;
        public readonly int MaxAmount;
        public readonly ChestRarity Rarity;

        public ChestLootEntry(ChestRewardKind kind, int weight, int minAmount, int maxAmount, ChestRarity rarity)
        {
            Kind = kind;
            Weight = weight;
            MinAmount = minAmount;
            MaxAmount = maxAmount;
            Rarity = rarity;
        }
    }

    public class ChestInfo
    {
        public readonly string Id;
        public readonly string Name;
        public readonly int Price;
        public readonly string Description;
        public readonly ChestLootEntry[] Loot;

        /// <summary>
        /// Nombre de lots délivrés par le coffre. Le premier est toujours des pièces : un coffre ne
        /// doit jamais paraître vide. Les suivants sont tirés dans la table de butin.
        /// </summary>
        public readonly int RewardCount;

        /// <summary>Modèles sous Assets/Resources, sans extension. Absents : l'animation se joue en UI seule.</summary>
        public string ClosedModelPath => $"Chests/chest_{Id}_closed";
        public string OpenModelPath => $"Chests/chest_{Id}_open";

        public ChestInfo(string id, string name, int price, int rewardCount, string description, ChestLootEntry[] loot)
        {
            Id = id;
            Name = name;
            Price = price;
            RewardCount = rewardCount;
            Description = description;
            Loot = loot;
        }

        /// <summary>Nombre de lots réellement tirés au sort (le lot de pièces garanti ne l'est pas).</summary>
        public int DrawnCount => Mathf.Max(0, RewardCount - 1);

    }

    /// <summary>
    /// Coffres achetables avec la monnaie du jeu. Les poids sont relatifs au total de chaque coffre :
    /// plus le coffre est cher, plus la part de motos et de peintures rares augmente.
    /// </summary>
    public static class ChestCatalog
    {
        public static readonly ChestInfo[] All =
        {
            new ChestInfo("bronze", "Caisse Bronze", 500, 3,
                "Trois lots : des pièces, souvent une amélioration.",
                new[]
                {
                    new ChestLootEntry(ChestRewardKind.Coins, 580, 150, 450, ChestRarity.Commun),
                    new ChestLootEntry(ChestRewardKind.Upgrade, 300, 1, 1, ChestRarity.Rare),
                    new ChestLootEntry(ChestRewardKind.Paint, 120, 1, 1, ChestRarity.Rare),
                }),

            new ChestInfo("argent", "Caisse Argent", 1500, 3,
                "Trois lots : améliorations, peintures, et une chance de moto.",
                new[]
                {
                    new ChestLootEntry(ChestRewardKind.Coins, 450, 500, 1400, ChestRarity.Commun),
                    new ChestLootEntry(ChestRewardKind.Upgrade, 330, 1, 2, ChestRarity.Rare),
                    new ChestLootEntry(ChestRewardKind.Paint, 200, 1, 1, ChestRarity.Epique),
                    new ChestLootEntry(ChestRewardKind.Moto, 20, 1, 1, ChestRarity.Legendaire),
                }),

            new ChestInfo("or", "Caisse Or", 4000, 4,
                "Quatre lots, et la meilleure chance de décrocher une moto.",
                new[]
                {
                    new ChestLootEntry(ChestRewardKind.Coins, 340, 1500, 3500, ChestRarity.Rare),
                    new ChestLootEntry(ChestRewardKind.Upgrade, 350, 2, 3, ChestRarity.Epique),
                    new ChestLootEntry(ChestRewardKind.Paint, 280, 1, 1, ChestRarity.Epique),
                    new ChestLootEntry(ChestRewardKind.Moto, 30, 1, 1, ChestRarity.Legendaire),
                }),
        };

        public static Color RarityColor(ChestRarity rarity)
        {
            switch (rarity)
            {
                case ChestRarity.Rare: return new Color(0.30f, 0.60f, 0.95f);
                case ChestRarity.Epique: return new Color(0.62f, 0.35f, 0.90f);
                case ChestRarity.Legendaire: return new Color(0.98f, 0.76f, 0.24f);
                default: return new Color(0.70f, 0.72f, 0.78f);
            }
        }

        /// <summary>
        /// Couleur des particules d'ouverture. Volontairement éloignée de <see cref="RarityColor"/> :
        /// le halo de fond porte déjà la teinte de la rareté, des étincelles de la même couleur s'y
        /// noient complètement. Violet appelle du jaune, or appelle du rouge, bleu appelle du violet.
        /// </summary>
        public static Color ConfettiColor(ChestRarity rarity)
        {
            switch (rarity)
            {
                case ChestRarity.Rare: return new Color(0.80f, 0.38f, 0.99f);       // sur fond bleu
                case ChestRarity.Epique: return new Color(0.99f, 0.88f, 0.26f);     // sur fond violet
                // Rouge tire vers le carmin : sur fond or, un rouge orangé n'était qu'à 37° de teinte.
                case ChestRarity.Legendaire: return new Color(0.97f, 0.15f, 0.28f);
                default: return new Color(0.99f, 0.63f, 0.20f);                     // sur fond gris
            }
        }

        public static string RarityLabel(ChestRarity rarity)
        {
            switch (rarity)
            {
                case ChestRarity.Rare: return "RARE";
                case ChestRarity.Epique: return "ÉPIQUE";
                case ChestRarity.Legendaire: return "LÉGENDAIRE";
                default: return "COMMUN";
            }
        }
    }
}
