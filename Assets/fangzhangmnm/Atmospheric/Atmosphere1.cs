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
        [Range(500, 15000)] public float lightTemperature = 5778f;//in space
        public float lightIntensity = 1.2f;
        public bool overrideLightColorFromTemperature = true;
        [ColorUsage(true, true)] public Color lightColor;
        public float sunDiscG = .99f;
        [Range(1, 10)] public float sunDiscConvergence = 5f;

        [Header("Atmosphere")]
        public AtmosphereScatteringSetting atmosphereScatteringSetting=new AtmosphereScatteringSetting();
        public float SkyLightMultiplier = 4f;

        [Header("LightMarch")]
        public float fixedStepLength = 1000f;
        public int fixedStepNum = 5;
        public int totalStepNum = 10;


        [Header("Update Scene Lights")]
        public bool updateSceneLights = false;
        public float sceneLightMultiplier = 1f;
        public float sceneLightDirectionalLightColorMultiplier = 1f;
        public float sceneLightAmbientSkyColorMultiplier = 1f;
        public float sceneLightAmbientEquatorColorMultiplier = 1f;
        public float sceneLightAmbientEquatorRaySlope = .1f;

        [Multiline(10)] public string debug_text;
        AtmosphereShaderEmulator emulator;

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
            atmosphereScatteringSetting.SetMaterial(mat,planetRadius:planetRadius,scale:scale);

            //LightMarch Parameters
            mat.SetFloat("fixedStepLength", fixedStepLength / scale);
            mat.SetInt("fixedStepNum", fixedStepNum);
            mat.SetInt("totalStepNum", totalStepNum);


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
            if (overrideLightColorFromTemperature) lightColor = Temperature2RGB(lightTemperature) * lightIntensity;

            fixedStepNum = Mathf.Min(fixedStepNum, totalStepNum);

            if (autoScaleForNumericalStability)
                scaleForNumericalStability = Mathf.Sqrt(2*planetRadius*atmosphereMaxHeight+atmosphereMaxHeight*atmosphereMaxHeight);
        }

        void UpdateSceneLights()
        {
            Debug.Assert(mat);
            if (!updateSceneLights) return;
            if (emulator == null) emulator = new AtmosphereShaderEmulator();
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

        public Vector3 RGB2SpectralColor(Color color) => atmosphereScatteringSetting.RGB2SpectralColor(color);
        public Color SpectralColor2RGB(Vector3 color) => atmosphereScatteringSetting.SpectralColor2RGB(color);
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




    }

}
