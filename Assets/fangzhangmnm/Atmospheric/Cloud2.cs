using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace fzmnm
{
    [ExecuteInEditMode, ImageEffectAllowedInSceneView]
    public class Cloud2 : MonoBehaviour
    {
        public Shader cloudShader,fullScreenShader; private Material mat1,mat2; new private Camera camera;
        
        public AtmosphereScatteringSetting atmosphereScatteringSetting=new AtmosphereScatteringSetting();

        [Header("Scene References")]
        public bool overridePlanetCenter = true;
        public Transform planetRef;
        public Light sceneDirectionalLight;

        [Header("Global Cloud")]
        public WeatherMapType weatherMapType = WeatherMapType.Planar;
        public enum WeatherMapType { Uniform, Planar, Spherical };
        public Texture2D weatherMap;
        public float weatherMapScale = 100000f;
        public Vector2 weatherMapOffset;
        public float cloudSphereMaxHeight = 5000f;
        [Range(0, 1f)] public float cloudType = 0;
        public float cloudLowerHeight = 500f;
        public float cloudDeltaHeight = 1500f;
        [Range(0, 1f)] public float cloudCoverage = .5f;

        [Header("Cloud Shape")]
        public Texture3D cloudNoiseTexture;
        public Texture3D cloudErosionTexture;
        public float cloudNoiseScale = 10000f;
        public float cloudErosionScaleDivisor = 23.3f;
        public Vector3 cloudNoisePositionOffset, cloudErosionPositionOffset;
        public Vector3 cloudNoisePositionOffsetVelocity, cloudErosionOffsetVelocity;
        [Range(0, 1f)] public float cloudErosionStrength = .2f;

        [Header("Cloud Scattering")]
        public float cloudDensity = 0.01f;
        [ColorUsage(false, true)] public Color cloudInscattering = Color.white;
        [ColorUsage(false, true)] public Color cloudAbsorption = Color.white*.5f;
        [ColorUsage(false, true)] public Color cloudEmission = Color.black;
        [Range(-1f, 0)] public float cloudBackwardMiePhase = -.15f;
        [Range(0, 1f)] public float cloudForwardMiePhase = .85f;
        public float cloudBackwardScatteringBoost = 2.16f;
        public float cloudMultiScattering = 5f;

        public float cloudPowderCoeff = 2f;

        [Header("LightMarch")]
        public int atmosphereStepNum = 8;

        public int cloudStepNum = 128;
        public float cloudStepMaxDist = 50000f;
        public float cloudStepQN = 128f;
        public float cloudStepDensityQ = 1f;

        public int cloudLightingStepNum = 6;
        public float cloudLightingStepQ = 1.5f;
        public float cloudLightingStepMaxDist = 5000f;

        public float cloudLightingLQDepth = .5f;
        public int cloudLightingLQStepNum = 2;
        public float cloudLightingLQStepQ = 2f;
        public float cloudLightingLQStepMaxDist = 2000f;

        public float cloudNoiseLodBias = 0;
        public float cloudErosionLodBias = 0;
        public float weatherMapLodBias = 0;

        public float cloudStepQuitDepth = 3f;
        public float cloudNoiseSDFMultiplier = .125f;
        public float cloudNoiseSDFShift = .1f;
        public float cloudHeightSDFMultiplier = .2f;

        public enum CloudDebugDisplay { Normal,ShowSteps,ShowSamples};
        public CloudDebugDisplay CLOUD_DEBUG_DISPLAY = CloudDebugDisplay.Normal;

        [Header("Downsampling")]
        public bool renderToTwoEyes = false;
        public float renderTextureScale = .25f;


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

            Debug.Log($"[{source}]: {source.vrUsage} {source.width}x{source.height}\n[{destination}]: {(destination? destination.width:0)}x{(destination ? destination.height : 0)}");

            RenderTextureDescriptor desc;
            if (XRSettings.enabled && renderToTwoEyes)
            {
                desc = XRSettings.eyeTextureDesc;
                desc.width = Mathf.FloorToInt(desc.width * renderTextureScale);
                desc.height = Mathf.FloorToInt(desc.height * renderTextureScale);
            }
            else
            {
                desc = new RenderTextureDescriptor(Mathf.FloorToInt(source.width * renderTextureScale), Mathf.FloorToInt(source.height * renderTextureScale));
            }

            RenderTexture rt = RenderTexture.GetTemporary(desc);

            UpdateMaterial();
            if (updateSceneLights)
                UpdateSceneLights();
            Graphics.Blit(source, destination, mat1);
            //Graphics.Blit(rt, destination, mat2);

            RenderTexture.ReleaseTemporary(rt);

        }
        private void OnValidate()
        {
            if (sceneDirectionalLight == null) sceneDirectionalLight = FindObjectOfType<Light>(); 
            UpdateMaterial();
            if (updateSceneLights)
                UpdateSceneLights();
            Debug.Assert(cloudNoiseTexture.filterMode == FilterMode.Trilinear);
            if(weatherMapType==WeatherMapType.Planar)
                Debug.Assert(weatherMap.wrapMode == TextureWrapMode.Clamp);
            Debug.Assert(QualitySettings.activeColorSpace == ColorSpace.Linear);
        }
        private void Reset()
        {
            OnValidate();
        }
        Vector3 getMieCoeff(float g, float boost = 1)
        {
            float g2 = g * g;
            return new Vector3(boost*3f / (8f * Mathf.PI) * (1 - g2) / (2 + g2), 1 + g2, 2 * g);
        }
        void UpdateMaterial()
        {
            if (!mat1 || mat1.shader != cloudShader && cloudShader) { mat1 = new Material(cloudShader); }
            if (!mat2 || mat2.shader != fullScreenShader && fullScreenShader) { mat2 = new Material(fullScreenShader); }

            mat1.DisableKeyword("WEATHERMAP_UNIFORM");
            mat1.DisableKeyword("WEATHERMAP_PLANAR");
            mat1.DisableKeyword("WEATHERMAP_SPHERICAL");
            switch (weatherMapType)
            {
                case WeatherMapType.Uniform:
                    mat1.EnableKeyword("WEATHERMAP_UNIFORM");
                    break;
                case WeatherMapType.Planar:
                    mat1.EnableKeyword("WEATHERMAP_PLANAR");
                    break;
                case WeatherMapType.Spherical:
                    mat1.EnableKeyword("WEATHERMAP_SPHERICAL");
                    break;
            }
            mat1.DisableKeyword("CLOUD_DEBUG_SHOW_STEPS");
            mat1.DisableKeyword("CLOUD_DEBUG_SHOW_SAMPLES");
            switch (CLOUD_DEBUG_DISPLAY)
            {
                case CloudDebugDisplay.Normal:
                    break;
                case CloudDebugDisplay.ShowSteps:
                    mat1.EnableKeyword("CLOUD_DEBUG_SHOW_STEPS");
                    break;
                case CloudDebugDisplay.ShowSamples:
                    mat1.EnableKeyword("CLOUD_DEBUG_SHOW_SAMPLES");
                    break;
            }

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

            atmosphereScatteringSetting.SetMaterial(mat1);

            float scale = atmosphereScatteringSetting.scaleForNumericalStability;

            //Weather Map
            mat1.SetTexture("WeatherMap", weatherMap);
            mat1.SetFloat("weatherMapScale", weatherMapScale / scale);
            mat1.SetVector("weatherMapOffset", (weatherMapOffset + Vector2.one * weatherMapScale/2) / scale);
            mat1.SetFloat("cloudMaxHeight", (cloudSphereMaxHeight) / scale);
            mat1.SetFloat("cloudType", cloudType);
            mat1.SetFloat("cloudLowerHeight", cloudLowerHeight / scale);
            mat1.SetFloat("cloudDeltaHeight", cloudDeltaHeight / scale);
            mat1.SetFloat("cloudCoverage", cloudCoverage);

            //Cloud Shape
            mat1.SetTexture("CloudNoiseTexture", cloudNoiseTexture);
            mat1.SetTexture("CloudErosionTexture", cloudErosionTexture);
            mat1.SetFloat("cloudNoiseScale", cloudNoiseScale / scale);
            mat1.SetFloat("cloudErosionScale", cloudNoiseScale / cloudErosionScaleDivisor / scale);
#if UNITY_EDITOR
            float elapsed = (float)EditorApplication.timeSinceStartup;
#else
            float elapsed = Time.time;
#endif
            mat1.SetVector("cloudNoisePositionOffset", (cloudNoisePositionOffset + elapsed * cloudNoisePositionOffsetVelocity) / scale);
            mat1.SetVector("cloudErosionPositionOffset", (cloudErosionPositionOffset + elapsed * cloudErosionOffsetVelocity) / scale);
            mat1.SetFloat("cloudErosionStrength", cloudErosionStrength);

            //Cloud Scattering
            mat1.SetVector("cloudAbsorption", RGB2SpectralColor( cloudAbsorption) * cloudDensity * scale);
            mat1.SetVector("cloudInscattering", RGB2SpectralColor(cloudInscattering) * cloudDensity * scale);
            mat1.SetVector("cloudEmission", RGB2SpectralColor(cloudEmission) * cloudDensity * scale);
            mat1.SetVector("cloudBackwardMieCoeff", getMieCoeff(cloudBackwardMiePhase,cloudBackwardScatteringBoost));
            mat1.SetVector("cloudForwardMieCoeff", getMieCoeff(cloudForwardMiePhase));
            mat1.SetFloat("cloudPowderCoeff", cloudPowderCoeff);
            mat1.SetFloat("cloudMultiScattering", cloudMultiScattering);

            //LightMarch Parameters
            mat1.SetInt("atmosphereStepNum", atmosphereStepNum);

            mat1.SetInt("cloudStepNum", cloudStepNum);
            mat1.SetFloat("cloudStepMaxDist", cloudStepMaxDist /scale);
            mat1.SetFloat("cloudStepQ", Mathf.Pow(cloudStepQN,1.0f/cloudStepNum));
            mat1.SetFloat("cloudStepDensityQ", cloudStepDensityQ);

            mat1.SetInt("cloudLightingStepNum", cloudLightingStepNum);
            mat1.SetFloat("cloudLightingStepMaxDist", cloudLightingStepMaxDist / scale);
            mat1.SetFloat("cloudLightingStepQ", cloudLightingStepQ);

            mat1.SetFloat("cloudLightingLQDepth", cloudLightingLQDepth);
            mat1.SetInt("cloudLightingLQStepNum", cloudLightingLQStepNum);
            mat1.SetFloat("cloudLightingLQStepMaxDist", cloudLightingLQStepMaxDist / scale);
            mat1.SetFloat("cloudLightingLQStepQ", cloudLightingLQStepQ);

            mat1.SetFloat("cloudNoiseLodShift", Mathf.Log(cloudNoiseTexture.width / (cloudNoiseScale / scale), 2) + cloudNoiseLodBias);
            mat1.SetFloat("cloudErosionLodShift", Mathf.Log(cloudErosionTexture.width / (cloudNoiseScale / cloudErosionScaleDivisor / scale), 2) + cloudErosionLodBias);
            float weatherMapScale1 = weatherMapType == WeatherMapType.Spherical ? 2 * Mathf.PI * atmosphereScatteringSetting.planetRadius : weatherMapScale;
            mat1.SetFloat("weatherMapLodShift", Mathf.Log(weatherMap.width / (weatherMapScale1 / scale), 2) + weatherMapLodBias);

            mat1.SetFloat("cloudStepQuitDepth", cloudStepQuitDepth);
            mat1.SetFloat("cloudNoiseSDFMultiplier", cloudNoiseSDFMultiplier);
            mat1.SetFloat("cloudNoiseSDFShift", cloudNoiseSDFShift);
            mat1.SetFloat("cloudHeightSDFMultiplier", cloudHeightSDFMultiplier);
        }

        void UpdateSceneLights()
        {
            Debug.Assert(mat1);
            if (!updateSceneLights) return;
            if (emulator == null) emulator = new AtmosphereShaderEmulator();
            emulator.ReadMaterial(mat1);

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
        public Color Temperature2RGB(float temperature) => atmosphereScatteringSetting.Temperature2RGB(temperature);




        // C# emulation for shaders for light calculation



    }

}
