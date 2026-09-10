using UnityEngine;
using WheelingMoto.Core;

namespace WheelingMoto.Data
{
    /// <summary>Statistiques finales d'une moto : stats de base du catalogue plus améliorations achetées.</summary>
    public struct MotoStats
    {
        public float Power;
        public float Handling;
        public float Wheelie;

        public const float MaxValue = 10f;

        public static MotoStats For(MotoInfo moto)
        {
            var stats = new MotoStats
            {
                Power = moto.Power,
                Handling = moto.Handling,
                Wheelie = moto.Wheelie,
            };

            foreach (var slot in MotoUpgrades.Slots)
            {
                int level = GarageOwnership.GetUpgradeLevel(moto.Name, slot.Id);
                stats.Power += slot.PowerGain * level;
                stats.Handling += slot.HandlingGain * level;
                stats.Wheelie += slot.WheelieGain * level;
            }

            stats.Power = Mathf.Min(stats.Power, MaxValue);
            stats.Handling = Mathf.Min(stats.Handling, MaxValue);
            stats.Wheelie = Mathf.Min(stats.Wheelie, MaxValue);
            return stats;
        }
    }
}
