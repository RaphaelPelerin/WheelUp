using UnityEngine;
using UnityEngine.Rendering;

namespace WheelingMoto.UI
{
    /// <summary>
    /// Mise en lumière de l'ouverture d'un coffre : poussières en suspension, étincelles aspirées
    /// pendant la montée en tension, éclatement, onde de choc, colonne de lumière et braises qui
    /// montent. Tout est monté sur le plateau de l'aperçu, donc filmé par sa caméra dédiée.
    /// </summary>
    [DisallowMultipleComponent]
    public class ChestFx : MonoBehaviour
    {
        ParticleSystem dust;
        ParticleSystem charge;
        ParticleSystem burst;
        ParticleSystem embers;
        ParticleSystem shockwave;
        ParticleSystem beam;
        ParticleSystem core;

        Light keyLight;
        Color rarity = Color.white;
        float lightPulse;

        /// <summary>
        /// Monte l'effet sur un plateau d'aperçu.
        ///
        /// Pas de bloom : activer le post-traitement sur cette caméra la soumettrait au profil de
        /// volume par défaut du projet, qui force grain, vignette, profondeur de champ et flou de
        /// mouvement. La lumière vient donc des particules elles-mêmes — cœurs surexposés, colonne
        /// large, halo d'UI — et d'une vraie lumière ponctuelle sur le modèle.
        /// </summary>
        public static ChestFx Create(Transform stage)
        {
            var go = new GameObject("ChestFx");
            go.transform.SetParent(stage, false);

            var fx = go.AddComponent<ChestFx>();

            try
            {
                fx.Build();
            }
            catch (System.Exception error)
            {
                // L'effet est décoratif : une configuration refusée par le moteur ne doit pas
                // empêcher d'ouvrir un coffre. On le désactive et la séquence continue sans lui.
                Debug.LogWarning($"Effets de coffre désactivés : {error.Message}");
                Destroy(go);
                return null;
            }

            return fx;
        }

        void Build()
        {
            dust = BuildSystem("Dust", FxAssets.DotMaterial, loop: true);
            ConfigureDust();

            charge = BuildSystem("Charge", FxAssets.DotMaterial, loop: true);
            ConfigureCharge();

            burst = BuildSystem("Burst", FxAssets.ChipMaterial, loop: false);
            ConfigureBurst();

            shockwave = BuildSystem("Shockwave", FxAssets.RingMaterial, loop: false);
            ConfigureShockwave();

            beam = BuildSystem("Beam", FxAssets.DotMaterial, loop: false);
            ConfigureBeam();

            embers = BuildSystem("Embers", FxAssets.DotMaterial, loop: true);
            ConfigureEmbers();

            core = BuildSystem("Core", FxAssets.DotMaterial, loop: false);
            ConfigureCore();

            var lightObject = new GameObject("FxLight");
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            keyLight = lightObject.AddComponent<Light>();
            keyLight.type = LightType.Point;
            keyLight.range = 6f;
            keyLight.intensity = 0f;
            keyLight.shadows = LightShadows.None;

            Calm();
        }

        ParticleSystem BuildSystem(string name, Material material, bool loop)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var ps = go.AddComponent<ParticleSystem>();

            // AddComponent démarre le système immédiatement (playOnAwake est vrai par défaut et
            // Awake a déjà tourné). Or le moteur refuse qu'on change la durée d'un système en
            // lecture. On l'arrête donc avant de le configurer.
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = loop;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return ps;
        }

        void ConfigureDust()
        {
            var main = dust.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.2f, 4.0f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.09f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.035f);
            main.maxParticles = 120;

            var emission = dust.emission;
            emission.rateOverTime = 9f;

            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.25f;

            var velocity = dust.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            // Les trois axes doivent partager le même mode de courbe. N'en poser qu'un laisse les
            // deux autres en constante et Unity rejette le module entier au montage.
            velocity.x = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.03f, 0.10f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);

            FadeInOut(dust, 0.55f);
        }

        void ConfigureCharge()
        {
            var main = charge.main;
            main.startLifetime = 0.62f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(-2.6f, -1.5f); // vitesse négative : vers le centre
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.055f);
            main.maxParticles = 400;

            var emission = charge.emission;
            emission.rateOverTime = 0f; // piloté par Charge()

            var shape = charge.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 1.7f;
            shape.radiusThickness = 0f; // émission depuis la coque uniquement

            var size = charge.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, Ramp(0.2f, 1f));

            FadeInOut(charge, 0.35f);
        }

        void ConfigureBurst()
        {
            var main = burst.main;
            main.duration = 1.8f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.1f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 5.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.15f);
            main.gravityModifier = 1.4f;
            main.maxParticles = 260;

            // Rotation de départ aléatoire sur les trois axes. En script ces angles sont en radians,
            // contrairement à l'inspecteur qui les affiche en degrés.
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var emission = burst.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 140) });

            var shape = burst.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 55f;
            shape.radius = 0.3f;
            shape.rotation = new Vector3(-90f, 0f, 0f); // cône ouvert vers le haut

            // Culbutage : c'est lui qui fait lire du papier qui tombe plutôt que des points en vol.
            var rotation = burst.rotationOverLifetime;
            rotation.enabled = true;
            rotation.separateAxes = true;
            rotation.x = new ParticleSystem.MinMaxCurve(-5.5f, 5.5f);
            rotation.y = new ParticleSystem.MinMaxCurve(-5.5f, 5.5f);
            rotation.z = new ParticleSystem.MinMaxCurve(-3.0f, 3.0f);

            // Freinage progressif : les confettis flottent au lieu de filer tout droit.
            var damping = burst.limitVelocityOverLifetime;
            damping.enabled = true;
            damping.dampen = 0.12f;
            damping.limit = new ParticleSystem.MinMaxCurve(1.6f);

            // Rendu en quad plutôt qu'en panneau face caméra : sans cela la rotation 3D ne se verrait
            // pas, un billboard restant toujours de face. Le matériau désactive le culling des faces.
            var renderer = burst.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = FxAssets.Quad;
            renderer.alignment = ParticleSystemRenderSpace.Local;

            FadeInOut(burst, 0.06f);
        }

        void ConfigureShockwave()
        {
            var main = shockwave.main;
            main.duration = 1f;
            main.startLifetime = 0.55f;
            main.startSpeed = 0f;
            main.startSize = 0.6f;
            main.maxParticles = 4;

            var emission = shockwave.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            var shape = shockwave.shape;
            shape.enabled = false;

            var size = shockwave.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(6.5f, Ramp(0.05f, 1f));

            var renderer = shockwave.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard; // l'onde reste à plat

            FadeInOut(shockwave, 0.08f);
        }

        void ConfigureBeam()
        {
            var main = beam.main;
            main.duration = 2f;
            main.startLifetime = 1.5f;
            main.startSpeed = 0f;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(0.55f, 0.9f);
            main.startSizeY = 2.6f;
            main.maxParticles = 6;

            var emission = beam.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2) });

            var shape = beam.shape;
            shape.enabled = false;

            // La demi-hauteur visible de la caméra d'aperçu vaut 2.14 unités : au-delà, la colonne
            // serait tranchée net par le bord du rendu. Le dégradé radial de la texture, étiré, fait
            // le fondu haut et bas tout seul.
            beam.transform.localPosition = new Vector3(0f, 0.75f, 0f);

            var size = beam.sizeOverLifetime;
            size.enabled = true;
            size.separateAxes = true;
            size.x = new ParticleSystem.MinMaxCurve(1f, Ramp(0.15f, 1f));
            size.y = new ParticleSystem.MinMaxCurve(1f, Ramp(0.4f, 1f));
            size.z = new ParticleSystem.MinMaxCurve(1f, Ramp(0.15f, 1f));

            FadeInOut(beam, 0.18f);
        }

        void ConfigureEmbers()
        {
            var main = embers.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.25f, 0.75f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.05f);
            main.maxParticles = 160;

            var emission = embers.emission;
            emission.rateOverTime = 0f; // allumé par Burst()

            var shape = embers.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.4f, 0.1f, 1.0f);

            var velocity = embers.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);

            var size = embers.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, Ramp(1f, 0f));

            FadeInOut(embers, 0.3f);
        }

        /// <summary>
        /// Cœur surexposé de l'éclat : deux grosses taches presque blanches qui enflent puis
        /// disparaissent. Sans bloom, c'est ce qui donne la sensation de lumière crue.
        /// </summary>
        void ConfigureCore()
        {
            var main = core.main;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.16f, 0.28f);
            main.startSpeed = 0f;
            // Volontairement contenu : la caméra d'aperçu ne voit que 4.3 unités de haut, un cœur plus
            // large repeignait tout le cadre et noyait les confettis dans la couleur de la rareté.
            main.startSize = new ParticleSystem.MinMaxCurve(0.7f, 1.15f);
            main.maxParticles = 6;

            var emission = core.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 2) });

            var shape = core.shape;
            shape.enabled = false;

            var size = core.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, Ramp(0.4f, 1.25f));

            FadeInOut(core, 0.06f);
        }

        /// <summary>Courbe linéaire simple, de `from` à `to` sur la durée de vie.</summary>
        static AnimationCurve Ramp(float from, float to)
        {
            return AnimationCurve.EaseInOut(0f, from, 1f, to);
        }

        /// <summary>Apparition puis disparition en fondu, pour qu'aucune particule ne surgisse net.</summary>
        static void FadeInOut(ParticleSystem ps, float fadeIn)
        {
            var color = ps.colorOverLifetime;
            color.enabled = true;

            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, Mathf.Clamp01(fadeIn)),
                    new GradientAlphaKey(0f, 1f),
                });
            color.color = new ParticleSystem.MinMaxGradient(gradient);
        }

        /// <summary>
        /// Deux couleurs, pas une : <paramref name="light"/> pour ce qui fait office de source
        /// lumineuse (colonne, lumière ponctuelle), <paramref name="confetti"/> pour tout ce qui est
        /// grain visible. Le halo de l'écran porte déjà la teinte de la rareté ; des étincelles de la
        /// même couleur y sont invisibles.
        /// </summary>
        public void SetRarity(Color light, Color confetti)
        {
            rarity = light;

            Tint(dust, confetti, 0.75f);
            Tint(charge, Color.Lerp(confetti, Color.white, 0.25f), 1f);
            // Deux teintes tirées au hasard par confetti : un jet monochrome fait tache plate.
            var burstMain = burst.main;
            burstMain.startColor = new ParticleSystem.MinMaxGradient(
                confetti, Color.Lerp(confetti, Color.white, 0.55f));
            Tint(embers, confetti, 0.95f);

            // Ces trois-là sont de la lumière, pas du grain : ils restent sur la teinte de rareté.
            Tint(shockwave, Color.Lerp(light, Color.white, 0.45f), 0.85f);
            Tint(beam, light, 0.55f);
            Tint(core, Color.Lerp(light, Color.white, 0.85f), 0.7f);

            keyLight.color = light;
        }

        static void Tint(ParticleSystem ps, Color color, float alpha)
        {
            var main = ps.main;
            main.startColor = new Color(color.r, color.g, color.b, alpha);
        }

        /// <summary>Repos : seules les poussières flottent.</summary>
        public void Calm()
        {
            SetRate(charge, 0f);
            SetRate(embers, 0f);
            lightPulse = 0f;

            if (!dust.isPlaying) dust.Play();
            if (!charge.isPlaying) charge.Play();
        }

        /// <summary>Montée en tension : le flux aspiré vers le coffre suit l'intensité du tremblement.</summary>
        public void Charge(float intensity)
        {
            float t = Mathf.Clamp01(intensity);

            SetRate(charge, 70f * t * t);
            lightPulse = Mathf.Max(lightPulse, 0.7f * t * t);
        }

        /// <summary>Éclatement : tout part d'un coup au moment où le couvercle se lève.</summary>
        public void Burst()
        {
            SetRate(charge, 0f);
            SetRate(embers, 26f);

            burst.Play();
            shockwave.Play();
            beam.Play();
            core.Play();

            lightPulse = 9f;
        }

        static void SetRate(ParticleSystem ps, float rate)
        {
            var emission = ps.emission;
            emission.rateOverTime = rate;
        }

        void Update()
        {
            // La lumière retombe toute seule : un éclat qui persiste ressemble à un bug d'affichage.
            lightPulse = Mathf.Lerp(lightPulse, 0f, Time.deltaTime * 3.4f);
            keyLight.intensity = lightPulse;
        }
    }
}
