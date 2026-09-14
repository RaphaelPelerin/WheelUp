using UnityEngine;
using WheelingMoto.Core;

namespace WheelingMoto.Data
{
    /// <summary>
    /// Fiche technique finale d'une moto : celle du catalogue, améliorations achetées comprises. C'est ce que
    /// la conduite applique et ce que le garage affiche.
    /// </summary>
    public struct MotoStats
    {
        public const float RiderMassKg = 75f;
        public const float MaxHandling = 10f;
        /// <summary>Part de la puissance qui arrive à la roue arrière, une fois passées la boîte et la chaîne.</summary>
        public const float DrivetrainEfficiency = 0.9f;
        public const float RollingResistance = 0.015f;
        public const float AirDensity = 1.225f;
        public const float Gravity = 9.81f;
        public const float WattsPerHorsepower = 735.5f;

        const float MinDragArea = 0.22f;
        const float MaxDragArea = 1.2f;
        // Marge de la traînée au-dessus de la vitesse de pointe : la moto l'atteint encore, au limiteur,
        // au lieu de s'en approcher sans jamais la toucher.
        const float TopSpeedMargin = 1.04f;

        public float PowerHp;
        public float PeakPowerRpm;
        public float RedlineRpm;
        public float WeightKg;
        public float TopSpeedKmh;
        public int Gears;
        public float FirstGearKmh;
        public float BrakingMps2;
        public float LaunchGrip;
        public float BalanceAngle;
        public float BalanceWidth;
        public float Handling;
        /// <summary>Traînée (surface frontale × Cx), en m², déduite de la fiche d'origine.</summary>
        public float DragArea;

        /// <summary>Masse lancée : la moto et son pilote, en kg.</summary>
        public float TotalMassKg => WeightKg + RiderMassKg;

        public static MotoStats For(MotoInfo moto)
        {
            MotoSpecs s = moto.Specs;
            var stats = new MotoStats
            {
                PowerHp = s.PowerHp,
                PeakPowerRpm = s.PeakPowerRpm,
                RedlineRpm = s.RedlineRpm,
                WeightKg = s.WeightKg,
                TopSpeedKmh = s.TopSpeedKmh,
                Gears = Mathf.Max(1, s.Gears),
                FirstGearKmh = s.FirstGearKmh,
                BrakingMps2 = s.BrakingMps2,
                LaunchGrip = s.LaunchGrip,
                BalanceAngle = s.BalanceAngle,
                BalanceWidth = s.BalanceWidth,
                Handling = s.Handling,
                // Traînée calée sur la fiche d'origine : la vitesse de pointe annoncée est bien celle où la
                // puissance ne suffit plus à vaincre l'air. Les améliorations ne changent pas la carrosserie.
                DragArea = CalibratedDragArea(s),
            };

            float powerShare = 0f, gripShare = 0f, weightShare = 0f;
            foreach (var slot in MotoUpgrades.Slots)
            {
                int level = GarageOwnership.GetUpgradeLevel(moto.Name, slot.Id);
                powerShare += slot.PowerShare * level;
                gripShare += slot.GripShare * level;
                weightShare += slot.WeightShare * level;
                stats.Handling += slot.HandlingGain * level;
                stats.BalanceWidth += slot.BalanceWidthGain * level;
            }

            stats.PowerHp *= 1f + powerShare;
            // Plus de puissance repousse l'équilibre avec l'air selon la racine cubique : la boîte est
            // rallongée d'autant, sinon le limiteur garderait l'ancienne vitesse de pointe.
            stats.TopSpeedKmh *= Mathf.Pow(1f + powerShare, 1f / 3f);
            stats.LaunchGrip *= 1f + gripShare;
            stats.BrakingMps2 *= 1f + gripShare;
            stats.WeightKg *= 1f - weightShare;
            stats.Handling = Mathf.Min(stats.Handling, MaxHandling);
            return stats;
        }

        /// <summary>
        /// Traînée qui fait tomber l'équilibre puissance / résistance juste au-dessus de la vitesse de pointe,
        /// moteur à la zone rouge en dernier rapport.
        /// </summary>
        static float CalibratedDragArea(MotoSpecs s)
        {
            float speed = s.TopSpeedKmh / 3.6f * TopSpeedMargin;
            float mass = s.WeightKg + RiderMassKg;
            float power = s.PowerHp * WattsPerHorsepower * DrivetrainEfficiency * PowerCurve(s.RedlineRpm / Mathf.Max(1f, s.PeakPowerRpm));
            float rolling = RollingResistance * mass * Gravity * speed;
            float area = (power - rolling) / (0.5f * AirDensity * speed * speed * speed);
            return Mathf.Clamp(area, MinDragArea, MaxDragArea);
        }

        /// <summary>
        /// Courbe de puissance, en part de la puissance maximale, selon le régime rapporté à celui de la
        /// puissance maximale : u + u² − u³. Couple maximal à mi-régime, puissance maximale à u = 1, chute
        /// au-delà : l'allure d'un moteur à combustion.
        /// </summary>
        public static float PowerCurve(float u) => Mathf.Max(0f, u + u * u - u * u * u);
    }
}
