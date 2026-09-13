using System;
using UnityEngine;

namespace WheelingMoto.Gameplay
{
    /// <summary>Figure en cours : roue arrière en l'air (wheeling) ou roue avant (stoppie).</summary>
    public enum StuntKind
    {
        None,
        Wheelie,
        Stoppie
    }

    /// <summary>Prouesse encaissée, telle qu'elle s'annonce au moment où les deux roues se reposent.</summary>
    public readonly struct StuntResult
    {
        public readonly StuntKind Kind;
        public readonly int Points;
        /// <summary>Temps passé en l'air, en secondes.</summary>
        public readonly float Duration;
        public readonly int Multiplier;
        /// <summary>Vrai si cette prouesse bat le record du joueur.</summary>
        public readonly bool Record;

        public StuntResult(StuntKind kind, int points, float duration, int multiplier, bool record)
        {
            Kind = kind;
            Points = points;
            Duration = duration;
            Multiplier = multiplier;
            Record = record;
        }
    }

    /// <summary>
    /// Points de prouesse. Tant qu'une roue est en l'air, le compteur monte ; son débit dépend de la qualité
    /// de la figure :
    /// - l'angle tenu : peu de points au décollage, pleins points dans la zone d'équilibre, un peu plus encore
    ///   au-delà, là où la chute guette ;
    /// - la vitesse : tenir la roue au pas est le plus dur, donc le plus payant, mais lever à pleine vitesse
    ///   rapporte quand même plus que le tarif de base ;
    /// - la durée : un cran de multiplicateur de plus à chaque palier de temps tenu.
    /// Dès que les deux roues sont reposées, le décompte s'arrête et les points sont encaissés. Une chute fait
    /// tomber tout ce qui ne l'était pas encore, mais le menu de chute laisse le joueur racheter la prouesse
    /// contre une publicité avant de repartir.
    /// </summary>
    public class StuntScorer : MonoBehaviour
    {
        const string BestKey = "stunt_best";

        [Header("Débit de points (par seconde, au multiplicateur x1)")]
        public float wheeliePointsPerSecond = 100f;
        [Tooltip("La roue avant est plus rare et plus risquée : elle rapporte davantage.")]
        public float stoppiePointsPerSecond = 140f;

        [Header("Qualité de la figure")]
        [Tooltip("Part des points au décollage de la roue, avant la zone d'équilibre.")]
        public float risingFactor = 0.35f;
        [Tooltip("Bonus dans la zone d'équilibre.")]
        public float balanceFactor = 1.25f;
        [Tooltip("Bonus au-delà de la zone d'équilibre : ça rapporte plus, mais la chute est proche.")]
        public float riskFactor = 1.4f;
        [Tooltip("Bonus au pas : un wheeling lent est bien plus dur à tenir.")]
        public float slowFactor = 2f;
        [Tooltip("Facteur à vitesse maximale : lever à fond rapporte moins qu'au pas, mais reste bien payant.")]
        public float fastFactor = 1.2f;

        [Header("Multiplicateur")]
        [Tooltip("Secondes tenues pour gagner un cran de multiplicateur.")]
        public float multiplierStep = 2.5f;
        public int maxMultiplier = 5;

        [Header("Fin de figure")]
        [Tooltip("Angle sous lequel la roue est considérée reposée, en degrés.")]
        public float minAngle = 2f;
        [Tooltip("Délai avant encaissement, les deux roues au sol : de quoi relever aussitôt sans casser la série.")]
        public float landedGrace = 0.25f;
        [Tooltip("En dessous, la prouesse est comptée mais pas annoncée à l'écran (petit cabrage sans intérêt).")]
        public int minAnnouncedPoints = 20;

        /// <summary>Prouesse encaissée : les deux roues sont reposées, ou la chute a été rachetée.</summary>
        public event Action<StuntResult> Banked;
        /// <summary>Chute : les points en cours tombent, rachetables tant que le joueur n'est pas reparti.</summary>
        public event Action<int> Failed;

        MotorcycleController bike;
        StuntKind kind;
        float pending;
        float duration;
        float landedTimer;
        int total;

        // Prouesse tombée avec le pilote : gardée de côté, le menu de chute permet de la racheter.
        StuntKind lostKind;
        int lostPoints;
        float lostDuration;
        int lostMultiplier = 1;

        /// <summary>Vrai tant qu'une figure est en cours (le court délai d'encaissement compris).</summary>
        public bool Active => kind != StuntKind.None;
        /// <summary>Figure en cours, ou la dernière pendant le délai d'encaissement.</summary>
        public StuntKind Kind => kind;
        /// <summary>Points accumulés, pas encore encaissés.</summary>
        public int PendingPoints => Mathf.RoundToInt(pending);
        /// <summary>Temps passé en l'air sur la figure en cours.</summary>
        public float Duration => duration;
        public int Multiplier => MultiplierAt(duration);
        /// <summary>Avance vers le cran de multiplicateur suivant, de 0 à 1 (1 une fois au maximum).</summary>
        public float ChainProgress => Multiplier >= maxMultiplier
            ? 1f
            : Mathf.Repeat(duration / Mathf.Max(0.1f, multiplierStep), 1f);
        /// <summary>Points encaissés depuis le début de la partie.</summary>
        public int Total => total;
        /// <summary>Points de la prouesse tombée avec le pilote, encore rachetables (0 : rien à sauver).</summary>
        public int LostPoints => lostPoints;
        /// <summary>Meilleure prouesse du joueur, conservée d'une partie à l'autre.</summary>
        public int Best => PlayerPrefs.GetInt(BestKey, 0);
        /// <summary>Zone d'équilibre de la figure en cours : donne sa couleur à l'affichage.</summary>
        public WheelieZone Zone => bike == null
            ? WheelieZone.Flat
            : kind == StuntKind.Stoppie ? bike.CurrentStoppieZone : bike.CurrentWheelieZone;
        /// <summary>Multiplicateur dû à la lenteur (1 à vitesse maximale, jusqu'à slowFactor au pas).</summary>
        public float SpeedBonus => bike != null ? SpeedFactor() : 1f;

        /// <summary>Compteur de la moto, créé au besoin : la moto le pose elle-même, le HUD s'y raccroche.</summary>
        public static StuntScorer Attach(MotorcycleController bike)
        {
            if (bike == null) return null;
            if (!bike.TryGetComponent(out StuntScorer scorer))
            {
                scorer = bike.gameObject.AddComponent<StuntScorer>();
            }
            scorer.Bind(bike);
            return scorer;
        }

        void Bind(MotorcycleController owner)
        {
            if (bike == owner) return;
            if (bike != null) Unbind();
            bike = owner;
            bike.Fell += OnFell;
            bike.Respawned += OnRespawned;
        }

        void OnDestroy() => Unbind();

        void Unbind()
        {
            if (bike == null) return;
            bike.Fell -= OnFell;
            bike.Respawned -= OnRespawned;
        }

        void Update()
        {
            // Pendant la chute, la moto bascule jusqu'à 95° : sans ce garde-fou, elle marquerait des points en tombant.
            if (bike == null || bike.IsFallen) return;

            float dt = Time.deltaTime;
            StuntKind current = CurrentKind();

            if (current == StuntKind.None)
            {
                if (!Active) return;
                // Les deux roues sont reposées : le décompte s'arrête ici, et les points tombent juste après.
                landedTimer += dt;
                if (landedTimer >= landedGrace) Bank();
                return;
            }

            landedTimer = 0f;
            kind = current;
            duration += dt;
            pending += PointsPerSecond(current) * AngleFactor(current) * SpeedFactor() * MultiplierAt(duration) * dt;
        }

        StuntKind CurrentKind()
        {
            if (bike.StoppieAngle >= minAngle) return StuntKind.Stoppie;
            if (bike.WheelieAngle >= minAngle) return StuntKind.Wheelie;
            return StuntKind.None;
        }

        float PointsPerSecond(StuntKind current)
            => current == StuntKind.Stoppie ? stoppiePointsPerSecond : wheeliePointsPerSecond;

        /// <summary>Qualité de l'angle tenu, aux bornes de zones lues sur la moto : tout réglage s'y reflète.</summary>
        float AngleFactor(StuntKind current)
        {
            bool stoppie = current == StuntKind.Stoppie;
            float angle = stoppie ? bike.StoppieAngle : bike.WheelieAngle;
            float sweetMin = stoppie ? bike.stoppieSweetMin : bike.wheelieSweetMin;
            float sweetMax = stoppie ? bike.stoppieSweetMax : bike.wheelieSweetMax;

            if (angle < sweetMin) return Mathf.Lerp(risingFactor, 1f, angle / Mathf.Max(1f, sweetMin));
            if (angle <= sweetMax) return balanceFactor;
            return riskFactor;
        }

        /// <summary>Bonus de lenteur : tenir la roue au pas vaut bien plus que la lever à fond.</summary>
        float SpeedFactor()
        {
            float speed = Mathf.Abs(bike.SignedSpeed) / Mathf.Max(0.1f, bike.maxSpeed);
            return Mathf.Lerp(slowFactor, fastFactor, Mathf.Clamp01(speed));
        }

        int MultiplierAt(float held)
            => Mathf.Clamp(1 + Mathf.FloorToInt(held / Mathf.Max(0.1f, multiplierStep)), 1, Mathf.Max(1, maxMultiplier));

        void Bank()
        {
            StuntKind banked = kind;
            int points = PendingPoints;
            int multiplier = Multiplier;
            float held = duration;
            Clear();
            Cash(banked, points, held, multiplier);
        }

        /// <summary>Points crédités et annoncés : fin de figure, ou prouesse rachetée après une chute.</summary>
        void Cash(StuntKind banked, int points, float held, int multiplier)
        {
            if (points <= 0) return;

            total += points;
            bool record = points > Best;
            if (record)
            {
                PlayerPrefs.SetInt(BestKey, points);
                PlayerPrefs.Save();
            }
            Banked?.Invoke(new StuntResult(banked, points, held, multiplier, record));
        }

        void OnFell()
        {
            // La prouesse tombe avec le pilote, mais rien n'est encore perdu : elle reste rachetable
            // jusqu'à ce que le joueur reparte (voir RecoverLost / ForgetLost).
            lostKind = kind;
            lostPoints = PendingPoints;
            lostDuration = duration;
            lostMultiplier = Multiplier;
            Clear();
            if (lostPoints > 0) Failed?.Invoke(lostPoints);
        }

        /// <summary>Prouesse rachetée (publicité) : ses points sont encaissés au lieu d'être perdus.</summary>
        public bool RecoverLost()
        {
            if (lostPoints <= 0) return false;

            Cash(lostKind, lostPoints, lostDuration, lostMultiplier);
            ForgetLost();
            return true;
        }

        /// <summary>Le joueur est reparti sans racheter : la prouesse tombée est définitivement perdue.</summary>
        void OnRespawned() => ForgetLost();

        void ForgetLost()
        {
            lostKind = StuntKind.None;
            lostPoints = 0;
            lostDuration = 0f;
            lostMultiplier = 1;
        }

        void Clear()
        {
            kind = StuntKind.None;
            pending = 0f;
            duration = 0f;
            landedTimer = 0f;
        }
    }
}
