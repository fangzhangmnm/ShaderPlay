using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace fzmnm.Test
{

    public class TestNoise2 : MonoBehaviour
    {
        public Vector2Int size = new Vector2Int(128, 128);
        public float scale = .25f;
        public float persistence = .5f;
        public float lacunarity = 2f;
        public int octave = 4;
        private void OnValidate()
        {
            Color[] colors = new Color[size.x * size.y];
            //left to right, bottom to top
            for (int y = 0; y < size.y; ++y)
                for (int x = 0; x < size.x; ++x)
                {
                    float value = Noise.PerlinOctave2D(x / scale / size.x, y / scale / size.y, octave: octave, persistence: persistence, lacunarity: lacunarity);
                    value = value / 2 + .5f;
                    Color color = new Color(value, value, value, value);
                    colors[x + y * size.x] = color;
                }
            Save2DTexture(colors);
        }
        void Save2DTexture(Color[] colors)
        {
            var mat = GetComponent<MeshRenderer>().sharedMaterial;
            var tex2D = (Texture2D)mat.GetTexture("_MainTex");
            if (!tex2D || tex2D.width != size.x || tex2D.height != size.y)
                tex2D = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
            mat.SetTexture("_MainTex", tex2D);
            tex2D.SetPixels(colors);
            tex2D.Apply();
        }
    }

}