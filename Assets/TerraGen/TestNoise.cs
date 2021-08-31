using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace fzmnm.Test
{
    public class TestNoise : MonoBehaviour
    {

        public Vector3Int size = new Vector3Int(32, 32, 32);
        public Texture2D tex2D;
        public Texture3D tex3D;
        public float scale = 4;
        public float persistence = .5f;
        public float lacunarity = 2f;
        public int octave = 4;
        [Range(1,256)]
        public int repeat = 256;

        [Button]
        public void Generate2DOctave()
        {
            Color[] colors = new Color[size.x * size.y];
            //left to right, bottom to top
            for (int y = 0; y < size.y; ++y)
                for (int x = 0; x < size.x; ++x)
                {
                    float value = Noise.Octave2D(x * scale / size.x, y * scale / size.y,octave:octave,persistence:persistence,lacunarity:lacunarity);
                    value = value / 2 + .5f;
                    Color color = new Color(value,value,value, value);
                    colors[x + y * size.x] = color;
                }
            Save2DTexture(colors);
        }
        [Button]
        public void Generate2DPerlin()
        {
            Color[] colors = new Color[size.x * size.y];
            //left to right, bottom to top
            for (int y = 0; y < size.y; ++y)
                for (int x = 0; x < size.x; ++x)
                {
                    float value = Noise.Perlin2D(x * scale / size.x, y * scale / size.y, repeat,repeat);
                    value = value / 2 + .5f;
                    Color color = new Color(value, value, value, value);
                    colors[x + y * size.x] = color;
                }
            Save2DTexture(colors);
        }
        [Button]
        public void Generate3DPerlin()
        {
            Color[] colors = new Color[size.x * size.y*size.z];
            //left to right, bottom to top, ? to ?
            for(int z=0;z<size.z;++z)
            for (int y = 0; y < size.y; ++y)
                for (int x = 0; x < size.x; ++x)
                {
                    float value = Noise.Perlin3D(x * scale / size.x, y * scale / size.y, z * scale / size.z, repeat, repeat, repeat);
                    Color color = new Color(value, value, value, value);
                    colors[x + y * size.x+z*size.x*size.y] = color;
                }
            Save3DTexture(colors);
        }
        void Save2DTexture(Color[] colors)
        {
            if (!tex2D || tex2D.width != size.x || tex2D.height != size.y) tex2D = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
            tex2D.SetPixels(colors);
            tex2D.Apply();
            if (!AssetDatabase.Contains(tex2D))
                AssetDatabase.CreateAsset(tex2D, EditorUtility.SaveFilePanelInProject("Save Texture2D", "NoiseTexture", "asset", "Save Texture2D"));
            else
                AssetDatabase.SaveAssets();
        }
        void Save3DTexture(Color[] colors)
        {
            if (!tex3D || tex3D.width != size.x || tex3D.height != size.y || tex3D.depth != size.z) tex3D = new Texture3D(size.x, size.y, size.z, TextureFormat.RGBA32, false);
            tex3D.SetPixels(colors);
            tex3D.Apply();
            if (!AssetDatabase.Contains(tex3D))
                AssetDatabase.CreateAsset(tex3D, EditorUtility.SaveFilePanelInProject("Save Texture3D", "NoiseTexture", "asset", "Save Texture3D"));
            else
                AssetDatabase.SaveAssets();
        }
    }
}
