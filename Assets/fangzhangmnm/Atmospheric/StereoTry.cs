using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StereoTry : MonoBehaviour
{
    public Shader shader; private Material mat; new private Camera camera;
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!mat || mat.shader != shader && shader) { mat = new Material(shader); }
        Graphics.Blit(source, destination, mat);
    }
}
