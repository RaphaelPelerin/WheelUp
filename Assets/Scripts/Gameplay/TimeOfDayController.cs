using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using WheelingMoto.Core;

namespace WheelingMoto.Gameplay
{
    /// <summary>
    /// Jour ou nuit sur la Métropole, selon le choix du menu Jouer (GameSession.SelectedTime).
    /// La scène est construite de nuit : ciel étoilé, lightmaps, sondes de lumière et reflets précalculés.
    /// La nuit, on la laisse telle quelle. Le jour, cet éclairage cuit est retiré au profit d'un soleil en
    /// temps réel avec ombres, du ciel procédural, d'une ambiance claire, d'une brume légère et d'un reflet
    /// du ciel de jour rendu au lancement.
    /// </summary>
    public class TimeOfDayController : MonoBehaviour
    {
        [Header("Jour")]
        [Tooltip("Ciel de jour : Default-Skybox d'Unity, procédural, qui dessine le soleil dans la direction de la lumière.")]
        public Material daySkybox;
        public Color sunColor = new Color(1f, 0.96f, 0.88f);
        public float sunIntensity = 1.6f;
        [Tooltip("Hauteur (x) et cap (y) du soleil, en degrés.")]
        public Vector2 sunAngles = new Vector2(48f, -35f);

        [Header("Ambiance de jour")]
        public Color ambientSky = new Color(0.62f, 0.72f, 0.85f);
        public Color ambientEquator = new Color(0.55f, 0.57f, 0.58f);
        public Color ambientGround = new Color(0.32f, 0.3f, 0.27f);
        [Tooltip("Brume de jour (exponentielle carrée) : donne de la profondeur aux rues.")]
        public Color fogColor = new Color(0.72f, 0.8f, 0.88f);
        public float fogDensity = 0.0025f;

        const int DayReflectionResolution = 256;
        const float DayReflectionSize = 5000f;

        // Start et non Awake : la moto et son pilote, créés dans les Awake, doivent aussi passer au jour.
        void Start()
        {
            if (GameSession.SelectedTime == TimeOfDay.Jour)
            {
                ApplyDay();
            }
        }

        void ApplyDay()
        {
            // La lumière de nuit ne sert qu'aux lightmaps : on la coupe et on pose un vrai soleil.
            if (RenderSettings.sun != null) RenderSettings.sun.enabled = false;

            var sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(transform, false);
            sunObject.transform.rotation = Quaternion.Euler(sunAngles.x, sunAngles.y, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = sunColor;
            sun.intensity = sunIntensity;
            sun.shadows = LightShadows.Soft;
            // Biais propres au soleil : sans eux, les ombres « grésillent » sur la moto et le pilote (acné d'ombre).
            if (!sunObject.TryGetComponent(out UniversalAdditionalLightData lightData))
            {
                lightData = sunObject.AddComponent<UniversalAdditionalLightData>();
            }
            lightData.usePipelineSettings = false;
            sun.shadowBias = 0.1f;
            sun.shadowNormalBias = 0.6f;
            RenderSettings.sun = sun;

            if (daySkybox != null) RenderSettings.skybox = daySkybox;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.ambientIntensity = 1f;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;

            // Éclairage de nuit précalculé retiré : les objets passent au soleil et à l'ambiance de jour.
            LightmapSettings.lightmaps = new LightmapData[0];
            foreach (Renderer r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                r.lightmapIndex = -1;
                r.lightProbeUsage = LightProbeUsage.Off;
            }
            foreach (ReflectionProbe probe in FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
            {
                probe.enabled = false;
            }

            // Sans lui, les surfaces brillantes refléteraient encore le ciel de nuit. Ciel seul : rendu quasi gratuit.
            var reflectionObject = new GameObject("DayReflection");
            reflectionObject.transform.SetParent(transform, false);
            ReflectionProbe dayReflection = reflectionObject.AddComponent<ReflectionProbe>();
            dayReflection.mode = ReflectionProbeMode.Realtime;
            dayReflection.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            dayReflection.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            dayReflection.clearFlags = ReflectionProbeClearFlags.Skybox;
            dayReflection.cullingMask = 0;
            dayReflection.resolution = DayReflectionResolution;
            dayReflection.size = Vector3.one * DayReflectionSize;
            dayReflection.RenderProbe();
        }
    }
}
