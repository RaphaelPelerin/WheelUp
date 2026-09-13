using UnityEngine;
using WheelingMoto.Core;
using WheelingMoto.Data;

namespace WheelingMoto.Gameplay
{
    /// <summary>
    /// Traduit la conduite en grandeurs mesurables et les rapporte à <see cref="MissionTracker"/>.
    /// C'est le seul pont entre le gameplay et les missions, et il ne va que dans un sens : le
    /// collecteur lit les propriétés publiques du contrôleur, il ne lui demande rien et ne le
    /// modifie pas. <see cref="MotorcycleController"/> n'a donc pas à savoir que les missions
    /// existent, et il n'a pas été touché pour les ajouter.
    ///
    /// Le composant s'installe tout seul depuis le HUD : les scènes sont des fichiers binaires que
    /// l'on préfère ne pas remanier pour un ajout de script.
    /// </summary>
    [DisallowMultipleComponent]
    public class RunStatsCollector : MonoBehaviour
    {
        /// <summary>
        /// Période de vidage du tampon. Un demi-tour de seconde : assez rare pour que la sauvegarde
        /// ne pèse rien, assez fréquent pour que le bandeau de mission accomplie tombe pendant que le
        /// joueur est encore sur la figure qui l'a déclenché.
        /// </summary>
        const float FlushInterval = 0.5f;

        /// <summary>En dessous, la moto est à l'arrêt : ni distance, ni temps de conduite.</summary>
        const float MovingSpeed = 0.5f;

        /// <summary>Durée minimale d'une roue avant pour qu'elle compte comme réussie plutôt que comme un coup de frein.</summary>
        const float StoppieMinDuration = 1.2f;

        MotorcycleController controller;
        float flushTimer;
        float wheelieStreak;
        float stoppieStreak;

        /// <summary>
        /// Pose le collecteur sur la moto et ouvre la session. Retourne null si la scène n'a pas de
        /// moto : l'appelant continue sans mesure plutôt que de planter.
        /// </summary>
        public static RunStatsCollector Install(MotorcycleController controller)
        {
            if (controller == null) return null;

            var existing = controller.GetComponent<RunStatsCollector>();
            if (existing != null) return existing;

            var collector = controller.gameObject.AddComponent<RunStatsCollector>();
            collector.controller = controller;
            return collector;
        }

        void Awake()
        {
            if (controller == null) controller = GetComponent<MotorcycleController>();
        }

        void OnEnable()
        {
            MissionTracker.BeginRun();
            if (controller != null) controller.Fell += OnFell;
        }

        void OnDisable()
        {
            if (controller != null) controller.Fell -= OnFell;

            // Quitter la scène sans passer par le bouton retour (retour système, mise en veille) ne
            // doit pas coûter la progression accumulée depuis le dernier vidage.
            MissionTracker.Flush();
        }

        void Update()
        {
            if (controller == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Measure(dt);

            flushTimer += dt;
            if (flushTimer >= FlushInterval)
            {
                flushTimer = 0f;
                MissionTracker.Flush();
            }
        }

        void Measure(float dt)
        {
            float forwardSpeed = Mathf.Max(0f, controller.SignedSpeed);
            bool fallen = controller.IsFallen;

            // Distance et temps : la marche arrière ne compte pas, sans quoi faire des allers-retours
            // sur place vaudrait autant que rouler.
            if (forwardSpeed > MovingSpeed)
            {
                float metres = forwardSpeed * dt;
                MissionTracker.Report(MissionMetric.Distance, metres);
                MissionTracker.Report(MissionMetric.RideTime, dt);

                if (GameSession.SelectedTime == TimeOfDay.Nuit)
                {
                    MissionTracker.Report(MissionMetric.NightDistance, metres);
                }
            }

            MissionTracker.ReportBest(MissionMetric.TopSpeed, controller.SpeedKmh);

            MeasureWheelie(dt, forwardSpeed, fallen);
            MeasureStoppie(dt, fallen);
        }

        /// <summary>
        /// Wheeling : distance et temps d'équilibre s'additionnent, la série la plus longue est un
        /// record. La série est rapportée à chaque image plutôt qu'à sa rupture — sinon la mission
        /// « tiens un wheeling de 22 s » ne se déclencherait qu'une fois la roue reposée, bien après
        /// le moment que le joueur vient de réussir.
        ///
        /// La zone Critique reste comptée : elle fait partie du wheeling tant que la moto ne tombe
        /// pas, et c'est précisément là que se joue le dosage. La chute, elle, casse la série.
        /// </summary>
        void MeasureWheelie(float dt, float forwardSpeed, bool fallen)
        {
            bool up = !fallen && controller.CurrentWheelieZone >= WheelieZone.Rising;

            if (!up)
            {
                wheelieStreak = 0f;
                return;
            }

            wheelieStreak += dt;
            MissionTracker.Report(MissionMetric.WheelieDistance, forwardSpeed * dt);
            MissionTracker.ReportBest(MissionMetric.WheelieStreak, wheelieStreak);

            if (controller.CurrentWheelieZone == WheelieZone.Balance)
            {
                MissionTracker.Report(MissionMetric.BalanceTime, dt);
            }
        }

        /// <summary>
        /// Roue avant : le temps s'additionne, et une roue avant n'est comptée comme « réussie »
        /// qu'au-delà de <see cref="StoppieMinDuration"/>. Sans ce seuil, chaque freinage un peu
        /// ferme validerait la mission sans que le joueur ait rien tenté.
        /// </summary>
        void MeasureStoppie(float dt, bool fallen)
        {
            bool up = !fallen && controller.CurrentStoppieZone >= WheelieZone.Rising;

            if (up)
            {
                float before = stoppieStreak;
                stoppieStreak += dt;
                MissionTracker.Report(MissionMetric.StoppieTime, dt);

                // Comptée au passage du seuil, pas à la retombée : la roue avant en cours compte déjà.
                if (before < StoppieMinDuration && stoppieStreak >= StoppieMinDuration)
                {
                    MissionTracker.Report(MissionMetric.StoppieCount, 1f);
                }
                return;
            }

            stoppieStreak = 0f;
        }

        void OnFell()
        {
            wheelieStreak = 0f;
            stoppieStreak = 0f;
            MissionTracker.Report(MissionMetric.Falls, 1f);
        }
    }
}
