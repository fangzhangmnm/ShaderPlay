using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class Atmosphere1 : MonoBehaviour
{
    public Shader shader; public Material mat;new public Camera camera;

    [Header("Planet Geometry")]
    public Transform planetRef;
    public bool overridePlanetCenter = true;
    public Vector3 planetCenter;
    public Quaternion planetRotation = Quaternion.identity;
    public float planetRadius=6371000;
    public float atmosphereMaxHeight=27000;
    public float scaleForNumericalStability = 100000;
    public float sceneScaleMultiplier = 1;

    [Header("Sun Light")]
    public new Light light;
    [Range(500, 15000)] public float lightTemperature = 6500f;//in space
    public float lightIntensity = 1.5f;
    public bool overrideLightColorFromTemperature=true;
    [ColorUsage(true, true)] public Color lightColor;// = new Color(1.0997f, 0.9771f, 0.9329f,0)*1.5f+new Color(0,0,0,1);//http://www.thetenthplanet.de/archives/4519
    public float lightColorLDRBoost = 1;

    [Header("Atmosphere")]
    public float atmosphereDensityFalloffHeight = 9000; //https://www.engineeringtoolbox.com/standard-atmosphere-d_604.html

    public float atmosphereDensity = 1.224f; 
    public float atmosphereScatteringMultiplier = 2.4845e-5f / 1.224f; //https://advances.realtimerendering.com/s2021/jpatry_advances2021/index.html
    public bool overrideAtmosphereAbsorption = true;
    [ColorUsage(true, true)] public Color atmosphereAbsorption = Color.white;
    [ColorUsage(true, true)] public Color atmosphereExtinsion = Color.black;
    [ColorUsage(true, true)] public Color atmosphereEmission = Color.black;
    public Vector3 wavelengths = new Vector3(598, 524, 445);//new Vector3(615, 535, 445);//http://www.thetenthplanet.de/archives/4519
    public bool overrideAtmosphereRayleighInscattering = true;
    [ColorUsage(true, true)] public Color atmosphereRayleighInscattering = Color.white;
    [ColorUsage(true, true)] public Color atmosphereMieInscattering = new Color(.1f, .1f, .1f, 1);
    [Range(-1, 1)] public float atmosphereMiePhase = .95f;

    [Header("LightMarch Parameters")]
    public int numInScatteringPoints = 10;
    public int numOpticalDepthPoints = 10;

    [Header("PostProcessing Parameters")]
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
    public float ambientSkyColorMultiplier = 1f;
    public float ambientEquatorRaySlope = .5f;
    public float ambientEquatorColorMultiplier = 1f;
    public bool adjustLDRExposure = true;

    [Multiline(10)] public string debug_text;
    ShaderEmulator emulator;

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        ValidateParameters();
        UpdateMaterial();

        Graphics.Blit(source, destination, mat);

        if (updateSceneLights && mat)
            UpdateSceneLights();
    }
    private void OnValidate()
    {
        Debug.Assert(QualitySettings.activeColorSpace == ColorSpace.Linear);
        if (light == null)
            light = FindObjectOfType<Light>();
        camera = GetComponent<Camera>();
        camera.depthTextureMode = DepthTextureMode.Depth;
        ValidateParameters();
        UpdateMaterial();
        if (updateSceneLights && mat)
            UpdateSceneLights();
        //debug_text = $"{RGB2SpectralColor(atmosphereRayleighInscattering) * atmosphereDensity *atmosphereScatteringMultiplier * 1e5f}";
    }

    void UpdateMaterial()
    {

        float scale = scaleForNumericalStability;

        //Planet Geometry
        mat.SetMatrix("worldToPlanetTRS",
            Matrix4x4.Inverse(Matrix4x4.TRS(planetCenter / sceneScaleMultiplier, planetRotation, Vector3.one * scale / sceneScaleMultiplier)));
        mat.SetFloat("depthToPlanetS", sceneScaleMultiplier / scale);
        mat.SetFloat("zeroHeightRadius", (planetRadius) / scale);
        mat.SetFloat("atmosphereRadius", (atmosphereMaxHeight + planetRadius) / scale);

        //Sun Light
        mat.SetVector("dirToLight", -light.transform.forward);
        mat.SetVector("lightColor", RGB2SpectralColor(lightColor*lightColorLDRBoost));

        //Atmosphere
        mat.SetFloat("atmosphereDensityScaleHeight", atmosphereDensityFalloffHeight/scale);
        mat.SetVector("atmosphereAbsorption", scale * atmosphereDensity * atmosphereScatteringMultiplier * RGB2SpectralColor(atmosphereAbsorption));
        mat.SetVector("atmosphereEmission", scale * atmosphereDensity * atmosphereScatteringMultiplier * RGB2SpectralColor(atmosphereEmission));
        mat.SetVector("atmosphereRayleighInscattering", scale * atmosphereDensity * atmosphereScatteringMultiplier * RGB2SpectralColor(atmosphereRayleighInscattering));
        mat.SetVector("atmosphereMieInscattering", scale * atmosphereDensity * atmosphereScatteringMultiplier * RGB2SpectralColor(atmosphereMieInscattering));
        float g2 = atmosphereMiePhase * atmosphereMiePhase;
        mat.SetVector("atmosphereMieCoeff", new Vector3(1.5f * (1 - g2) / (2 + g2), 1 + g2, 2 * atmosphereMiePhase));

        //LightMarch Parameters
        mat.SetInt("numInScatteringPoints", numInScatteringPoints);
        mat.SetInt("numOpticalDepthPoints", numOpticalDepthPoints);

        //PostProcessing
        mat.SetFloat("toneMappingExposure", useToneMapping ? Mathf.Exp(toneMappingLogExposure) : 0);
        mat.SetMatrix("spectralColor2RGB", useColorSpace ? spectralColor2RGBMatrix : Matrix4x4.identity);
        mat.SetMatrix("RGB2spectralColor", useColorSpace ? RGB2spectralColorMatrix : Matrix4x4.identity);

    }

    private void ValidateParameters()
    {
        if (!mat || mat.shader != shader && shader) { mat = new Material(shader); }

        if (overridePlanetCenter)
        {
            if (planetRef == null)
                planetCenter = new Vector3(0, -planetRadius, 0);
            else
                planetCenter = planetRef.position;
        }
        RGB2spectralColorMatrix = Matrix4x4.Inverse(spectralColor2RGBMatrix);
        if (overrideAtmosphereRayleighInscattering)
        {
            Vector3 wavelengthAdjust = new Vector3(Mathf.Pow(wavelengths.x, -4), Mathf.Pow(wavelengths.y, -4), Mathf.Pow(wavelengths.z, -4));
            wavelengthAdjust /= wavelengthAdjust.z;
            atmosphereRayleighInscattering = SpectralColor2RGB(wavelengthAdjust);
            //(0.76224e-5,1.2935e-5,2.4845e-5)
        }
        if (overrideAtmosphereAbsorption)
            atmosphereAbsorption = atmosphereRayleighInscattering + atmosphereMieInscattering+atmosphereExtinsion;
        if (overrideLightColorFromTemperature)
            lightColor = Temperature2RGB(lightTemperature) * lightIntensity;

    }

    void UpdateSceneLights()
    {
        if (!updateSceneLights) return;
        if (emulator == null) emulator = new ShaderEmulator();
        emulator.ReadMaterial(mat);
        emulator.includeMieInscattering = false;

        emulator.frag(camera.transform.position, -light.transform.forward, out Vector3 t1, out Vector3 l1);
        Color l1c= SpectralColor2RGB((Vector3.Scale(t1, RGB2SpectralColor(lightColor*lightColorLDRBoost))));//omit mie scattering



        light.intensity = l1c.maxColorComponent;
        l1c.a = l1c.maxColorComponent;
        light.color = l1c / light.intensity;

        //RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        //emulator.frag(camera.transform.position, Vector3.up, out Vector3 t2, out Vector3 l2);
        //RenderSettings.ambientLight =  SpectralColor2RGB(ambientSkyLightMultiplier * l2);

        
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;

        emulator.frag(camera.transform.position, Vector3.up, out Vector3 t2, out Vector3 l2);
        RenderSettings.ambientSkyColor = SpectralColor2RGB((ambientSkyColorMultiplier * l2));


        Vector3 equatorDir = Vector3.Normalize(Vector3.ProjectOnPlane(light.transform.forward, Vector3.up)+ ambientEquatorRaySlope*Vector3.up);
        emulator.frag(camera.transform.position, equatorDir, out Vector3 t3, out Vector3 l3);
        RenderSettings.ambientEquatorColor = SpectralColor2RGB((ambientEquatorColorMultiplier * l3));
        
        RenderSettings.ambientGroundColor = .1f * RenderSettings.ambientEquatorColor + .3f * RenderSettings.ambientSkyColor;

        if (adjustLDRExposure)
        {
            lightColorLDRBoost = Mathf.Clamp( 1.5f* lightColorLDRBoost / light.intensity, .1f,10);
        }

    }

    Color SpectralColor2RGB(Vector3 color)
    {
        if(useColorSpace)
            color = spectralColor2RGBMatrix.MultiplyVector(color);
        return new Color(color.x, color.y, color.z,1);
    }
    Vector3 exp(Vector3 v) => new Vector3(Mathf.Exp(v.x), Mathf.Exp(v.y), Mathf.Exp(v.z));
    Vector3 ToneMapping(Vector3 color)
    {
        if(useToneMapping)
            color= Vector3.one - exp(-color * toneMappingLogExposure);
        return color;
    }
    Vector3 RGB2SpectralColor(Color color)
    {
        Vector3 v = new Vector3(color.r, color.g, color.b);
        if (useColorSpace)
            v = RGB2spectralColorMatrix.MultiplyVector(v);
        return v;
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
        else if (t<=66)
            return new Color(
                1,
                Mathf.Clamp01((99.4708025861f * Mathf.Log(t) - 161.1195681661f) / 255f),
                Mathf.Clamp01((138.5177312231f * Mathf.Log(t-10) - 305.0447927307f) / 255f),
                1);
        else
            return new Color(
                Mathf.Clamp01(329.698727446f * Mathf.Pow((t - 60), -0.1332047592f) / 255f),
                Mathf.Clamp01(288.1221695283f * Mathf.Pow((t - 60), -0.0755148492f) / 255f),
                1,
                1);
    }



    // C# emulation for shaders for light calculation
    class ShaderEmulator
    {
        //Planet Geometry
        Matrix4x4 worldToPlanetTRS;
        float planetRadius;
        float atmosphereRadius;

        //Sun Light
        Vector3 dirToLight;
        Vector3 lightColor;

        //Atmosphere
        float atmosphereDensityScaleHeight;
        Vector3 atmosphereAbsorption;
        Vector3 atmosphereEmission;
        Vector3 atmosphereRayleighInscattering;
        Vector3 atmosphereMieInscattering;
        Vector3 atmosphereMieCoeff;
        public bool includeMieInscattering = true;
        
        //LightMarch Parameters
        int numInScatteringPoints;
        int numOpticalDepthPoints;

        //PostProcessing
        float toneMappingExposure;
        Matrix4x4 spectralColor2RGB;
        Matrix4x4 RGB2spectralColor;

        public void ReadMaterial(Material mat)
        {
            worldToPlanetTRS = mat.GetMatrix("worldToPlanetTRS");
            planetRadius = mat.GetFloat("zeroHeightRadius");
            atmosphereRadius = mat.GetFloat("atmosphereRadius");

            dirToLight = mat.GetVector("dirToLight");
            lightColor = mat.GetVector("lightColor");

            atmosphereDensityScaleHeight = mat.GetFloat("atmosphereDensityScaleHeight");
            atmosphereAbsorption = mat.GetVector("atmosphereAbsorption");
            atmosphereEmission = mat.GetVector("atmosphereEmission");
            atmosphereRayleighInscattering = mat.GetVector("atmosphereRayleighInscattering");
            atmosphereMieInscattering = mat.GetVector("atmosphereMieInscattering");
            atmosphereMieCoeff = mat.GetVector("atmosphereMieCoeff");

            numInScatteringPoints = mat.GetInt("numInScatteringPoints");
            numOpticalDepthPoints = mat.GetInt("numOpticalDepthPoints");

            toneMappingExposure = mat.GetFloat("toneMappingExposure");
            spectralColor2RGB = mat.GetMatrix("spectralColor2RGB");
            RGB2spectralColor = mat.GetMatrix("RGB2spectralColor");
        }


        Vector3 exp(Vector3 v) => new Vector3(Mathf.Exp(v.x), Mathf.Exp(v.y), Mathf.Exp(v.z));
        float miePhase(float cosTh, Vector3 mieCoeff)
        {
            return Mathf.Clamp(mieCoeff.x * Mathf.Pow(mieCoeff.y - mieCoeff.z * cosTh, -1.5f), 0, 100);
        }
        float rayleighPhase(float cosTh)
        {
            return 1.12f + .4f * cosTh;
            //return .75f * (1 + cosTh * cosTh);
        }

        Vector2 raySphere(float sphereRadius, Vector3 rayOrigin, Vector3 rayDir)
        {
            //returns dstToSphere,dstThroughSphere
            //dir is normalzied
            float b = 2 * Vector3.Dot(rayOrigin, rayDir);
            float c = Vector3.Dot(rayOrigin, rayOrigin) - sphereRadius * sphereRadius;
            float d = b * b - 4 * c;
            if (d > 0)
            {
                float s = Mathf.Sqrt(d);
                float dstToSphereNear = Mathf.Max(0, (-b - s) / 2);
                float dstToSphereFar = (-b + s) / 2;
                if (dstToSphereFar >= 0) return new Vector2(dstToSphereNear, dstToSphereFar - dstToSphereNear);
            }
            return new Vector2(0, 0);
        }
        float getDensity(Vector3 pos)
        {
            float height = Mathf.Max(Vector3.Magnitude(pos) - planetRadius,0) ;
            return Mathf.Exp(-height / atmosphereDensityScaleHeight);
        }
        float getOpticalDepth(Vector3 rayOrigin, Vector3 rayDir, float rayLength)
        {
            float step = rayLength / numOpticalDepthPoints;
            Vector3 pos = rayOrigin + rayDir * (step * .5f);
            float opticalDepth = 0;
            for (int i = 0; i < numOpticalDepthPoints; ++i)
            {
                opticalDepth += getDensity(pos) * step;
                pos += rayDir * step;
            }
            return opticalDepth;
        }
        void raymarch(Vector3 rayOrigin, Vector3 rayDir, float rayLength, out Vector3 transmittance, out Vector3 light)
        {
            float dst;
            light = Vector3.zero;
            transmittance = new Vector3(1, 1, 1);

            float cosTh = Vector3.Dot(rayDir, dirToLight);
            Vector3 atmosphereInscatteringLight = Vector3.Scale(lightColor ,atmosphereRayleighInscattering * rayleighPhase(cosTh));
            if(includeMieInscattering)
                atmosphereInscatteringLight+= Vector3.Scale(lightColor,atmosphereMieInscattering * miePhase(cosTh, atmosphereMieCoeff));
            float step = rayLength / numInScatteringPoints;
            dst = step / 2;
            for (int i = 0; i < numInScatteringPoints; ++i)
            {
                Vector3 pos = rayOrigin + rayDir * dst;
                float atmosphereStepDensity = getDensity(pos) * step;

                transmittance =Vector3.Scale(transmittance,exp(-atmosphereStepDensity * atmosphereAbsorption));
                
                Vector2 hitInfo = raySphere(atmosphereRadius, pos, dirToLight);
                float inscatteringAtmosphereDepth = getOpticalDepth(pos, dirToLight, hitInfo.y);
                //Todo Sphere Shadow
                light += Vector3.Scale(atmosphereEmission + Vector3.Scale(atmosphereInscatteringLight , exp(- inscatteringAtmosphereDepth*atmosphereAbsorption)) , atmosphereStepDensity * transmittance);
                dst += step;
            }

        }
        public void frag(Vector3 cameraPos, Vector3 viewVector, out Vector3 transmittance, out Vector3 light)
        {
            //Get the screen depth and camera ray
            Vector3 rayOrigin = worldToPlanetTRS.MultiplyPoint(cameraPos);
            Vector3 rayDir = Vector3.Normalize(worldToPlanetTRS.MultiplyVector(viewVector));

            //Intersect the ray to the atmosphere
            Vector2 hitInfo = raySphere(atmosphereRadius, rayOrigin, rayDir);
            float dstToAtmosphere = hitInfo.x;
            float dstThroughAtmosphere = hitInfo.y;

            raymarch(rayOrigin + rayDir * dstToAtmosphere, rayDir, dstThroughAtmosphere, out transmittance, out light);
        }

    }
    

}
