using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace fzmnm
{
    public class GenerateNoiseTexture : MonoBehaviour
    {
        public Vector3Int size = new Vector3Int(32, 32, 32);
        public Texture3D tex3D;
        public int scale = 4;
        public float persistence = .5f;
        public float lacunarity = 2f;
        public int octave = 4;


        [Button]
        public void Generate3DPerlin()
        {
            Color[] colors = new Color[size.x * size.y * size.z];
            //left to right, bottom to top, ? to ?
            for (int z = 0; z < size.z; ++z)
                for (int y = 0; y < size.y; ++y)
                    for (int x = 0; x < size.x; ++x)
                    {
                        int repeat = scale;
                        float xx = (float)x / size.x * scale, yy = (float)y / size.y * scale, zz = (float)z/size.z * scale;
                        float value = 0;
                        float amplitude = 1;
                        for(int i = 0; i < octave; ++i)
                        {
                            value += amplitude*(Noise.Perlin3D(xx, yy, zz, repeat, repeat, repeat)-.5f)*2;
                            xx *= 2;yy *= 2;zz *= 2;
                            repeat = Mathf.Min(repeat * 2, 256);
                            amplitude *= persistence;
                        }
                        value = (value + 1) / 2;
                        Color color = new Color(1, 1, 1, value);
                        colors[x + y * size.x + z * size.x * size.y] = color;
                    }

            if (!tex3D || tex3D.width != size.x || tex3D.height != size.y || tex3D.depth != size.z) tex3D = new Texture3D(size.x, size.y, size.z, TextureFormat.Alpha8, false);
            tex3D.SetPixels(colors);
            tex3D.Apply();
            if (!AssetDatabase.Contains(tex3D))
                AssetDatabase.CreateAsset(tex3D, EditorUtility.SaveFilePanelInProject("Save Texture3D", "NoiseTexture", "asset", "Save Texture3D"));
            else
                AssetDatabase.SaveAssets();
        }
    }

}
