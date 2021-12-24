using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace fzmnm
{
    [ExecuteInEditMode, ImageEffectAllowedInSceneView]
    public class Atmosphere1 : MonoBehaviour
    {
        public Shader shader; private Material mat; new private Camera camera;

        [Header("Planet Geometry")]
        public Transform planetRef;
        public bool overridePlanetCenter = true;
        public Vector3 planetCenter;
        public Quaternion planetRotation = Quaternion.identity;
        public float planetRadius = 6371000f;
        public float atmosphereMaxHeight = 60000f;
        public bool autoScaleForNumericalStability = true;
        public float scaleForNumericalStability;
        public float sceneScaleMultiplier = 1f;

        [Header("Sun")]
        public new Light light;
        [Range(500, 15000)] public float lightTemperature = 6500f;//in space
        public float lightIntensity = 1.2f;
        public bool overrideLightColorFromTemperature = true;
        [ColorUsage(true, true)] public Color lightColor;
        public float sunDiscG = .99f;
        [Range(1, 10)] public float sunDiscConvergence = 5f;

        [Header("Atmosphere")]
        public float atmosphereDensity = 1.224f;
        public float atmosphereScatteringMultiplier = 1e-5f / 1.224f;

        [ColorUsage(false, true)] public Color atmosphereRayleighScattering = new Color(.5802f, 1.3558f, 3.31f);
        [ColorUsage(false, true)] public Color atmosphereRayleighAbsorption = new Color(.5802f, 1.3558f, 3.31f);
        public Vector3 atmosphereRayleighPhaseCoeff = new Vector3(1.12f, .4f, 0f) / (4 * Mathf.PI);
        public float atmosphereRayleighScaleHeight = 8000f;

        public float atmosphereMieMultiplier = 1f;
        [ColorUsage(false, true)] public Color atmosphereMieScattering = new Color(.3996f, .3996f, .3996f);
        [ColorUsage(false, true)] public Color atmosphereMieAbsorption = new Color(.440f, .440f, .440f);
        [Range(-0.99f, 0.99f)] public float atmosphereMieG = 0.8f;
        public float atmosphereMieScaleHeight = 1200f;

        [ColorUsage(false, true)] public Color atmosphereOzoneAbsorption = new Color(.0650f, .1881f, .0085f);
        public float atmosphereOzoneMinHeight = 10000f;
        public float atmosphereOzoneMaxHeight = 40000f;

        [Header("LightMarch")]
        public float fixedStepLength = 1000f;
        public int fixedStepNum = 5;
        public int totalStepNum = 10;

        [Header("PostProcessing")]
        public float SkyLightMultiplier = 4f;
        public bool useToneMapping = true;
        [Range(-10, 10)] public float toneMappingLogExposure = 0;
        //http://www.thetenthplanet.de/archives/4519
        public bool useColorSpace = true;
        public Matrix4x4 spectralColor2RGBMatrix = new Matrix4x4(
            new Vector4(1.6218f, -0.0374f, -0.0283f, 0),
            new Vector4(-0.4493f, 1.0598f, -0.1119f, 0),
            new Vector4(0.0325f, -0.0742f, 1.0491f, 0),
            new Vector4(0, 0, 0, 1));
        public Matrix4x4 RGB2spectralColorMatrix;

        [Header("Update Scene Lights")]
        public bool updateSceneLights = false;
        public float sceneLightMultiplier = 1f;
        public float sceneLightDirectionalLightColorMultiplier = 1f;
        public float sceneLightAmbientSkyColorMultiplier = 1f;
        public float sceneLightAmbientEquatorColorMultiplier = 1f;
        public float sceneLightAmbientEquatorRaySlope = .1f;

        [Multiline(10)] public string debug_text;
        AtmosphericShaderEmulator emulator;

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            UpdateParameters();
            UpdateMaterial();
            if (updateSceneLights)
                UpdateSceneLights();

            Graphics.Blit(source, destination, mat);

        }
        private void OnValidate()
        {
            if (light == null) light = FindObjectOfType<Light>();
            UpdateParameters();
            UpdateMaterial();
            if (updateSceneLights)
                UpdateSceneLights();
            Debug.Assert(QualitySettings.activeColorSpace == ColorSpace.Linear);
        }
        private void Reset()
        {
            OnValidate();
        }

        void UpdateMaterial()
        {

            float scale = scaleForNumericalStability;

            //Planet Geometry
            mat.SetMatrix("worldToPlanetTRS", Matrix4x4.Inverse(Matrix4x4.TRS(planetCenter / sceneScaleMultiplier, planetRotation, Vector3.one * scale / sceneScaleMultiplier)));
            mat.SetFloat("depthToPlanetS", sceneScaleMultiplier / scale);
            mat.SetFloat("planetRadius", (planetRadius) / scale);
            mat.SetFloat("atmosphereRadius", (atmosphereMaxHeight + planetRadius) / scale);

            //Sun Light
            mat.SetVector("dirToSun", -light.transform.forward);
            mat.SetVector("sunColor", RGB2SpectralColor(lightColor * Mathf.PI * SkyLightMultiplier)); //should be pi instead of 4 pi. Why?
            float g2 = sunDiscG * sunDiscG;
            mat.SetVector("sunDiscCoeff", new Vector4(3f / (8f * Mathf.PI) * (1 - g2) / (2 + g2), 1 + g2, 2 * sunDiscG, sunDiscConvergence));


            //Atmosphere
            mat.SetVector("atmosphere_rayleighScattering", scale * atmosphereDensity * atmosphereScatteringMultiplier * RGB2SpectralColor(atmosphereRayleighScattering));
            mat.SetVector("atmosphere_rayleighAbsorption", scale * atmosphereDensity * atmosphereScatteringMultiplier * RGB2SpectralColor(atmosphereRayleighAbsorption));
            mat.SetVector("atmosphere_rayleighPhaseCoeff", atmosphereRayleighPhaseCoeff);
            mat.SetFloat("atmosphere_rayleighScaleHeight", atmosphereRayleighScaleHeight / scale);

            mat.SetVector("atmosphere_mieScattering", scale * atmosphereDensity * atmosphereScatteringMultiplier * atmosphereMieMultiplier * RGB2SpectralColor(atmosphereMieScattering));
            mat.SetVector("atmosphere_mieAbsorption", scale * atmosphereDensity * atmosphereScatteringMultiplier * atmosphereMieMultiplier * RGB2SpectralColor(atmosphereMieAbsorption));
            g2 = atmosphereMieG * atmosphereMieG;
            mat.SetVector("atmosphere_miePhaseCoeff", new Vector3(3f / (8f * Mathf.PI) * (1 - g2) / (2 + g2), 1 + g2, 2 * atmosphereMieG));
            mat.SetFloat("atmosphere_mieScaleHeight", atmosphereMieScaleHeight / scale);

            mat.SetVector("atmosphere_ozoneAbsorption", scale * atmosphereDensity * atmosphereScatteringMultiplier * RGB2SpectralColor(atmosphereOzoneAbsorption));
            mat.SetFloat("atmosphere_ozoneMinRadius", (atmosphereOzoneMinHeight + planetRadius) / scale);
            mat.SetFloat("atmosphere_ozoneMaxRadius", (atmosphereOzoneMaxHeight + planetRadius) / scale);

            //LightMarch Parameters
            mat.SetFloat("fixedStepLength", fixedStepLength / scale);
            mat.SetInt("fixedStepNum", fixedStepNum);
            mat.SetInt("totalStepNum", totalStepNum);

            //PostProcessing
            mat.SetFloat("toneMappingExposure", useToneMapping ? Mathf.Exp(toneMappingLogExposure) : 0);
            mat.SetMatrix("spectralColor2RGB", useColorSpace ? spectralColor2RGBMatrix : Matrix4x4.identity);
            mat.SetMatrix("RGB2spectralColor", useColorSpace ? RGB2spectralColorMatrix : Matrix4x4.identity);

        }

        private void UpdateParameters()
        {
            if (!mat || mat.shader != shader && shader) { mat = new Material(shader); }
            camera = GetComponent<Camera>();
            camera.depthTextureMode = DepthTextureMode.Depth;

            if (overridePlanetCenter)
            {
                if (planetRef == null)
                    planetCenter = new Vector3(0, -planetRadius, 0);
                else
                    planetCenter = planetRef.position;
            }
            RGB2spectralColorMatrix = Matrix4x4.Inverse(spectralColor2RGBMatrix);
            if (overrideLightColorFromTemperature) lightColor = Temperature2RGB(lightTemperature) * lightIntensity;

            fixedStepNum = Mathf.Min(fixedStepNum, totalStepNum);

            if (autoScaleForNumericalStability)
                scaleForNumericalStability = Mathf.Sqrt(2*planetRadius*atmosphereMaxHeight+atmosphereMaxHeight*atmosphereMaxHeight);
        }

        void UpdateSceneLights()
        {
            Debug.Assert(mat);
            if (!updateSceneLights) return;
            if (emulator == null) emulator = new AtmosphericShaderEmulator();
            emulator.ReadMaterial(mat);

            emulator.includeMieInscattering = false;
            //do not multiply lightColor by pi because input is in unit of color
            Color l1c = sceneLightDirectionalLightColorMultiplier * sceneLightMultiplier * SpectralColor2RGB(
                emulator.frag(RGB2SpectralColor(lightColor), camera.transform.position, -light.transform.forward)
                );
            light.intensity = l1c.maxColorComponent;
            if (light.intensity > 0) light.color = l1c / light.intensity;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

            emulator.includeMieInscattering = false;
            RenderSettings.ambientSkyColor = sceneLightAmbientSkyColorMultiplier * sceneLightMultiplier * SpectralColor2RGB(
                emulator.frag(Vector3.zero, camera.transform.position, Vector3.up)
                );


            emulator.includeMieInscattering = true;
            Vector3 equatorDir = Vector3.Normalize(Vector3.ProjectOnPlane(light.transform.forward, Vector3.up) + sceneLightAmbientEquatorRaySlope * Vector3.up);
            RenderSettings.ambientEquatorColor = sceneLightAmbientEquatorColorMultiplier * sceneLightMultiplier * SpectralColor2RGB(
                emulator.frag(Vector3.zero, camera.transform.position, equatorDir)
                );

            RenderSettings.ambientGroundColor = .4f * RenderSettings.ambientEquatorColor + .2f * RenderSettings.ambientSkyColor;

        }


        Vector3 exp(Vector3 v) => new Vector3(Mathf.Exp(v.x), Mathf.Exp(v.y), Mathf.Exp(v.z));
        Vector3 ToneMapping(Vector3 color) => useToneMapping ? Vector3.one - exp(-color * toneMappingLogExposure) : color;
        Vector3 RGB2SpectralColor(Color color)
        {
            Vector3 v = new Vector3(color.r, color.g, color.b);
            if (useColorSpace)
                v = RGB2spectralColorMatrix.MultiplyVector(v);
            return v;
        }
        Color SpectralColor2RGB(Vector3 color)
        {
            if (useColorSpace)
                color = spectralColor2RGBMatrix.MultiplyVector(color);
            return new Color(Mathf.Max(0, color.x), Mathf.Max(0, color.y), Mathf.Max(0, color.z), 1);
        }
        Color Temperature2RGB(float temperature)
        {
            //https://tannerhelland.com/2012/09/18/convert-temperature-rgb-algorithm-code.html
            float t = temperature / 100;
            if (t <= 19)
                return new Color(
                    1,
                    Mathf.Clamp01((99.4708025861f * Mathf.Log(t) - 161.1195681661f) / 255f),
                    0,
                    1);
            else if (t <= 66)
                return new Color(
                    1,
                    Mathf.Clamp01((99.4708025861f * Mathf.Log(t) - 161.1195681661f) / 255f),
                    Mathf.Clamp01((138.5177312231f * Mathf.Log(t - 10) - 305.0447927307f) / 255f),
                    1);
            else
                return new Color(
                    Mathf.Clamp01(329.698727446f * Mathf.Pow((t - 60), -0.1332047592f) / 255f),
                    Mathf.Clamp01(288.1221695283f * Mathf.Pow((t - 60), -0.0755148492f) / 255f),
                    1,
                    1);
        }



        // C# emulation for shaders for light calculation



    }

}
