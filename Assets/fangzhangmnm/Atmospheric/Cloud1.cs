using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class Cloud1 : MonoBehaviour
{
    public Shader shader;

    [Header("Planet Geometry")]
    public Vector3 planetCenter;
    public bool alignGroundToPlanet = true;
    public Quaternion planetRotation=Quaternion.identity;
    public float planetRadius = 100000;
    public float cloudMinHeight = 500;
    public float cloudMaxHeight = 2000;
    public float atmosphereHeight = 10000;

    [Header("Cloud Volume Textures")]
    public Texture3D noiseTexture;
    public Texture3D detailNoiseTexture;

    [Header("Cloud Parameters")]
    public float cloudScale = 2000;
    public float detailScaleDivisor = 16.6f;
    public Vector3 cloudPositionOffset, detailPositionOffset;
    [Range(-1f, 1f)] public float densityOffset = 0;
    [Range(0, 1f)] public float detailStrength = .1f;
    [Range(0, 1f)] public float cloudType = 0;

    [Header("Cloud Scattering Parameters")]
    [ColorUsage(true, true)] public Color lightColor = Color.white;
    [ColorUsage(true, true)] public Color ambientColorUpper = Color.black;
    [ColorUsage(true, true)] public Color ambientColorLower = Color.black;
    [ColorUsage(true, true)] public Color cloudAbsorption = Color.white;
    [ColorUsage(true, true)] public Color cloudInscattering = Color.white;
    public float cloudDensity = 1;
    [Range(-1f, 1f)] public float cloudMiePhase = .5f;
    [Range(0, 2f)] public float invCloudPowderCoeff = .5f;

    [Header("Atmosphere Scattering Parameters")]
    public float atmosphereDensity = 1;
    public float atmosphereDensityFalloffHeight = 1;
    public Vector3 wavelengths = new Vector3(700, 530, 440);
    public bool calculateScatteringFromWavelength = true;
    [ColorUsage(true, true)] public Color atmosphereAbsorption = Color.white;
    [ColorUsage(true, true)] public Color atmosphereInscattering = Color.white;

    [Header("LightMarch Parameters")]
    public float stepSize = .1f;
    public float lightStepSize = .1f;
    public float stepSizeDistCoeff = 5f;
    public float invStepSizeDensityCoeff = 1f;
    public float minStepSizeMultiplier = .25f;
    public int maxInScatteringPoints = 10;
    public int minInScatteringPoints = 10;
    public int numStepsLight = 10;

    [Header("PostProcessing Parameters")]
    public bool useToneMapping = true;
    [Range(0.01f,5)]public float toneMappingExposure = 2;

    [Multiline(10)] public string debug_text;

    Material mat;   
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!mat || mat.shader != shader && shader) { mat = new Material(shader); }

        if (alignGroundToPlanet)
            planetCenter.y = -planetRadius;
        float scale = 1;
        mat.SetMatrix("worldToPlanetTRS",
            Matrix4x4.Inverse(Matrix4x4.TRS(planetCenter, planetRotation, Vector3.one * scale)));
        mat.SetFloat("worldToPlanetS", 1 / scale);
        mat.SetFloat("planetRadius", planetRadius / scale);
        mat.SetFloat("cloudMinRadius", (cloudMinHeight+planetRadius) / scale);
        mat.SetFloat("cloudDeltaRadius", (cloudMaxHeight-cloudMinHeight) / scale);
        mat.SetFloat("atmosphereRadius", (atmosphereHeight + planetRadius) / scale);

        mat.SetTexture("NoiseTexture", noiseTexture);
        mat.SetTexture("DetailNoiseTexture", detailNoiseTexture);


        mat.SetFloat("cloudScale", cloudScale / scale);
        mat.SetFloat("detailScale", cloudScale/detailScaleDivisor / scale);
        mat.SetVector("cloudPositionOffset", cloudPositionOffset / scale);
        mat.SetVector("detailPositionOffset", detailPositionOffset / scale);
        mat.SetFloat("densityOffset", densityOffset);
        mat.SetFloat("detailStrength", detailStrength);
        mat.SetFloat("cloudType", cloudType);

        mat.SetColor("lightColor", lightColor);
        mat.SetColor("cloudAbsorption", cloudAbsorption * cloudDensity * scale);
        mat.SetColor("cloudInscattering", cloudInscattering * cloudDensity * scale);
        mat.SetColor("ambientColorUpper", ambientColorUpper);
        mat.SetColor("ambientColorLower", ambientColorLower);
        float g2 = cloudMiePhase * cloudMiePhase;
        mat.SetVector("cloudMieCoeff", new Vector3(1.5f*(1 - g2)/(2+g2), 1 + g2, 2 * cloudMiePhase));
        mat.SetFloat("cloudPowderCoeff", 1 / invCloudPowderCoeff);

        mat.SetColor("atmosphereAbsorption", atmosphereAbsorption * atmosphereDensity * scale);
        mat.SetColor("atmosphereInscattering", atmosphereInscattering * atmosphereDensity * scale);

        mat.SetFloat("stepSize", stepSize/ scale);
        mat.SetFloat("lightStepSize", lightStepSize/ scale);
        //float step=min(stepSize*clamp(stepSizeDensityCoeff*abs(density),minStepSizeMultiplier,1)*clamp(dst/stepSize*stepSizeDistCoeff,1,4),rayLength/minInScatteringPoints-.001f);
        mat.SetFloat("stepSizeDistCoeff", stepSizeDistCoeff);
        mat.SetFloat("stepSizeDensityCoeff", 1/invStepSizeDensityCoeff);
        mat.SetFloat("minStepSizeMultiplier", minStepSizeMultiplier);
        mat.SetInt("maxInScatteringPoints", maxInScatteringPoints);
        mat.SetInt("minInScatteringPoints", minInScatteringPoints);
        mat.SetInt("numStepsLight", numStepsLight);

        mat.SetFloat("toneMappingExposure", useToneMapping? toneMappingExposure:0);

        Graphics.Blit(source, destination, mat);
    }
    private void OnValidate()
    {
        //minInScatteringPoints = Mathf.Min(maxInScatteringPoints, minInScatteringPoints);
        //Debug.Assert(minInScatteringPoints < maxInScatteringPoints);
        Debug.Assert(QualitySettings.activeColorSpace == ColorSpace.Linear);
        if (alignGroundToPlanet)
            planetCenter.y = -planetRadius;
        wavelengths.y = Mathf.Max(wavelengths.y, wavelengths.z);
        wavelengths.x = Mathf.Max(wavelengths.x, wavelengths.y);
        if (calculateScatteringFromWavelength)
        {
            Color wavelengthAdjust = new Color(Mathf.Pow(440f / wavelengths.x, 4), Mathf.Pow(440f / wavelengths.y, 4), Mathf.Pow(440f / wavelengths.z, 4),1);
            atmosphereAbsorption = atmosphereInscattering = wavelengthAdjust;
        }
        debug_text = $"noiseTexture dimensions: {noiseTexture.width},{noiseTexture.height},{noiseTexture.depth}";
    }
}
