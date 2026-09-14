using UnityEngine;

namespace WheelingMoto.Data
{
    /// <summary>Poste d'amélioration achetable, et ce qu'il change sur la fiche technique, par niveau.</summary>
    public class UpgradeSlot
    {
        public readonly string Id;
        public readonly string Label;
        public readonly string Effect;

        /// <summary>Puissance en plus, en part de la puissance d'origine (0,04 : +4 %).</summary>
        public readonly float PowerShare;
        /// <summary>Adhérence en plus au démarrage et au freinage, en part de l'origine.</summary>
        public readonly float GripShare;
        /// <summary>Poids en moins, en part du poids d'origine.</summary>
        public readonly float WeightShare;
        /// <summary>Maniabilité en plus, en points sur 10.</summary>
        public readonly float HandlingGain;
        /// <summary>Zone d'équilibre élargie, en degrés.</summary>
        public readonly float BalanceWidthGain;

        public UpgradeSlot(string id, string label, string effect, float powerShare = 0f, float gripShare = 0f,
            float weightShare = 0f, float handlingGain = 0f, float balanceWidthGain = 0f)
        {
            Id = id;
            Label = label;
            Effect = effect;
            PowerShare = powerShare;
            GripShare = gripShare;
            WeightShare = weightShare;
            HandlingGain = handlingGain;
            BalanceWidthGain = balanceWidthGain;
        }
    }

    public static class MotoUpgrades
    {
        public const int MaxLevel = 5;

        /// <summary>Au niveau maximal : +20 % de puissance, +15 % d'adhérence, -15 % de poids, zone d'équilibre bien plus large.</summary>
        public static readonly UpgradeSlot[] Slots =
        {
            new UpgradeSlot("engine", "Moteur", "Accélération et vitesse de pointe", powerShare: 0.04f),
            new UpgradeSlot("suspension", "Suspension", "Stabilité au point d'équilibre", handlingGain: 0.2f, balanceWidthGain: 0.6f),
            new UpgradeSlot("tires", "Pneus", "Accroche et vitesse en virage", gripShare: 0.03f, handlingGain: 0.4f),
            new UpgradeSlot("weight", "Allègement", "Reprises et facilité à cabrer", weightShare: 0.03f, handlingGain: 0.2f, balanceWidthGain: 0.3f),
        };

        /// <summary>
        /// Coût du passage au niveau demandé. Indexé sur le prix de la moto : préparer une 50cc reste
        /// abordable, pousser une hypersport au maximum est un objectif de fin de progression.
        /// </summary>
        public static int CostForLevel(MotoInfo moto, int level)
        {
            if (moto == null || level < 1 || level > MaxLevel) return 0;

            float basis = 250f + moto.Price * 0.10f;
            return Mathf.RoundToInt(basis * level / 50f) * 50;
        }

        public static UpgradeSlot Find(string slotId)
        {
            return System.Array.Find(Slots, s => s.Id == slotId);
        }
    }
}
