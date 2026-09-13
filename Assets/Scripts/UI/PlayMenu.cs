using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Core;
using WheelingMoto.Data;
using WheelingMoto.Gameplay;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Onglet Jouer, en deux colonnes : la carte à gauche, dans un carrousel qu'on fait défiler à
    /// la flèche, et la moto à droite. Chaque réglage est rangé sous le visuel qu'il concerne — le
    /// mode de jeu sous la carte, le moment de la journée sous la moto — et JOUER ferme la colonne
    /// de droite, au bout du parcours du regard.
    ///
    /// Carte et moto sont montrées en volume parce que c'est ici qu'on appuie sur JOUER : partir
    /// sans voir ni sur quoi ni où on part était le principal angle mort du menu.
    ///
    /// La carte affichée par le carrousel fait office de sélection. Quand elle est verrouillée,
    /// JOUER s'éteint et le bouton de déblocage prend sa place sous l'aperçu : le joueur peut donc
    /// se promener dans les cartes qu'il ne possède pas sans jamais se retrouver à lancer une partie
    /// sur l'une d'elles.
    /// </summary>
    public class PlayMenu
    {
        public GameObject Root { get; private set; }

        /// <summary>Levé par « Changer de moto » : au menu principal de basculer sur l'onglet Garage.</summary>
        public Action GarageRequested;

        // Deux colonnes, en fractions de la zone de contenu. Chaque réglage est rangé sous le visuel
        // qu'il concerne : le mode de jeu sous la carte, le moment de la journée sous la moto. Le
        // joueur règle ainsi ce qu'il regarde, au lieu de faire l'aller-retour vers une colonne de
        // réglages séparée.
        const float MidStart = 0f;
        const float MidEnd = 0.62f;
        const float RightStart = 0.64f;

        /// <summary>
        /// Hauteur commune à tous les boutons de l'écran. Une constante plutôt que six mesures
        /// posées à la main : c'est ce qui garantit que les deux colonnes gardent le même rythme, et
        /// qu'une retouche de gabarit les emporte toutes les deux au lieu d'en désaligner une.
        /// </summary>
        const float ButtonHeight = 66f;

        /// <summary>Espace entre deux rangées de boutons empilées.</summary>
        const float ButtonGap = 10f;

        /// <summary>
        /// Bas de la rangée la plus basse, commun aux deux colonnes : c'est lui qui met JOUER et les
        /// boutons de mode sur la même ligne d'horizon. Les deux conteneurs doivent donc partager la
        /// même base — la colonne de la moto n'a plus de marge basse depuis qu'elle n'a plus de fond.
        /// </summary>
        const float BottomRowY = 26f;

        // Bas de chaque rangée, mesuré depuis le bas de sa colonne.
        const float ModeRowY = BottomRowY;
        const float UnlockRowY = 142f;
        const float PlayRowY = BottomRowY;
        const float TimeRowY = PlayRowY + ButtonHeight + ButtonGap;
        const float ChangeRowY = TimeRowY + ButtonHeight + ButtonGap;

        UITheme theme;
        GameMode selectedMode;
        TimeOfDay selectedTime;

        Button defisButton;
        Button courseButton;
        Button dayButton;
        Button nightButton;
        Button playButton;
        TextMeshProUGUI playLabel;

        MapCarousel carousel;
        TextMeshProUGUI mapName;
        TextMeshProUGUI mapState;
        Button unlockButton;
        TextMeshProUGUI unlockLabel;
        InfoButton mapInfo;

        MotoPreview motoPreview;
        TextMeshProUGUI motoPlaceholder;
        TextMeshProUGUI motoName;
        TextMeshProUGUI motoDetail;

        public void Build(Transform parent, UITheme t)
        {
            theme = t;
            selectedMode = GameSession.SelectedMode;
            selectedTime = GameSession.SelectedTime;

            var panel = UIFactory.AddPanel(parent, "PlayPanel", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Root = panel.gameObject;

            BuildMapColumn(Root.transform);
            BuildMotoColumn(Root.transform);

            RefreshSelectionVisuals();
            RefreshMap();
        }

        // ---------------------------------------------------------------- colonne de la carte

        void BuildMapColumn(Transform root)
        {
            UIFactory.AddText(root, "Title", "JOUER", UITheme.FontTitle, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(MidStart, 1), new Vector2(MidEnd, 1), new Vector2(30, -56), new Vector2(-10, -6));

            carousel = MapCarousel.Create(root, theme, GameSession.SelectedMap,
                new Vector2(MidStart, 0f), new Vector2(MidEnd, 1f),
                new Vector2(30, 320), new Vector2(-10, -62));

            carousel.Changed += OnMapShown;

            mapName = UIFactory.AddText(root, "MapName", "", UITheme.FontTitle, theme.Text, TextAnchor.MiddleCenter,
                new Vector2(MidStart, 0f), new Vector2(MidEnd, 0f), new Vector2(30, 254), new Vector2(-10, 302),
                FontStyles.Bold | FontStyles.Italic);

            // La description passe derrière la pastille, posée au bout du nom : sous l'aperçu, elle
            // imposerait un corps trop petit, et c'est le nom de la carte qui en pâtirait.
            mapInfo = UIFactory.AddInfoButton(root, "MapInfo", theme, "", "",
                new Vector2(MidEnd, 0f), new Vector2(MidEnd, 0f),
                new Vector2(-10 - ButtonHeight, 245), new Vector2(-10, 245 + ButtonHeight));

            mapState = UIFactory.AddText(root, "MapState", "", UITheme.FontBody, theme.TextMuted, TextAnchor.MiddleCenter,
                new Vector2(MidStart, 0f), new Vector2(MidEnd, 0f), new Vector2(30, 216), new Vector2(-10, 250));

            unlockButton = UIFactory.AddButton(root, "UnlockButton", "DÉBLOQUER", theme.Accent, Color.white,
                UITheme.FontLabel, new Vector2(MidStart, 0f), new Vector2(MidEnd, 0f),
                new Vector2(130, UnlockRowY), new Vector2(-110, UnlockRowY + ButtonHeight), OnUnlockPressed);

            unlockLabel = unlockButton.GetComponentInChildren<TextMeshProUGUI>();

            // Le mode se règle sous la carte : c'est elle qu'il qualifie, un défi et une course ne se
            // courent pas sur le même terrain.
            UIFactory.AddText(root, "ModeLabel", "Mode de jeu", UITheme.FontBody, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(MidStart, 0f), new Vector2(MidEnd, 0f), new Vector2(30, 100), new Vector2(-10, 132));

            float modeSplit = (MidStart + MidEnd) / 2f;

            defisButton = UIFactory.AddButton(root, "DefisButton", "DÉFIS", theme.PanelAlt, theme.Text, UITheme.FontLabel,
                new Vector2(MidStart, 0f), new Vector2(modeSplit, 0f),
                new Vector2(30, ModeRowY), new Vector2(-6, ModeRowY + ButtonHeight),
                () => SelectMode(GameMode.Defis));

            courseButton = UIFactory.AddButton(root, "CourseButton", "COURSE", theme.PanelAlt, theme.Text, UITheme.FontLabel,
                new Vector2(modeSplit, 0f), new Vector2(MidEnd, 0f),
                new Vector2(6, ModeRowY), new Vector2(-10, ModeRowY + ButtonHeight),
                () => SelectMode(GameMode.Course));
        }

        // ---------------------------------------------------------------- colonne de la moto

        void BuildMotoColumn(Transform root)
        {
            // Aucun aplat derrière la moto : le halo suffit à la détacher du fond, et un pavé gris
            // autour d'elle l'enfermait dans une vignette au lieu de la poser dans l'écran.
            var column = UIFactory.AddPanel(root, "MotoColumn", Color.clear,
                new Vector2(RightStart, 0f), new Vector2(1f, 1f), new Vector2(10, 0), new Vector2(-30, -6));
            column.raycastTarget = false;

            UIFactory.AddText(column.transform, "Heading", "TA MOTO", UITheme.FontBody, theme.TextMuted,
                TextAnchor.MiddleLeft, new Vector2(0f, 1f), new Vector2(1f, 1f),
                new Vector2(24, -56), new Vector2(-24, -18));

            var slot = UIFactory.AddPanel(column.transform, "Preview", Color.clear,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(20, 344), new Vector2(-20, -64));
            slot.raycastTarget = false;

            Storefront.AddPreviewGlow(slot.transform, new Color(0.42f, 0.46f, 0.62f, 0.18f));

            motoPlaceholder = UIFactory.AddText(slot.transform, "Missing", "", UITheme.FontBody, theme.NavTextInactive,
                TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10),
                FontStyles.Italic);

            var render = UIFactory.AddRawImage(slot.transform, "Render", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            motoPreview = MotoPreview.Create(render);

            motoName = UIFactory.AddText(column.transform, "Name", "", UITheme.FontHeading, theme.Text,
                TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24, 286), new Vector2(-24, 334), FontStyles.Bold | FontStyles.Italic);

            motoDetail = UIFactory.AddText(column.transform, "Detail", "", UITheme.FontBody, theme.TextMuted,
                TextAnchor.MiddleLeft, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24, 248), new Vector2(-24, 282));

            UIFactory.AddButton(column.transform, "ChangeMoto", "CHANGER DE MOTO", theme.PanelAlt, theme.Text,
                UITheme.FontLabel, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24, ChangeRowY), new Vector2(-24, ChangeRowY + ButtonHeight), () => GarageRequested?.Invoke());

            // Le moment de la journée se règle sous la moto, juste au-dessus du départ : c'est le
            // dernier réglage que le joueur croise avant d'appuyer.
            dayButton = UIFactory.AddButton(column.transform, "DayButton", "JOUR", theme.PanelAlt, theme.Text,
                UITheme.FontLabel, new Vector2(0f, 0f), new Vector2(0.5f, 0f),
                new Vector2(24, TimeRowY), new Vector2(-6, TimeRowY + ButtonHeight), () => SelectTime(TimeOfDay.Jour));

            nightButton = UIFactory.AddButton(column.transform, "NightButton", "NUIT", theme.PanelAlt, theme.Text,
                UITheme.FontLabel, new Vector2(0.5f, 0f), new Vector2(1f, 0f),
                new Vector2(6, TimeRowY), new Vector2(-24, TimeRowY + ButtonHeight), () => SelectTime(TimeOfDay.Nuit));

            // L'action principale ferme la colonne. Elle est la plus basse et la plus large de
            // l'écran : c'est là que le pouce finit sa descente, et rien ne la borde qu'on puisse
            // presser à sa place.
            playButton = UIFactory.AddButton(column.transform, "PlayButton", "JOUER", theme.Accent, Color.white,
                UITheme.FontHeading, new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(24, PlayRowY), new Vector2(-24, PlayRowY + ButtonHeight), OnPlayPressed, ButtonKind.Primary);

            playLabel = playButton.GetComponentInChildren<TextMeshProUGUI>();
        }

        // ---------------------------------------------------------------- rafraîchissement

        public void RefreshOnShow()
        {
            selectedMode = GameSession.SelectedMode;
            selectedTime = GameSession.SelectedTime;

            // La carte mémorisée peut avoir cessé d'être jouable entre deux visites (progression
            // effacée) : on ramène le carrousel sur une carte réellement ouverte.
            var playable = MapOwnership.Playable(GameSession.SelectedMap);
            GameSession.SelectedMap = playable.Id;
            carousel.Show(playable.Id);

            RefreshMoto();
            RefreshMap();
            RefreshSelectionVisuals();
        }

        /// <summary>Appelé par le carrousel dès qu'une autre carte devient la carte affichée.</summary>
        void OnMapShown(MapInfo map)
        {
            // Seule une carte ouverte devient la carte de la prochaine partie ; parcourir les autres
            // ne doit rien changer à ce qui est sélectionné.
            if (MapOwnership.IsUnlocked(map)) GameSession.SelectedMap = map.Id;

            RefreshMap();
        }

        void RefreshMap()
        {
            var map = carousel.Current;
            bool unlocked = MapOwnership.IsUnlocked(map);

            mapName.text = map.Name;
            mapName.color = unlocked ? theme.Text : theme.TextMuted;

            mapInfo.Title = map.Name;
            mapInfo.Body = map.Description;

            unlockButton.gameObject.SetActive(!unlocked);

            if (unlocked)
            {
                mapState.text = "Débloquée";
                mapState.color = theme.TextMuted;
            }
            else
            {
                bool affordable = MapOwnership.CanAfford(map);
                mapState.text = $"{map.Price} pièces";
                mapState.color = theme.Coin;

                unlockButton.interactable = affordable;
                unlockLabel.text = affordable ? "DÉBLOQUER" : "SOLDE INSUFFISANT";
                UIFactory.SetButtonColor(unlockButton, affordable ? theme.Accent : theme.PanelAlt);
            }

            // On ne part pas sur une carte qu'on ne possède pas : le bouton le dit au lieu de la
            // remplacer en douce par une autre au moment du départ.
            playButton.interactable = unlocked;
            playLabel.text = unlocked ? "JOUER" : "VERROUILLÉE";
            UIFactory.SetButtonColor(playButton, unlocked ? theme.Accent : theme.PanelAlt);
        }

        void RefreshMoto()
        {
            var moto = Loadout.Selected;
            if (moto == null) return;

            motoName.text = moto.Name;

            bool has3D = motoPreview != null && motoPreview.Show(moto.ModelResourcePath);
            motoPlaceholder.gameObject.SetActive(!has3D);

            if (has3D)
            {
                MotoPainter.Apply(motoPreview.CurrentModel, moto.Name);
                motoDetail.text = moto.Category;
            }
            else
            {
                // La plupart des motos du catalogue n'ont pas encore de modèle. Le dire franchement
                // vaut mieux qu'un cadre vide, qui se lirait comme un bug.
                motoPlaceholder.text = moto.Name;
                motoDetail.text = moto.Category + " · modèle 3D à venir";
            }
        }

        void RefreshSelectionVisuals()
        {
            UIFactory.SetButtonColor(defisButton, selectedMode == GameMode.Defis ? theme.Accent : theme.PanelAlt);
            UIFactory.SetButtonColor(courseButton, selectedMode == GameMode.Course ? theme.Accent : theme.PanelAlt);
            UIFactory.SetButtonColor(dayButton, selectedTime == TimeOfDay.Jour ? theme.Accent : theme.PanelAlt);
            UIFactory.SetButtonColor(nightButton, selectedTime == TimeOfDay.Nuit ? theme.Accent : theme.PanelAlt);
        }

        // ---------------------------------------------------------------- actions

        void SelectMode(GameMode mode)
        {
            selectedMode = mode;
            GameSession.SelectedMode = mode;
            RefreshSelectionVisuals();
        }

        void SelectTime(TimeOfDay time)
        {
            selectedTime = time;
            GameSession.SelectedTime = time;
            RefreshSelectionVisuals();
        }

        void OnUnlockPressed()
        {
            var map = carousel.Current;
            if (!MapOwnership.Buy(map)) return;

            // Débloquer vaut sélection : c'est ce que le joueur vient de payer pour faire.
            GameSession.SelectedMap = map.Id;
            RefreshMap();
        }

        void OnPlayPressed()
        {
            // V1 : seule la Métropole existe en tant que scène. Les deux autres se débloquent et se
            // sélectionnent, mais mènent encore ici — la scène reste à construire.
            GameSession.SelectedMap = MapOwnership.Playable(carousel.Current.Id).Id;
            SceneLoader.LoadMetropole();
        }
    }
}
