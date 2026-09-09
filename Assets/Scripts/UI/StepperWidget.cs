using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace WheelingMoto.UI
{
    /// <summary>Sélecteur "- valeur +" utilisé pour la qualité graphique et les volumes (alternative tactile au Slider/Dropdown).</summary>
    public class StepperWidget
    {
        public GameObject Root;
        public TextMeshProUGUI Label;
        public Button PrevButton;
        public Button NextButton;
    }
}
