using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class Gen3DTextures : MonoBehaviour
{
    public Vector3Int size = new Vector3Int(32, 32, 32);
    public Texture3D tex3D;
    public float scale = 1;

    [Button]
    void Generate3DTexture()
    {
        Color[] colors = new Color[size.x * size.y * size.z];
        for (int x = 0; x < size.x; ++x)
            for (int y = 0; y < size.y; ++y)
                for (int z = 0; z < size.z; ++z)
                {
                    Vector3 pos = new Vector3((float)x/size.x, (float)y/size.y, (float)z/size.z);
                    colors[x * size.y * size.z + y * size.z + z] = Color.white * (.5f*Perlin.Fbm(pos* scale, 6)+.5f);
                }

        if (!tex3D) tex3D = new Texture3D(size.x, size.y, size.z, TextureFormat.RGBA32, false);
        tex3D.SetPixels(colors);
        tex3D.Apply();
        if (!AssetDatabase.Contains(tex3D))
            AssetDatabase.CreateAsset(tex3D, EditorUtility.SaveFilePanelInProject("Save Texture3D", "NoiseTexture", "asset", "Save Texture3D"));
        else
            AssetDatabase.SaveAssets();
    }
}
