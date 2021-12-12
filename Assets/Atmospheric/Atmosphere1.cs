using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class Atmosphere1 : MonoBehaviour
{
    public Shader shader;
    public int numInScatteringPoints = 10;
    public int numOpticalDepthPoints = 10;
    public Transform sun;
    public Color sunlight = Color.white;//Do not use Light.color because sunlight is not yellow before atmosphere absorbtion
    public float sunlightStrength = 1;
    public Color ambientLight = Color.white;//Do not use Light.color because sunlight is not yellow before atmosphere absorbtion
    public float ambientLightStrength = 0;

    public Vector3 planetCenter;
    public float planetRadius=10;
    public bool alignGroundToPlanet = false;
    public float atmosphereHeight=2;
    public float densityFalloff=1;
    public Vector3 wavelengths = new Vector3(700, 530, 440);
    public float rayleigh=1;
    public float mie=.01f;
    public float mieG=-.75f;

    [Multiline(10)] public string debug_text;

    Material mat;
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!mat || mat.shader != shader && shader) { mat = new Material(shader); }

        Vector3 wavelengthAdjust = new Vector3(Mathf.Pow(440f / wavelengths.x, 4), Mathf.Pow(440f / wavelengths.y, 4), Mathf.Pow(440f / wavelengths.z, 4));

        if (alignGroundToPlanet)
            planetCenter.y = -planetRadius;

        float scale = planetRadius;

        mat.SetVector("planetCenter", planetCenter/scale);
        mat.SetVector("sunlightDir", sun.transform.forward);
        mat.SetVector("sunPos", sun.position/scale);
        mat.SetVector("sunlight", sunlight* sunlightStrength);
        mat.SetVector("ambientLight", ambientLight * ambientLightStrength);
        mat.SetFloat("planetRadius", planetRadius/ scale);
        mat.SetFloat("scale", scale);
        mat.SetFloat("atmosphereHeight", atmosphereHeight/ scale);
        mat.SetFloat("densityFalloff", densityFalloff);
        mat.SetVector("rayleighScattering", wavelengthAdjust*rayleigh* scale);
        mat.SetFloat("mieScattering", mie* scale);
        mat.SetFloat("mieG", mieG);
        mat.SetInt("numInScatteringPoints", numInScatteringPoints);
        mat.SetInt("numOpticalDepthPoints", numOpticalDepthPoints);

        Graphics.Blit(source, destination, mat);
    }
    private void OnValidate()
    {
        Debug.Assert(QualitySettings.activeColorSpace == ColorSpace.Linear);
    }
}
