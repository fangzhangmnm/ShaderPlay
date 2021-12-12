using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode, ImageEffectAllowedInSceneView]
public class Cloud1 : MonoBehaviour
{
    public Shader shader;
    public Transform container;
    public Texture3D noiseTexture;
    public Vector3 cloudScale=Vector3.one;
    public Vector3 cloudOffset;
    [ColorUsage(true,true)]
    public Color lightColor = Color.white;
    [ColorUsage(true, true)]
    public Color ambientColorUpper = Color.black;
    [ColorUsage(true, true)]
    public Color ambientColorLower = Color.black;
    public Color absorption = Color.white;
    public Color inscattering = Color.white;
    [Range(-1f, 1f)]
    public float miePhase = .5f;
    [Range(0, 2f)]
    public float powderCoeff = .5f;
    public float densityMultiplier=1;
    [Range(-1f,1f)]
    public float densityOffset = 0;
    [Range(0f, 1f)]
    public float cloudSize = 0;
    public float detailStrength = .1f;
    public float detailScale = 16.66f;
    public float stepSize = .1f;
    public float stepSizeDistCoeff = 5f;
    public float stepSizeDensityCoeff = 1f;
    public float minStepSizeCoeff = .25f;
    public float lightStepSize = .1f;
    public int maxInScatteringPoints = 10;
    public int minInScatteringPoints = 10;
    public int numStepsLight = 10;

    [Multiline(10)] public string debug_text;

    Material mat;   
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!mat || mat.shader != shader && shader) { mat = new Material(shader); }
        mat.SetVector("BoundsMin", container.position - container.localScale / 2);
        mat.SetVector("BoundsMax", container.position + container.localScale / 2);
        mat.SetVector("CloudScale", cloudScale);
        mat.SetVector("CloudOffset", cloudOffset);
        mat.SetColor("lightColor", lightColor);
        mat.SetColor("ambientColorUpper", ambientColorUpper);
        mat.SetColor("ambientColorLower", ambientColorLower);
        mat.SetFloat("DensityOffset", densityOffset);
        mat.SetFloat("CloudSize", cloudSize);
        mat.SetFloat("DetailStrength", detailStrength);
        mat.SetFloat("DetailScale", detailScale);
        mat.SetFloat("stepSize", stepSize);
        mat.SetFloat("stepSizeDistCoeff", 1/stepSizeDistCoeff);
        mat.SetFloat("stepSizeDensityCoeff", stepSizeDensityCoeff);
        mat.SetFloat("minStepSizeCoeff", minStepSizeCoeff);
        mat.SetFloat("lightStepSize", lightStepSize);
        mat.SetVector("mieCoeff", new Vector3((1 - miePhase * miePhase),1 + miePhase * miePhase, 2 * miePhase));//Do not normalize by 4pi
        mat.SetFloat("powderCoeff", 1/powderCoeff);
        mat.SetColor("absorption", absorption*densityMultiplier);
        mat.SetColor("inscattering", inscattering*densityMultiplier);
        mat.SetInt("maxInScatteringPoints", maxInScatteringPoints);
        mat.SetInt("minInScatteringPoints", minInScatteringPoints);
        mat.SetInt("numStepsLight", numStepsLight);
        mat.SetTexture("NoiseTexture", noiseTexture);
        Graphics.Blit(source, destination, mat);
    }
    private void OnValidate()
    {
        //minInScatteringPoints = Mathf.Min(maxInScatteringPoints, minInScatteringPoints);
        //Debug.Assert(minInScatteringPoints < maxInScatteringPoints);
        Debug.Assert(QualitySettings.activeColorSpace == ColorSpace.Linear);
        debug_text = $"noiseTexture dimensions: {noiseTexture.width},{noiseTexture.height},{noiseTexture.depth}";
    }
}
