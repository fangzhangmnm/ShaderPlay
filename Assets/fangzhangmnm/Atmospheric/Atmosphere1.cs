using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace fzmnm
{
    [ExecuteInEditMode, ImageEffectAllowedInSceneView]
    public class Atmosphere1 : MonoBehaviour
    {
        public Shader shader; private Material mat; new private Camera camera;


        public AtmosphereScatteringSetting atmosphereScatteringSetting=new AtmosphereScatteringSetting();


        [Header("Scene References")]
        public bool overridePlanetCenter = true;
        public Transform planetRef;
        public Light sceneDirectionalLight;

        [Header("Update Scene Lights")]
        public bool updateSceneLights = false;
        public float sceneLightMultiplier = 1f;
        public float sceneLightDirectionalLightColorMultiplier = 1f;
        public float sceneLightAmbientSkyColorMultiplier = 1f;
        public float sceneLightAmbientEquatorColorMultiplier = 1f;
        public float sceneLightAmbientEquatorRaySlope = .1f;

        [Header("LightMarch")]
        public int atmosphereStepNum = 10;

        [Multiline(10)] public string debug_text;
        AtmosphereShaderEmulator emulator;

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            UpdateMaterial();
            if (updateSceneLights)
                UpdateSceneLights();

            Graphics.Blit(source, destination, mat);

        }
        private void OnValidate()
        {
            if (sceneDirectionalLight == null) sceneDirectionalLight = FindObjectOfType<Light>();
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
            if (!mat || mat.shader != shader && shader) { mat = new Material(shader); }
            camera = GetComponent<Camera>();
            camera.depthTextureMode = DepthTextureMode.Depth;

            if (overridePlanetCenter)
            {
                if (planetRef == null)
                {
                    atmosphereScatteringSetting.planetCenter = new Vector3(0, -atmosphereScatteringSetting.planetRadius, 0);
                    atmosphereScatteringSetting.planetRotation = Quaternion.identity;
                }
                else
                {
                    atmosphereScatteringSetting.planetCenter = planetRef.position;
                    atmosphereScatteringSetting.planetRotation = planetRef.rotation;
                }
            }
            atmosphereScatteringSetting.dirToSun = -sceneDirectionalLight.transform.forward;

            atmosphereScatteringSetting.SetMaterial(mat);

            mat.SetInt("atmosphereStepNum", atmosphereStepNum);
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
                emulator.frag(RGB2SpectralColor(atmosphereScatteringSetting.lightColor), camera.transform.position, -sceneDirectionalLight.transform.forward)
                );
            sceneDirectionalLight.intensity = l1c.maxColorComponent;
            if (sceneDirectionalLight.intensity > 0) sceneDirectionalLight.color = l1c / sceneDirectionalLight.intensity;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

            emulator.includeMieInscattering = false;
            RenderSettings.ambientSkyColor = sceneLightAmbientSkyColorMultiplier * sceneLightMultiplier * SpectralColor2RGB(
                emulator.frag(Vector3.zero, camera.transform.position, Vector3.up)
                );


            emulator.includeMieInscattering = true;
            Vector3 equatorDir = Vector3.Normalize(Vector3.ProjectOnPlane(sceneDirectionalLight.transform.forward, Vector3.up) + sceneLightAmbientEquatorRaySlope * Vector3.up);
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
