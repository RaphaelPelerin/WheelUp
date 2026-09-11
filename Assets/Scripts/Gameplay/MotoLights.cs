using UnityEngine;
using WheelingMoto.Core;

namespace WheelingMoto.Gameplay
{
    /// <summary>
    /// Feux de la moto. La nuit : phare allumé (optique lumineuse et vrai faisceau sur la route, qui suit le
    /// guidon) et feu arrière en veilleuse. De jour, l'optique du phare garde un feu de jour. De jour comme de
    /// nuit : feu stop vif au freinage (avec une lueur rouge derrière la moto la nuit), et clignotants qui battent
    /// du côté où l'on tourne en manœuvre, à faible vitesse.
    /// Les optiques du modèle reçoivent un matériau émissif dédié, référencé par la scène pour que la variante
    /// émissive du shader soit incluse dans les builds.
    /// </summary>
    public class MotoLights : MonoBehaviour
    {
        [Header("Phare")]
        public Color headlightColor = new Color(1f, 0.95f, 0.85f);
        [Tooltip("Éclat de l'optique du phare de jour (feu de jour).")]
        public float headlightDayGlow = 1.2f;
        [Tooltip("Éclat de l'optique du phare la nuit.")]
        public float headlightNightGlow = 4f;
        [Tooltip("Faisceau du phare sur la route, la nuit.")]
        public float beamIntensity = 5f;
        public float beamRange = 45f;
        public float beamAngle = 70f;
        [Tooltip("Faisceau incliné vers la route, en degrés.")]
        public float beamDownTilt = 6f;

        [Header("Feu arrière")]
        public Color tailColor = new Color(1f, 0.04f, 0.02f);
        [Tooltip("Veilleuse, la nuit.")]
        public float tailNightGlow = 1.2f;
        [Tooltip("Feu stop au freinage, de jour comme de nuit.")]
        public float brakeGlow = 5f;
        [Tooltip("Lueur rouge projetée derrière la moto au freinage, la nuit.")]
        public float brakeLightIntensity = 1.5f;

        [Header("Clignotants")]
        public Color indicatorColor = new Color(1f, 0.45f, 0.02f);
        public float indicatorGlow = 4f;
        [Tooltip("Battements par seconde.")]
        public float blinkRate = 1.6f;
        [Tooltip("Vitesse (m/s) sous laquelle tourner fait clignoter le côté du virage.")]
        public float indicatorMaxSpeed = 6f;

        static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        MotorcycleController bike;
        bool night;
        Material headlight, tail, indicatorLeft, indicatorRight;
        Light beam, brakeLight;

        public void Setup(MotorcycleController owner, Transform model, MotoRig rig, Material lightMaterial)
        {
            bike = owner;
            night = GameSession.SelectedTime == TimeOfDay.Nuit;
            if (lightMaterial == null)
            {
                Debug.LogWarning("[MotoLights] Pas de matériau émissif référencé : les feux risquent de rester éteints dans les builds.", this);
            }

            Renderer headlightRenderer = PartRenderer(model, rig.HeadlightPart);
            Renderer tailRenderer = PartRenderer(model, rig.TailLightPart);
            headlight = Lens(headlightRenderer, lightMaterial, new Color(0.75f, 0.75f, 0.72f));
            tail = Lens(tailRenderer, lightMaterial, new Color(0.35f, 0.02f, 0.02f));
            indicatorLeft = Lens(PartRenderer(model, rig.IndicatorLeftPart), lightMaterial, new Color(0.4f, 0.2f, 0.02f));
            indicatorRight = Lens(PartRenderer(model, rig.IndicatorRightPart), lightMaterial, new Color(0.4f, 0.2f, 0.02f));

            if (night && headlightRenderer != null)
            {
                // Rattaché à l'optique : le faisceau tourne avec le guidon et suit l'assiette de la moto.
                beam = CreateLight("HeadlightBeam", headlightRenderer.transform, headlightRenderer.bounds.center,
                    Quaternion.LookRotation(model.forward, model.up) * Quaternion.Euler(beamDownTilt, 0f, 0f));
                beam.type = LightType.Spot;
                beam.range = beamRange;
                beam.spotAngle = beamAngle;
                beam.innerSpotAngle = beamAngle * 0.55f;
                beam.intensity = beamIntensity;
                beam.color = headlightColor;
            }

            if (night && tailRenderer != null)
            {
                brakeLight = CreateLight("BrakeGlow", tailRenderer.transform, tailRenderer.bounds.center - model.forward * 0.15f,
                    Quaternion.identity);
                brakeLight.type = LightType.Point;
                brakeLight.range = 3f;
                brakeLight.intensity = brakeLightIntensity;
                brakeLight.color = tailColor;
                brakeLight.enabled = false;
            }
        }

        void Update()
        {
            if (bike == null) return;

            bool braking = bike.BrakeHeld && !bike.IsFallen;
            SetGlow(headlight, headlightColor, night ? headlightNightGlow : headlightDayGlow);
            SetGlow(tail, tailColor, braking ? brakeGlow : night ? tailNightGlow : 0f);
            if (brakeLight != null) brakeLight.enabled = braking;

            float steer = bike.SteerInput;
            bool manoeuvre = Mathf.Abs(bike.SignedSpeed) < indicatorMaxSpeed && Mathf.Abs(steer) > 0.3f && !bike.IsFallen;
            bool blinkOn = Mathf.Repeat(Time.time * blinkRate, 1f) < 0.5f;
            SetGlow(indicatorLeft, indicatorColor, manoeuvre && steer < 0f && blinkOn ? indicatorGlow : 0f);
            SetGlow(indicatorRight, indicatorColor, manoeuvre && steer > 0f && blinkOn ? indicatorGlow : 0f);
        }

        static Renderer PartRenderer(Transform model, string suffix)
        {
            if (string.IsNullOrEmpty(suffix)) return null;
            Transform part = MotoRigs.Part(model, suffix);
            return part != null ? part.GetComponent<Renderer>() : null;
        }

        /// <summary>Optique du modèle passée sur une copie du matériau émissif, teintée comme un verre éteint.</summary>
        static Material Lens(Renderer renderer, Material template, Color glassColor)
        {
            if (renderer == null) return null;

            Material lens = template != null ? new Material(template) : new Material(renderer.sharedMaterial);
            lens.name = renderer.name + " (feu)";
            lens.EnableKeyword("_EMISSION");
            if (lens.HasProperty(BaseColorId)) lens.SetColor(BaseColorId, glassColor);
            lens.SetColor(EmissionColorId, Color.black);

            var materials = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++) materials[i] = lens;
            renderer.sharedMaterials = materials;
            return lens;
        }

        static Light CreateLight(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            var go = new GameObject(name);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.SetParent(parent, true);
            var light = go.AddComponent<Light>();
            light.shadows = LightShadows.None;
            return light;
        }

        static void SetGlow(Material material, Color color, float intensity)
        {
            if (material == null) return;
            material.SetColor(EmissionColorId, color * intensity);
        }
    }
}
