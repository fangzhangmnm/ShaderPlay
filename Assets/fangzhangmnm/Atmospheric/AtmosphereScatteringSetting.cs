using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace fzmnm
{

    [System.Serializable]
    public class AtmosphereScatteringSetting
    {
        [Header("Planet Geometry")]
        public Vector3 planetCenter;
        public Quaternion planetRotation = Quaternion.identity;
        public float planetRadius = 6371000f;
        public float atmosphereMaxHeight = 60000f;
        public bool autoScaleForNumericalStability = true;
        public float scaleForNumericalStability;
        public float sceneScaleMultiplier = 1f;

        [Header("Lighting")]
        [Range(500, 15000)] public float lightTemperature = 5778f;//in space
        public float lightIntensity = 1.2f;
        public bool overrideLightColorFromTemperature = true;
        [ColorUsage(false, true)] public Color lightColor;
        public Vector3 dirToSun=Vector3.up;
        [ColorUsage(false, true)] public Color planetColor = Color.white * .3f;
        public float sunDiscG = .99f;
        [Range(1, 500)] public float sunDiscConvergence = 100f;
        public float SkyLightMultiplier = 4f;

        [Header("Scattering")]
        public float density = 1.224f;
        public float scatteringMultiplier = 1e-5f / 1.224f;

        public float rayleighMultiplier = 1f;
        [ColorUsage(false, true)] public Color rayleighScattering = new Color(.5802f, 1.3558f, 3.31f);
        [ColorUsage(false, true)] public Color rayleighAbsorption = new Color(.5802f, 1.3558f, 3.31f);
        public Vector3 rayleighPhaseCoeff = new Vector3(1.12f, .4f, 0f) / (4 * Mathf.PI);
        public float rayleighScaleHeight = 8000f;

        public float mieMultiplier = 1f;
        [ColorUsage(false, true)] public Color mieScattering = new Color(.3996f, .3996f, .3996f);
        [ColorUsage(false, true)] public Color mieAbsorption = new Color(.440f, .440f, .440f)/2;
        [Range(-0.99f, 0.99f)] public float mieG = 0.8f;
        public float mieScaleHeight = 1200f;

        public float ozoneMultiplier = 1f;
        [ColorUsage(false, true)] public Color ozoneAbsorption = new Color(.0650f, .1881f, .0085f);
        public float ozoneMinHeight = 10000f;
        public float ozoneMaxHeight = 40000f;

        [Header("Color Correction")]
        public Matrix4x4 spectralColor2RGBMatrix = new Matrix4x4(
            new Vector4(1.6218f, -0.0374f, -0.0283f, 0),
            new Vector4(-0.4493f, 1.0598f, -0.1119f, 0),
            new Vector4(0.0325f, -0.0742f, 1.0491f, 0),
            new Vector4(0, 0, 0, 1));
        public Matrix4x4 RGB2spectralColorMatrix => Matrix4x4.Inverse(spectralColor2RGBMatrix);
        public bool useColorSpace = true;
        public bool useToneMapping = true;
        [Range(-10, 10)] public float toneMappingLogExposure = 0;

        public void SetMaterial(Material mat)
        {
            if (overrideLightColorFromTemperature) lightColor = Temperature2RGB(lightTemperature) * lightIntensity;

            if (autoScaleForNumericalStability)
                scaleForNumericalStability = Mathf.Sqrt(2 * planetRadius * atmosphereMaxHeight + atmosphereMaxHeight * atmosphereMaxHeight);

            float scale = scaleForNumericalStability;

            //Planet Geometry
            mat.SetMatrix("worldToPlanetTRS", Matrix4x4.Inverse(Matrix4x4.TRS(planetCenter / sceneScaleMultiplier, planetRotation, Vector3.one * scale / sceneScaleMultiplier)));
            mat.SetFloat("depthToPlanetS", sceneScaleMultiplier / scale);
            mat.SetFloat("planetRadius", (planetRadius) / scale);
            mat.SetFloat("atmosphereRadius", (atmosphereMaxHeight + planetRadius) / scale);

            //Sun Light
            mat.SetVector("dirToSun", Quaternion.Inverse(planetRotation) * dirToSun);
            mat.SetVector("sunColor", RGB2SpectralColor(lightColor * Mathf.PI * SkyLightMultiplier)); //should be pi instead of 4 pi. Why?
            mat.SetVector("planetColor", RGB2SpectralColor(planetColor)); //should be pi instead of 4 pi. Why?
            Vector4 v = getMieCoeff(sunDiscG); v.w = sunDiscConvergence;
            mat.SetVector("sunDiscCoeff", v);

            //Scattering
            mat.SetVector("atmosphere_rayleighScattering", scale * density * scatteringMultiplier * rayleighMultiplier * RGB2SpectralColor(rayleighScattering));
            mat.SetVector("atmosphere_rayleighAbsorption", scale * density * scatteringMultiplier * rayleighMultiplier * RGB2SpectralColor(rayleighAbsorption));
            mat.SetVector("atmosphere_rayleighPhaseCoeff", rayleighPhaseCoeff);
            mat.SetFloat("atmosphere_rayleighScaleHeight", rayleighScaleHeight / scale);

            mat.SetVector("atmosphere_mieScattering", scale * density * scatteringMultiplier * mieMultiplier * RGB2SpectralColor(mieScattering));
            mat.SetVector("atmosphere_mieAbsorption", scale * density * scatteringMultiplier * mieMultiplier * RGB2SpectralColor(mieAbsorption));
            float g2 = mieG * mieG;
            mat.SetVector("atmosphere_miePhaseCoeff", new Vector3(3f / (8f * Mathf.PI) * (1 - g2) / (2 + g2), 1 + g2, 2 * mieG));
            mat.SetFloat("atmosphere_mieScaleHeight", mieScaleHeight / scale);

            mat.SetVector("atmosphere_ozoneAbsorption", scale * density * scatteringMultiplier * ozoneMultiplier* RGB2SpectralColor(ozoneAbsorption));
            mat.SetFloat("atmosphere_ozoneMeanRadius", ((ozoneMinHeight + ozoneMaxHeight) / 2 + planetRadius) / scale);
            mat.SetFloat("atmosphere_ozoneHalfDeltaRadius", (ozoneMaxHeight - ozoneMinHeight) / 2 / scale);

            //Color Correction
            mat.SetMatrix("spectralColor2RGB", useColorSpace ? spectralColor2RGBMatrix : Matrix4x4.identity);
            mat.SetMatrix("RGB2spectralColor", useColorSpace ? RGB2spectralColorMatrix : Matrix4x4.identity);
            mat.SetFloat("toneMappingExposure", useToneMapping ? Mathf.Exp(toneMappingLogExposure) : 0);
        }

        Vector3 getMieCoeff(float g, float boost = 1)
        {
            float g2 = g * g;
            return new Vector3(boost * 3f / (8f * Mathf.PI) * (1 - g2) / (2 + g2), 1 + g2, 2 * g);
        }
        public Vector3 RGB2SpectralColor(Color color)
        {
            Vector3 v = new Vector3(color.r, color.g, color.b);
            v = RGB2spectralColorMatrix.MultiplyVector(v);
            return v;
        }
        public Color SpectralColor2RGB(Vector3 color)
        {
            color = spectralColor2RGBMatrix.MultiplyVector(color);
            return new Color(Mathf.Max(0, color.x), Mathf.Max(0, color.y), Mathf.Max(0, color.z), 1);
        }
        public Color Temperature2RGB(float temperature)
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
        Vector3 exp(Vector3 v) => new Vector3(Mathf.Exp(v.x), Mathf.Exp(v.y), Mathf.Exp(v.z));
        public Vector3 ToneMapping(Vector3 color) => useToneMapping ? Vector3.one - exp(-color * toneMappingLogExposure) : color;
    }

}
