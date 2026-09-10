using UnityEngine;

namespace WheelingMoto.Data
{
    /// <summary>Poste d'amélioration achetable, et statistique qu'il fait progresser.</summary>
    public class UpgradeSlot
    {
        public readonly string Id;
        public readonly string Label;
        public readonly string Effect;

        /// <summary>Gain par niveau, appliqué aux stats de base (échelle sur 10).</summary>
        public readonly float PowerGain;
        public readonly float HandlingGain;
        public readonly float WheelieGain;

        public UpgradeSlot(string id, string label, string effect, float powerGain, float handlingGain, float wheelieGain)
        {
            Id = id;
            Label = label;
            Effect = effect;
            PowerGain = powerGain;
            HandlingGain = handlingGain;
            WheelieGain = wheelieGain;
        }
    }

    public static class MotoUpgrades
    {
        public const int MaxLevel = 5;

        public static readonly UpgradeSlot[] Slots =
        {
            new UpgradeSlot("engine", "Moteur", "Accélération et vitesse de pointe", 0.5f, 0f, 0.1f),
            new UpgradeSlot("suspension", "Suspension", "Stabilité au point d'équilibre", 0f, 0.2f, 0.5f),
            new UpgradeSlot("tires", "Pneus", "Accroche et vitesse en virage", 0f, 0.6f, 0.1f),
            new UpgradeSlot("weight", "Allègement", "Reprises et facilité à cabrer", 0.3f, 0.2f, 0.4f),
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
