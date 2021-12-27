using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace fzmnm
{
    [ExecuteInEditMode, ImageEffectAllowedInSceneView]
    public class Cloud2 : MonoBehaviour
    {
        public Shader shader; private Material mat; new private Camera camera;
        
        [Header("Planet Geometry")]
        public Transform planetRef;
        public bool overridePlanetCenter = true;
        public Vector3 planetCenter;
        public Quaternion planetRotation = Quaternion.identity;
        public float planetRadius = 6371000f;
        public float atmosphereMaxHeight = 60000f;
        public float cloudSphereMaxHeight = 5000f;
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

        [Header("Weather")]
        public WeatherMapType weatherMapType = WeatherMapType.Planar;
        public enum WeatherMapType { Uniform, Planar, Spherical };
        public Texture2D weatherMap;
        public float weatherMapScale = 100000f;
        public Vector2 weatherMapOffset;
        [Range(0, 1f)] public float cloudType = 0;
        public float cloudLowerHeight = 500f;
        public float cloudDeltaHeight = 1500f;
        [Range(0, 1f)] public float cloudCoverage = .5f;

        [Header("Cloud Shape")]
        public Texture3D cloudNoiseTexture;
        public Texture3D cloudErosionTexture;
        public float cloudNoiseScale = 10000f;
        public float cloudErosionScaleDivisor = 5.7f;
        public Vector3 cloudNoisePositionOffset, cloudErosionPositionOffset;
        public Vector3 cloudNoisePositionOffsetVelocity, cloudErosionOffsetVelocity;
        [Range(0, 1f)] public float cloudErosionStrength = .3f;

        [Header("Cloud Scattering")]
        public float cloudDensity = 0.01f;
        [ColorUsage(false, true)] public Color cloudInscattering = Color.white;
        [ColorUsage(false, true)] public Color cloudAbsorption = Color.white;
        [Range(-1f, 1f)] public float cloudMiePhase = .26f;

        [Header("LightMarch")]
        public int atmosphereStepNum = 8;
        public int cloudStepNum = 128;
        public int cloudLightingStepNum = 6;
        public float cloudLightingStepQ = 1.5f;
        public float cloudNoiseLodBias = 0;
        public float cloudErosionLodBias = 0;


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
            Debug.Assert(cloudNoiseTexture.filterMode == FilterMode.Trilinear);
            if(weatherMapType==WeatherMapType.Planar)
                Debug.Assert(weatherMap.wrapMode == TextureWrapMode.Clamp);
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
            mat.SetFloat("cloudMaxHeight", (cloudSphereMaxHeight) / scale);

            //Sun
            mat.SetVector("dirToSun", Quaternion.Inverse(planetRotation )*- light.transform.forward);
            mat.SetVector("sunColor", RGB2SpectralColor(lightColor * Mathf.PI * SkyLightMultiplier)); //should be pi instead of 4 pi. Why?
            float g2 = sunDiscG * sunDiscG;
            mat.SetVector("sunDiscCoeff", new Vector4(3f / (8f * Mathf.PI) * (1 - g2) / (2 + g2), 1 + g2, 2 * sunDiscG, sunDiscConvergence));

            //Atmosphere
            atmosphereScatteringSetting.SetMaterial(mat, planetRadius: planetRadius, scale: scale);

            //Weather Map
            mat.SetTexture("WeatherMap", weatherMap);
            mat.SetFloat("weatherMapScale", weatherMapScale / scale);
            mat.SetVector("weatherMapOffset", (weatherMapOffset + Vector2.one * weatherMapScale/2) / scale);
            mat.SetFloat("cloudType", cloudType);
            mat.SetFloat("cloudLowerHeight", cloudLowerHeight / scale);
            mat.SetFloat("cloudDeltaHeight", cloudDeltaHeight / scale);
            mat.SetFloat("cloudCoverage", cloudCoverage);

            //Cloud Shape
            mat.SetTexture("CloudNoiseTexture", cloudNoiseTexture);
            mat.SetTexture("CloudErosionTexture", cloudErosionTexture);
            mat.SetFloat("cloudNoiseScale", cloudNoiseScale / scale);
            mat.SetFloat("cloudErosionScale", cloudNoiseScale / cloudErosionScaleDivisor / scale);
#if UNITY_EDITOR
            float elapsed = (float)EditorApplication.timeSinceStartup;
#else
            float elapsed = Time.time;
#endif
            mat.SetVector("cloudNoisePositionOffset", (cloudNoisePositionOffset + elapsed * cloudNoisePositionOffsetVelocity) / scale);
            mat.SetVector("cloudErosionPositionOffset", (cloudErosionPositionOffset + elapsed * cloudErosionOffsetVelocity) / scale);
            

            //Cloud Scattering
            mat.SetColor("cloudAbsorption", cloudAbsorption * cloudDensity * scale);
            mat.SetColor("cloudInscattering", cloudInscattering * cloudDensity * scale);
            g2 = cloudMiePhase * cloudMiePhase;
            mat.SetVector("cloudMieCoeff", new Vector3(3f / (8f * Mathf.PI) * (1 - g2) / (2 + g2), 1 + g2, 2 * cloudMiePhase));

            //LightMarch Parameters
            mat.SetInt("atmosphereStepNum", atmosphereStepNum);
            mat.SetInt("cloudStepNum", cloudStepNum);
            mat.SetInt("cloudLightingStepNum", cloudLightingStepNum);
            mat.SetFloat("cloudLightingStepQ", cloudLightingStepQ);
            mat.SetFloat("cloudNoiseLodShift", Mathf.Log(cloudNoiseTexture.width / (cloudNoiseScale / scale), 2) + cloudNoiseLodBias);
            mat.SetFloat("cloudErosionLodShift", Mathf.Log(cloudErosionTexture.width / (cloudNoiseScale / cloudErosionScaleDivisor / scale), 2) + cloudErosionLodBias);
            mat.SetFloat("cloudErosionStrength", cloudErosionStrength);
        }

        private void UpdateParameters()
        {

            if (!mat || mat.shader != shader && shader) { mat = new Material(shader); }

            switch (weatherMapType)
            {
                case WeatherMapType.Uniform:
                    mat.EnableKeyword("WEATHERMAP_UNIFORM");
                    mat.DisableKeyword("WEATHERMAP_PLANAR");
                    mat.DisableKeyword("WEATHERMAP_SPHERICAL");
                    break;
                case WeatherMapType.Planar:
                    mat.DisableKeyword("WEATHERMAP_UNIFORM");
                    mat.EnableKeyword("WEATHERMAP_PLANAR");
                    mat.DisableKeyword("WEATHERMAP_SPHERICAL");
                    break;
                case WeatherMapType.Spherical:
                    mat.DisableKeyword("WEATHERMAP_UNIFORM");
                    mat.DisableKeyword("WEATHERMAP_PLANAR");
                    mat.EnableKeyword("WEATHERMAP_SPHERICAL");
                    break;
            }
            camera = GetComponent<Camera>();
            camera.depthTextureMode = DepthTextureMode.Depth;

            if (overridePlanetCenter)
            {
                if (planetRef == null)
                {
                    planetCenter = new Vector3(0, -planetRadius, 0);
                    planetRotation = Quaternion.identity;
                }
                else
                {
                    planetCenter = planetRef.position;
                    planetRotation = planetRef.rotation;
                }
            }
            if (overrideLightColorFromTemperature) lightColor = Temperature2RGB(lightTemperature) * lightIntensity;


            if (autoScaleForNumericalStability)
                scaleForNumericalStability = Mathf.Sqrt(2 * planetRadius * atmosphereMaxHeight + atmosphereMaxHeight * atmosphereMaxHeight);
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
        public Color Temperature2RGB(float temperature) => atmosphereScatteringSetting.Temperature2RGB(temperature);




        // C# emulation for shaders for light calculation



    }

}
