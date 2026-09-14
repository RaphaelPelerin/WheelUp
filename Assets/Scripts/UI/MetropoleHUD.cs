using TMPro;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.UI;
using WheelingMoto.Core;
using WheelingMoto.Data;
using WheelingMoto.Gameplay;

namespace WheelingMoto.UI
{
    /// <summary>
    /// HUD de la scène Métropole : commandes tactiles (direction par flèches ou par joystick selon les
    /// Paramètres ; GAZ, LEVER, FREIN AV et FREIN), vitesse, jauge d'angle (wheeling ou roue avant), bandeau
    /// « Chute ! », menu de chute, bouton de changement de vue et zone de glissement pour tourner la caméra.
    /// Pilote à terre, les commandes sont rangées : on ne conduit pas une moto couchée.
    ///
    /// Disposition, pensée pour un iPhone tenu en paysage :
    /// - à gauche, le cumul des prouesses, la jauge d'angle, puis la direction tout en bas ; la jauge est
    ///   plaquée contre le bord, au ras de la marge de la zone sûre ;
    /// - en haut au centre, le compteur de vitesse (<see cref="SpeedReadout"/>) ; en haut à droite, VUE et
    ///   PAUSE, puis les points de la figure en cours juste en dessous ;
    /// - en bas à droite, les quatre pédales en carré : FREIN AV et LEVER en haut, FREIN et GAZ en bas.
    ///   Les deux freins occupent la colonne de gauche ; les deux commandes qui lèvent la roue, LEVER et
    ///   GAZ, sont l'une sur l'autre dans la colonne de droite, GAZ dans le coin, le pouce passant de l'une
    ///   à l'autre sans quitter le bloc. Chaque frein est au niveau de la commande qu'il contre.
    /// Ce n'est que la disposition d'origine : le joueur peut déplacer la direction et chaque pédale depuis les
    /// Paramètres (<see cref="ControlLayoutEditor"/>), le gabarit partagé étant <see cref="ControlLayout"/>.
    /// Tout vit dans la zone sûre de l'écran (<see cref="SafeArea"/>) : la Dynamic Island et l'encoche, qui
    /// mangent un bord en paysage, la barre d'accueil et l'arc des coins arrondis en sont écartés. Rien
    /// d'important n'est posé derrière. Les boutons reprennent l'arrondi continu des coins de l'iPhone.
    /// </summary>
    public class MetropoleHUD : MonoBehaviour
    {
        const float FallOverlayHold = 0.9f;
        const float FallOverlayFade = 0.5f;

        // Gabarit des commandes, partagé avec l'éditeur de disposition des Paramètres (voir ControlLayout).
        const float Gap = ControlLayout.Gap;
        const float TopButtonWidth = ControlLayout.TopButtonWidth;
        const float TopButtonHeight = ControlLayout.TopButtonHeight;
        const int TopFontSize = UITheme.FontLabel;

        // Boutons translucides : la route reste visible sous les pouces. À l'appui, le fond passe au
        // rouge de la marque, l'assombrissement par défaut du Button ne se verrait pas sur ce fond.
        static readonly Color ControlColor = new Color(0.19f, 0.19f, 0.2f, 0.3f);
        static readonly Color ControlPressedColor = new Color(UITheme.Brand.r, UITheme.Brand.g, UITheme.Brand.b, 0.5f);
        static readonly Color ControlTextColor = new Color(1f, 1f, 1f, 0.85f);
        static readonly Color JoystickRailColor = new Color(1f, 1f, 1f, 0.12f);
        static readonly Color KnobColor = new Color(1f, 1f, 1f, 0.45f);

        MotorcycleController controller;
        MotoCameraRig cameraRig;
        UITheme theme;

        /// <summary>Racine du canvas : le récapitulatif de fin de session s'y pose, par-dessus tout le HUD.</summary>
        Transform hudRoot;
        SpeedReadout speedReadout;
        TextMeshProUGUI viewLabel;
        CameraView displayedView;
        WheelieGauge gauge;
        StuntScoreHud stunts;
        CrashMenu crashMenu;
        PauseMenu pauseMenu;
        GameObject controlsRoot;
        RectTransform steeringRoot;
        RectTransform pedalsRoot;
        SteeringControl builtSteering;
        int builtLayoutRevision;
        CanvasGroup fallOverlay;
        TextMeshProUGUI fallHint;
        float fallOverlayTimer;
        bool leftHeld;
        bool rightHeld;
        ControlLayout layout;
        float margin;

        void Awake()
        {
            controller = FindAnyObjectByType<MotorcycleController>();
            cameraRig = FindAnyObjectByType<MotoCameraRig>();
            theme = new UITheme();
            Build();

            if (controller != null)
            {
                controller.Fell += OnFell;

                // Ouvre la session de mesure et branche les missions sur la conduite. Posé ici plutôt
                // que dans la scène : ajouter un composant à un fichier .unity pour un script qui se
                // suffit à lui-même compliquerait les fusions pour rien.
                RunStatsCollector.Install(controller);
            }
        }

        void OnDestroy()
        {
            if (controller != null)
            {
                controller.Fell -= OnFell;
            }
            stunts?.Dispose();
            crashMenu?.Dispose();
            pauseMenu?.Dispose();
        }

        void Update()
        {
            // Échap sur PC, bouton retour sur Android (Unity le rapporte comme Échap) : pause, puis retour arrière.
            if (BackPressed())
            {
                if (pauseMenu.Showing) pauseMenu.Back();
                else pauseMenu.Open();
            }

            if (controller != null)
            {
                speedReadout?.Tick();
                gauge?.Tick();
                stunts?.Tick();
            }

            crashMenu?.Tick();
            // Pilote à terre ou jeu en pause : les commandes disparaissent, et les boutons maintenus se
            // relâchent avec elles.
            bool riding = !(crashMenu != null && crashMenu.Showing) && !pauseMenu.Showing;
            if (controlsRoot != null && controlsRoot.activeSelf != riding) controlsRoot.SetActive(riding);

            // La vue peut aussi changer au clavier (C / V) : le libellé suit.
            if (cameraRig != null && viewLabel != null && cameraRig.CurrentView != displayedView)
            {
                RefreshViewLabel();
            }

            if (fallOverlayTimer > 0f)
            {
                fallOverlayTimer -= Time.deltaTime;
                fallOverlay.alpha = Mathf.Clamp01(fallOverlayTimer / FallOverlayFade);
            }
        }

        static bool BackPressed()
        {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.Escape);
#endif
        }

        void OnFell()
        {
            fallHint.text = CrashMenu.Hint(controller);
            fallOverlayTimer = FallOverlayHold + FallOverlayFade;
            fallOverlay.alpha = 1f;
        }

        /// <summary>Mesure du gabarit ramenée à l'échelle retenue pour cet écran.</summary>
        float S(float value) => layout.S(value);

        void Build()
        {
            // Marge et échelle calées sur l'écran réel : rien ne doit jamais sortir des bords.
            layout = ControlLayout.Measure();
            margin = layout.Margin;

            var canvas = UIFactory.CreateRootCanvas("MetropoleHUDCanvas");
            var root = canvas.transform;
            hudRoot = root;

            // Créée en premier, donc dessinée derrière : les boutons restent prioritaires.
            // Plein écran : glisser depuis les bords tourne aussi la caméra.
            BuildCameraDragZone(root);
            BuildFallOverlay(root);

            // Zone sûre avec retrait des coins : Dynamic Island, barre d'accueil et arcs des coins écartés.
            Transform safeArea = UIFactory.AddSafeArea(root, "SafeArea").transform;

            float topButtonHeight = S(TopButtonHeight);

            // En haut au centre, le haut du cadran au ras des boutons : le compteur reste dans le champ de vision.
            if (controller != null)
            {
                speedReadout = new SpeedReadout();
                speedReadout.Build(safeArea, theme, controller, top: margin);
            }

            // La rangée du haut est rentrée d'un cran depuis le bord : elle tombait trop près de l'angle de
            // l'écran, là où la main qui tient le téléphone mord déjà.
            float rightEdge = margin + S(Gap);

            // Pause plutôt que retour direct au menu : on peut y régler les Paramètres sans quitter la partie.
            Vector2 topRight = new Vector2(1, 1);
            float buttonWidth = S(TopButtonWidth);
            var pauseButton = UIFactory.AddButton(safeArea, "PauseButton", "II  PAUSE", ControlColor, ControlTextColor,
                Mathf.RoundToInt(S(TopFontSize)), topRight, topRight,
                new Vector2(-rightEdge - buttonWidth, -margin - topButtonHeight), new Vector2(-rightEdge, -margin),
                () => pauseMenu.Open());
            UIFactory.ApplyScreenCorners(pauseButton.image);

            // Tout ce qui ne sert qu'en roulant (commandes, jauge d'angle, changement de vue) est réuni sous
            // un même objet : la chute le range d'un coup, et les boutons restés enfoncés se relâchent avec lui
            // (HoldButton et SteerJoystick lâchent tout à la désactivation).
            var driving = UIFactory.CreateUIObject("Driving", safeArea);
            UIFactory.SetRect(driving, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            controlsRoot = driving.gameObject;

            if (cameraRig != null)
            {
                float viewRight = -rightEdge - buttonWidth - S(Gap);
                var viewButton = UIFactory.AddButton(driving, "ViewButton", "VUE", ControlColor, ControlTextColor,
                    Mathf.RoundToInt(S(TopFontSize)), topRight, topRight,
                    new Vector2(viewRight - buttonWidth, -margin - topButtonHeight), new Vector2(viewRight, -margin),
                    () => cameraRig.ToggleView());
                UIFactory.ApplyScreenCorners(viewButton.image);
                viewLabel = viewButton.GetComponentInChildren<TextMeshProUGUI>();
                RefreshViewLabel();
            }

            if (controller != null)
            {
                // Points de la figure en cours dans la colonne de droite, sous VUE et PAUSE, et rentrés du
                // bord du même cran que ces boutons : c'est l'autre main, celle qui ne surveille pas l'angle.
                stunts = new StuntScoreHud();
                stunts.Build(safeArea, theme, controller,
                    edge: rightEdge,
                    liveTop: margin + topButtonHeight + S(Gap));

                // Jauge contre le bord gauche, sous le cumul et juste au-dessus des boutons de direction :
                // l'angle se surveille du coin de l'oeil, donc la barre sort du champ où les pouces passent.
                // Elle était auparavant centrée sur le bloc de direction, ce qui la ramenait vers le milieu
                // de l'écran. Le bord est sûr : la jauge est posée dans la zone sûre, qui écarte déjà la
                // Dynamic Island et l'arrondi des coins.
                gauge = new WheelieGauge();
                gauge.Build(driving, theme, controller,
                    left: margin,
                    top: layout.GaugeTop,
                    bottom: layout.GaugeBottom(SettingsManager.Steering));
            }

            // Direction et pédales dans leurs propres conteneurs : elles se reconstruisent si on change de
            // commande ou de disposition en pause.
            steeringRoot = UIFactory.CreateUIObject("Steering", driving);
            UIFactory.SetRect(steeringRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            pedalsRoot = UIFactory.CreateUIObject("Pedals", driving);
            UIFactory.SetRect(pedalsRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            BuildControls();

            // Au-dessus des commandes mais sous les menus : une mission validée doit rester lisible
            // pendant la conduite, sans recouvrir la pause ni le menu de chute.
            MissionToast.Create(safeArea, theme);

            // Créés en dernier, donc dessinés par-dessus tout le reste : ce sont des menus, ils passent devant
            // le HUD — la pause devant le menu de chute, qu'elle peut interrompre.
            if (controller != null)
            {
                crashMenu = new CrashMenu();
                crashMenu.Build(root, theme, controller, OnQuitToMenu);
            }
            pauseMenu = new PauseMenu();
            pauseMenu.Build(root, theme, OnPauseClosed, OnQuitToMenu);
        }

        void BuildControls()
        {
            builtSteering = SettingsManager.Steering;
            builtLayoutRevision = SettingsManager.ControlLayoutRevision;
            if (builtSteering == SteeringControl.Joystick)
            {
                BuildJoystick(steeringRoot);
            }
            else
            {
                BuildSteerArrows(steeringRoot);
            }
            BuildPedals(pedalsRoot);
        }

        /// <summary>
        /// Clôt la session et montre le bilan avant de rendre la main au menu. Le récapitulatif ne
        /// distribue rien : si le joueur quitte l'application au lieu de le valider, il n'a rien
        /// perdu, tout a déjà été crédité pendant la conduite.
        /// </summary>
        void OnQuitToMenu()
        {
            // Les points de prouesse s'arrêtaient au récapitulatif et disparaissaient avec lui. Ils
            // deviennent l'XP qui récompense le pilotage, quand les missions récompensent l'assiduité.
            //
            // Versés avant la clôture, et non après : une montée de niveau s'annonce via RewardFeed,
            // que le bilan ne collecte que tant que la session est ouverte. Verser après EndRun ferait
            // disparaître la ligne « NIVEAU 7 » du récapitulatif qui vient pourtant de la provoquer.
            var run = MissionTracker.CurrentRun;
            if (run != null)
            {
                run.XpEarned = run.StuntPoints / LevelRewardCatalog.StuntPointsPerXp;
                LevelManager.AddXp(run.XpEarned);
            }

            var stats = MissionTracker.EndRun();

            if (!RunSummaryScreen.TryShow(hudRoot, theme, stats, SceneLoader.LoadMainMenu))
            {
                SceneLoader.LoadMainMenu();
            }
        }

        /// <summary>
        /// Reprise après la pause : la commande de direction ou la disposition des boutons a pu changer dans les
        /// Paramètres.
        /// </summary>
        void OnPauseClosed()
        {
            if (SettingsManager.Steering == builtSteering && SettingsManager.ControlLayoutRevision == builtLayoutRevision) return;

            ClearChildren(steeringRoot);
            ClearChildren(pedalsRoot);
            leftHeld = false;
            rightHeld = false;
            controller?.SetSteer(0f);
            BuildControls();
        }

        /// <summary>Détruits en fin de frame : détachés d'abord, pour que les nouveaux prennent leur place tout de suite.</summary>
        static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform old = parent.GetChild(i);
                old.SetParent(null, false);
                Destroy(old.gameObject);
            }
        }

        void BuildSteerArrows(Transform parent)
        {
            AddHold(parent, DrivingControl.SteerLeft,
                () => { leftHeld = true; UpdateSteer(); }, () => { leftHeld = false; UpdateSteer(); });

            AddHold(parent, DrivingControl.SteerRight,
                () => { rightHeld = true; UpdateSteer(); }, () => { rightHeld = false; UpdateSteer(); });
        }

        /// <summary>
        /// Joystick horizontal : la direction suit l'écart du doigt et revient au milieu au relâchement.
        ///
        /// La zone de préhension est large d'une moitié d'écran, et non d'un disque à la taille du pouce.
        /// C'est la course du doigt qui donne la finesse du braquage : un disque de JoystickSize ne laissait
        /// qu'une centaine d'unités de part et d'autre du centre, donc un tout petit déplacement suffisait à
        /// passer de tout droit à braquage maximal. Sa place, par défaut en bas à gauche, se règle dans les
        /// Paramètres comme celle des autres commandes.
        ///
        /// La piste visible, elle, reste à la hauteur de la manette : une dalle de la hauteur de la zone
        /// masquerait le quart de la route. Le joueur attrape donc bien plus large que ce qu'il voit.
        /// </summary>
        void BuildJoystick(Transform parent)
        {
            var area = UIFactory.CreateUIObject("SteerJoystick", parent);
            layout.Place(area, DrivingControl.Joystick, layout.Center(DrivingControl.Joystick));

            // Transparente mais captante : c'est ce rectangle que SteerJoystick mesure pour la course.
            var surface = area.gameObject.AddComponent<Image>();
            surface.color = Color.clear;
            surface.raycastTarget = true;

            // Piste visible, centrée en hauteur sur la manette : la zone reste plus haute qu'elle, pour que
            // le pouce puisse dériver vers le haut ou le bas sans lâcher la direction.
            float trackHalf = S(ControlLayout.JoystickKnobSize) * 0.5f;
            var track = UIFactory.AddPanel(area, "Track", ControlColor, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f),
                new Vector2(0f, -trackHalf), new Vector2(0f, trackHalf), rounded: true);
            track.raycastTarget = false;

            // Rail horizontal : le joystick ne sert qu'à diriger.
            var rail = UIFactory.AddPanel(area, "Rail", JoystickRailColor, new Vector2(0.02f, 0.5f), new Vector2(0.98f, 0.5f),
                new Vector2(0f, -6f), new Vector2(0f, 6f), rounded: true);
            rail.raycastTarget = false;

            float half = S(ControlLayout.JoystickKnobSize) * 0.5f;
            var knob = UIFactory.CreateUIObject("Knob", area);
            UIFactory.SetRect(knob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-half, -half), new Vector2(half, half));
            var knobImage = knob.gameObject.AddComponent<Image>();
            knobImage.sprite = SteerJoystick.CircleSprite;
            knobImage.color = KnobColor;
            knobImage.raycastTarget = false;

            area.gameObject.AddComponent<SteerJoystick>().Init(knob,
                value => controller?.SetSteer(value),
                held => knobImage.color = held ? ControlPressedColor : KnobColor);
        }

        /// <summary>Les quatre pédales, chacune à la place que lui donne le gabarit (voir <see cref="ControlLayout.DefaultCenter"/>).</summary>
        void BuildPedals(Transform parent)
        {
            AddHold(parent, DrivingControl.Throttle,
                () => controller?.SetThrottle(true), () => controller?.SetThrottle(false));

            AddHold(parent, DrivingControl.Lift,
                () => controller?.SetLift(true), () => controller?.SetLift(false));

            AddHold(parent, DrivingControl.FrontBrake,
                () => controller?.SetFrontBrake(true), () => controller?.SetFrontBrake(false));

            AddHold(parent, DrivingControl.RearBrake,
                () => controller?.SetRearBrake(true), () => controller?.SetRearBrake(false));
        }

        void BuildCameraDragZone(Transform root)
        {
            if (cameraRig == null) return;

            var zone = UIFactory.AddPanel(root, "CameraDragZone", new Color(0f, 0f, 0f, 0f),
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            zone.raycastTarget = true;
            // Transparente mais capte les glissements ; pas de coût de rendu plein écran sur mobile.
            zone.canvasRenderer.cullTransparentMesh = true;
            zone.gameObject.AddComponent<CameraDragZone>().rig = cameraRig;
        }

        /// <summary>Bandeau d'échec : ne capte aucun toucher, le joueur peut repartir pendant qu'il s'estompe.</summary>
        void BuildFallOverlay(Transform root)
        {
            // Le fond traverse l'écran d'un bord à l'autre ; ses textes, centrés, restent loin de la Dynamic Island.
            var band = UIFactory.AddPanel(root, "FallOverlay", new Color(0.55f, 0.04f, 0.04f, 0.6f),
                new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -75f), new Vector2(0f, 75f));
            band.raycastTarget = false;

            fallOverlay = band.gameObject.AddComponent<CanvasGroup>();
            fallOverlay.interactable = false;
            fallOverlay.blocksRaycasts = false;
            fallOverlay.alpha = 0f;

            UIFactory.AddText(band.transform, "FallTitle", "CHUTE !", UITheme.FontDisplay, Color.white, TextAnchor.MiddleCenter,
                new Vector2(0.15f, 0.5f), new Vector2(0.85f, 0.5f), new Vector2(0f, -10f), new Vector2(0f, 60f));
            // Le conseil dépend de ce qui a fait tomber le pilote : il est écrit à chaque chute (OnFell).
            fallHint = UIFactory.AddText(band.transform, "FallHint", "", UITheme.FontLabel, theme.TextMuted, TextAnchor.MiddleCenter,
                new Vector2(0.15f, 0.5f), new Vector2(0.85f, 0.5f), new Vector2(0f, -55f), new Vector2(0f, -15f));
        }

        void RefreshViewLabel()
        {
            displayedView = cameraRig.CurrentView;
            viewLabel.text = displayedView == CameraView.Exterior ? "VUE : EXT." : "VUE : 1re P.";
        }

        void UpdateSteer()
        {
            float value = (rightHeld ? 1f : 0f) - (leftHeld ? 1f : 0f);
            controller?.SetSteer(value);
        }

        /// <summary>
        /// Bouton à maintenir, posé à la place que le joueur lui a donnée (ou à celle d'origine), aux coins arrondis
        /// de l'iPhone.
        /// </summary>
        void AddHold(Transform parent, DrivingControl control, UnityAction onPressed, UnityAction onReleased)
        {
            var btn = UIFactory.AddButton(parent, control.ToString(), ControlLayout.Label(control), ControlColor, ControlTextColor,
                Mathf.RoundToInt(S(ControlLayout.FontSize(control))), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, null);
            layout.Place((RectTransform)btn.transform, control, layout.Center(control));
            btn.transition = Selectable.Transition.None;
            var background = btn.GetComponent<Image>();
            UIFactory.ApplyScreenCorners(background);

            var hold = btn.gameObject.AddComponent<HoldButton>();
            hold.OnPressed.AddListener(() => background.color = ControlPressedColor);
            hold.OnPressed.AddListener(onPressed);
            hold.OnReleased.AddListener(() => background.color = ControlColor);
            hold.OnReleased.AddListener(onReleased);
        }
    }
}
