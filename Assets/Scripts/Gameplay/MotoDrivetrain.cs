using UnityEngine;
using WheelingMoto.Data;

namespace WheelingMoto.Gameplay
{
    /// <summary>Performances mesurées d'une fiche technique : ce que le garage affiche.</summary>
    public readonly struct MotoPerformance
    {
        /// <summary>Vitesse de pointe réellement atteinte, en km/h.</summary>
        public readonly float TopSpeedKmh;
        /// <summary>Temps de 0 à 50 km/h, en secondes.</summary>
        public readonly float ZeroTo50;
        /// <summary>Temps de 0 à 100 km/h, en secondes, ou 0 si la moto n'y arrive pas.</summary>
        public readonly float ZeroTo100;
        /// <summary>Distance d'arrêt depuis 100 km/h, en mètres.</summary>
        public readonly float BrakingFrom100;

        public MotoPerformance(float topSpeedKmh, float zeroTo50, float zeroTo100, float brakingFrom100)
        {
            TopSpeedKmh = topSpeedKmh;
            ZeroTo50 = zeroTo50;
            ZeroTo100 = zeroTo100;
            BrakingFrom100 = brakingFrom100;
        }
    }

    /// <summary>
    /// Moteur et transmission d'une moto, tirés de sa fiche technique. C'est d'eux que vient la prise de vitesse :
    /// - le moteur suit une vraie courbe de puissance : peu de chose au ralenti, le couple en milieu de
    ///   compte-tours, la puissance en haut, et le limiteur coupe à la zone rouge ;
    /// - la boîte passe les rapports toute seule, près de la zone rouge, avec une courte coupure à chaque
    ///   passage ; chaque rapport tire fort puis s'essouffle, le suivant reprend plus long ;
    /// - au départ, l'embrayage patine : le moteur reste dans ses tours pendant que la moto s'élance, et
    ///   c'est l'adhérence du pneu arrière qui borne ce qui passe au sol ;
    /// - la poignée de gaz s'ouvre en une fraction de seconde, pas instantanément.
    /// L'air (traînée au carré de la vitesse) et les pneus freinent en permanence : la vitesse de pointe
    /// arrive là où la puissance ne suffit plus, ou au limiteur du dernier rapport.
    /// </summary>
    public sealed class MotoDrivetrain
    {
        // La boîte monte le rapport à ce régime, en part de la zone rouge, et redescend sous celui-ci.
        const float UpshiftShare = 0.96f;
        const float DownshiftShare = 0.42f;
        // Coupure de la poussée pendant un passage de rapport, en secondes.
        const float ShiftTime = 0.12f;
        // Régime tenu par l'embrayage qui patine au départ, en part du régime de puissance maximale : assez
        // bas pour que l'embrayage soit relâché tôt, même sur une première longue.
        const float LaunchShare = 0.45f;
        // Étagement : vitesse de chaque rapport interpolée entre la première et la dernière selon t^GearSpacing.
        // Sous 1, les premiers rapports sont espacés et les derniers resserrés, comme sur une vraie boîte.
        const float GearSpacing = 0.85f;
        const float IdleShare = 0.13f;
        // Ouverture et fermeture de la poignée, en part par seconde : pleins gaz en un cinquième de seconde.
        const float ThrottleOpenRate = 5f;
        const float ThrottleCloseRate = 8f;

        readonly MotoStats stats;
        readonly float[] gearTopSpeed; // m/s atteints à la zone rouge, par rapport
        readonly float peakPowerWatts;
        readonly float mass;

        float shiftTimer;

        public int Gear { get; private set; } = 1;
        public int GearCount => gearTopSpeed.Length;
        /// <summary>Régime moteur, en tr/min.</summary>
        public float Rpm { get; private set; }
        /// <summary>Ouverture de la poignée, de 0 à 1.</summary>
        public float Throttle { get; private set; }
        /// <summary>Vrai pendant la courte coupure d'un passage de rapport.</summary>
        public bool Shifting => shiftTimer > 0f;
        public MotoStats Stats => stats;

        public MotoDrivetrain(MotoStats motoStats)
        {
            stats = motoStats;
            mass = Mathf.Max(1f, stats.TotalMassKg);
            peakPowerWatts = stats.PowerHp * MotoStats.WattsPerHorsepower * MotoStats.DrivetrainEfficiency;

            // De la première à la vitesse de pointe au limiteur, écarts décroissants d'un rapport à l'autre.
            int gears = Mathf.Max(1, stats.Gears);
            gearTopSpeed = new float[gears];
            float first = Mathf.Max(5f, stats.FirstGearKmh) / 3.6f;
            float top = Mathf.Max(first, stats.TopSpeedKmh / 3.6f);
            for (int i = 0; i < gears; i++)
            {
                float t = gears == 1 ? 1f : (float)i / (gears - 1);
                gearTopSpeed[i] = Mathf.Lerp(first, top, Mathf.Pow(t, GearSpacing));
            }
            Rpm = stats.RedlineRpm * IdleShare;
        }

        /// <summary>
        /// Accélération donnée par le moteur à cette vitesse, en m/s², résistances non comprises. Fait vivre la
        /// poignée, la boîte et le régime.
        /// </summary>
        /// <param name="open">Gaz demandés.</param>
        /// <param name="gripScale">Adhérence en plus (embrayage lâché pour lever la roue).</param>
        public float Drive(float speed, bool open, float gripScale, float dt)
        {
            Throttle = Mathf.MoveTowards(Throttle, open ? 1f : 0f, (open ? ThrottleOpenRate : ThrottleCloseRate) * dt);
            speed = Mathf.Max(0f, speed);
            UpdateGear(speed, open, dt);
            if (!open || Shifting) return 0f;

            float force = WheelForce(speed) * Throttle;
            return Mathf.Min(force / mass, stats.LaunchGrip * gripScale);
        }

        /// <summary>Frein moteur, gaz coupés, en m/s² : fort sur les petits rapports et haut dans les tours.</summary>
        public float EngineBraking(float baseDeceleration)
        {
            float gearShare = GearCount > 1 ? (Gear - 1f) / (GearCount - 1f) : 1f;
            float rev = Mathf.Clamp01(Rpm / Mathf.Max(1f, stats.RedlineRpm));
            return baseDeceleration * Mathf.Lerp(1.6f, 0.6f, gearShare) * (0.3f + rev);
        }

        /// <summary>Freinage de l'air et des pneus, en m/s².</summary>
        public float Resistance(float speed)
        {
            float drag = 0.5f * MotoStats.AirDensity * stats.DragArea * speed * speed;
            float rolling = MotoStats.RollingResistance * mass * MotoStats.Gravity;
            return (drag + rolling) / mass;
        }

        void UpdateGear(float speed, bool driving, float dt)
        {
            if (shiftTimer > 0f) shiftTimer -= dt;

            float redline = stats.RedlineRpm;
            if (driving && !Shifting && Gear < GearCount && RpmInGear(Gear, speed) >= redline * UpshiftShare)
            {
                Gear++;
                shiftTimer = ShiftTime;
            }
            while (Gear > 1 && RpmInGear(Gear, speed) < redline * DownshiftShare)
            {
                Gear--;
            }

            float rpm = RpmInGear(Gear, speed);
            // Départ : l'embrayage patine et laisse le moteur dans ses tours.
            if (Gear == 1 && driving) rpm = Mathf.Max(rpm, LaunchRpm);
            Rpm = Mathf.Clamp(rpm, redline * IdleShare, redline);
        }

        float RpmInGear(int gear, float speed) => stats.RedlineRpm * speed / gearTopSpeed[gear - 1];

        float LaunchRpm => stats.PeakPowerRpm * LaunchShare;

        /// <summary>Poussée à la roue arrière dans le rapport engagé, en newtons.</summary>
        float WheelForce(float speed)
        {
            float rpm = RpmInGear(Gear, speed);
            float launch = LaunchRpm;
            if (Gear == 1 && rpm < launch)
            {
                // Embrayage qui patine : le couple du moteur tenu à ce régime passe par la première.
                float launchSpeed = gearTopSpeed[0] * launch / stats.RedlineRpm;
                return PowerAt(launch) / Mathf.Max(0.1f, launchSpeed);
            }
            // Limiteur : plus rien au-delà de la zone rouge.
            if (rpm >= stats.RedlineRpm) return 0f;
            return PowerAt(rpm) / Mathf.Max(0.1f, speed);
        }

        float PowerAt(float rpm) => peakPowerWatts * MotoStats.PowerCurve(rpm / Mathf.Max(1f, stats.PeakPowerRpm));

        /// <summary>
        /// Mesure une fiche technique comme sur un banc : départ arrêté pleins gaz, sans lever la roue, jusqu'à
        /// la vitesse de pointe ; puis freinage appuyé depuis 100 km/h.
        /// </summary>
        public static MotoPerformance Measure(MotoStats motoStats)
        {
            const float Dt = 0.01f;
            const float MaxTime = 120f;

            var drivetrain = new MotoDrivetrain(motoStats);
            float speed = 0f, top = 0f, t50 = 0f, t100 = 0f, stableFor = 0f;
            for (float t = 0f; t < MaxTime; t += Dt)
            {
                float accel = drivetrain.Drive(speed, true, 1f, Dt) - drivetrain.Resistance(speed);
                speed = Mathf.Max(0f, speed + accel * Dt);

                if (t50 <= 0f && speed >= 50f / 3.6f) t50 = t;
                if (t100 <= 0f && speed >= 100f / 3.6f) t100 = t;
                if (speed > top + 0.01f)
                {
                    top = speed;
                    stableFor = 0f;
                }
                else if ((stableFor += Dt) > 3f)
                {
                    break;
                }
            }

            float from100 = 100f / 3.6f;
            float braking = from100 * from100 / (2f * Mathf.Max(0.1f, motoStats.BrakingMps2));
            return new MotoPerformance(top * 3.6f, t50, t100, braking);
        }
    }
}
