using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode,ImageEffectAllowedInSceneView]
public class TestFx : MonoBehaviour
{
    public Shader shader;
    Material mat;
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!mat) mat = new Material(shader);

        mat.SetVector("sphereCenter", Vector3.zero);
        mat.SetFloat("sphereRadius", 1);

        Graphics.Blit(source, destination, mat);

    }
}
