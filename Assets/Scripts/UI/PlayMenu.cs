using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WheelingMoto.Core;
using WheelingMoto.Data;

namespace WheelingMoto.UI
{
    /// <summary>Onglet Jouer & Cartes : mode (Défis/Course), sélection de carte, lancement de la partie.</summary>
    public class PlayMenu
    {
        public GameObject Root { get; private set; }

        UITheme theme;
        GameMode selectedMode;
        MapId selectedMap;
        TimeOfDay selectedTime;

        Button defisButton;
        Button courseButton;
        Button dayButton;
        Button nightButton;
        readonly Dictionary<MapId, Button> mapButtons = new Dictionary<MapId, Button>();

        public void Build(Transform parent, UITheme t)
        {
            theme = t;
            selectedMode = GameSession.SelectedMode;
            selectedMap = GameSession.SelectedMap;
            selectedTime = GameSession.SelectedTime;

            var panel = UIFactory.AddPanel(parent, "PlayPanel", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Root = panel.gameObject;

            UIFactory.AddText(Root.transform, "Title", "JOUER", 30, theme.Text, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -50), new Vector2(-30, -6));

            UIFactory.AddText(Root.transform, "ModeLabel", "Mode de jeu", 18, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -84), new Vector2(-30, -60));

            defisButton = UIFactory.AddButton(Root.transform, "DefisButton", "DÉFIS", theme.PanelAlt, theme.Text, 20,
                new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(30, -148), new Vector2(-8, -92), () => SelectMode(GameMode.Defis));

            courseButton = UIFactory.AddButton(Root.transform, "CourseButton", "COURSE", theme.PanelAlt, theme.Text, 20,
                new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(8, -148), new Vector2(-30, -92), () => SelectMode(GameMode.Course));

            UIFactory.AddText(Root.transform, "MapLabel", "Sélection de la carte", 18, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -186), new Vector2(-30, -162));

            var mapArea = UIFactory.AddPanel(Root.transform, "MapArea", Color.clear,
                new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, 130), new Vector2(-20, -196));

            int count = MapCatalog.All.Length;
            float gap = 16f;
            for (int i = 0; i < count; i++)
            {
                var map = MapCatalog.All[i];
                float xMin = (float)i / count;
                float xMax = (float)(i + 1) / count;
                var card = UIFactory.AddButton(mapArea.transform, "Map_" + map.Id, "", theme.PanelAlt, theme.Text, 20,
                    new Vector2(xMin, 0f), new Vector2(xMax, 1f), new Vector2(gap / 2f, 0), new Vector2(-gap / 2f, 0),
                    () => SelectMap(map.Id));

                UIFactory.AddText(card.transform, "Name", map.Name, 22, theme.Text, TextAnchor.UpperLeft,
                    Vector2.zero, Vector2.one, new Vector2(18, -60), new Vector2(-18, -14));
                UIFactory.AddText(card.transform, "Desc", map.Description, 15, theme.TextMuted, TextAnchor.UpperLeft,
                    Vector2.zero, Vector2.one, new Vector2(18, 14), new Vector2(-18, -66));

                mapButtons[map.Id] = card;
            }

            UIFactory.AddText(Root.transform, "TimeLabel", "Moment de la journée", 16, theme.TextMuted, TextAnchor.MiddleLeft,
                new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(30, 100), new Vector2(-10, 124));

            dayButton = UIFactory.AddButton(Root.transform, "DayButton", "JOUR", theme.PanelAlt, theme.Text, 20,
                new Vector2(0f, 0f), new Vector2(0.25f, 0f), new Vector2(30, 20), new Vector2(-6, 94), () => SelectTime(TimeOfDay.Jour));

            nightButton = UIFactory.AddButton(Root.transform, "NightButton", "NUIT", theme.PanelAlt, theme.Text, 20,
                new Vector2(0.25f, 0f), new Vector2(0.5f, 0f), new Vector2(6, 20), new Vector2(-10, 94), () => SelectTime(TimeOfDay.Nuit));

            UIFactory.AddButton(Root.transform, "PlayButton", "JOUER", theme.Accent, Color.white, 26,
                new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(10, 20), new Vector2(-30, 110), OnPlayPressed);

            RefreshSelectionVisuals();
        }

        public void RefreshOnShow()
        {
            selectedMode = GameSession.SelectedMode;
            selectedMap = GameSession.SelectedMap;
            selectedTime = GameSession.SelectedTime;
            RefreshSelectionVisuals();
        }

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

        void SelectMap(MapId id)
        {
            selectedMap = id;
            GameSession.SelectedMap = id;
            RefreshSelectionVisuals();
        }

        void RefreshSelectionVisuals()
        {
            UIFactory.SetButtonColor(defisButton, selectedMode == GameMode.Defis ? theme.Accent : theme.PanelAlt);
            UIFactory.SetButtonColor(courseButton, selectedMode == GameMode.Course ? theme.Accent : theme.PanelAlt);
            UIFactory.SetButtonColor(dayButton, selectedTime == TimeOfDay.Jour ? theme.Accent : theme.PanelAlt);
            UIFactory.SetButtonColor(nightButton, selectedTime == TimeOfDay.Nuit ? theme.Accent : theme.PanelAlt);

            foreach (var kv in mapButtons)
            {
                UIFactory.SetButtonColor(kv.Value, kv.Key == selectedMap ? theme.Accent : theme.PanelAlt);
            }
        }

        void OnPlayPressed()
        {
            // V1 : seule la carte Métropole est jouable (free-roam) ; les autres cartes/modes restent à construire.
            SceneLoader.LoadMetropole();
        }
    }
}
