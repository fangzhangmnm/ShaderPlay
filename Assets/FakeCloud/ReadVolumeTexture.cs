using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
namespace fzmnm
{
    public class ReadVolumeTexture : MonoBehaviour
    {
        public Texture2D slicedTexture2D;
        public Texture3D generatedTexture3D;
        public int col = 8, row = 8;
        public int width, height, depth;
        [Button]
        public void GenerateTexture3D()
        {
            if (!slicedTexture2D) return;
            width = slicedTexture2D.width / col;
            height = slicedTexture2D.height / row;
            depth = col * row;
            if (!generatedTexture3D) generatedTexture3D = new Texture3D(width, height, depth, TextureFormat.Alpha8, false);
            Color[] colors1 = slicedTexture2D.GetPixels(0);
            Color[] colors2 = new Color[width * height * depth];
            for (int i = 0; i < col; ++i)
                for (int j = 0; j < row; ++j)
                    for (int k = 0; k < width; ++k)
                        for (int l = 0; l < height; ++l)
                        {
                            int ii = i * width + k;
                            int jj = j * height + l;
                            int ij = i + (row - j - 1) * col;
                            Color c = colors1[ii + jj * slicedTexture2D.width];
                            colors2[k + l * width + ij * width * height] = new Color(1, 1, 1, c.grayscale);
                        }
            generatedTexture3D.SetPixels(colors2);
            generatedTexture3D.Apply();
            generatedTexture3D.wrapMode = TextureWrapMode.Clamp;
            if (!AssetDatabase.Contains(generatedTexture3D))
                AssetDatabase.CreateAsset(generatedTexture3D, EditorUtility.SaveFilePanelInProject("Save Texture3D", "VolumeTexture", "asset", "Save Texture3D"));
            else
                AssetDatabase.SaveAssets();
        }
    }
}
