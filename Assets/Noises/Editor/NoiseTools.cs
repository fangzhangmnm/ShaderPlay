using UnityEngine;
using UnityEditor;
namespace fzmnm.EditorTools
{
    public class NoiseTools : EditorWindow
    {
        [MenuItem("Window/Tools/Noise Generator")]
        static void Init()
        {
            GetWindow(typeof(NoiseTools), false, "Noise Generator").Show();
        }
        Texture2D tex2D;
        Texture3D tex3D;


        private void OnGUI()
        {
            Texture2D displayTexture = tex2D;
            if (displayTexture)
            {
                if (GUILayout.Button("Save PNG")) SavePNG();
                if (tex3D)
                    if (GUILayout.Button("Save Texture3D Asset")) 
                        SaveTexture3D();

                EditorGUILayout.LabelField("Output Preview:");
                if (tex3D)
                {
                    EditorGUI.BeginChangeCheck();
                    sliceZ = EditorGUILayout.IntSlider("Z slice", sliceZ, 0, tex3D.depth - 1);
                    if (EditorGUI.EndChangeCheck()) UpdateZSlice();
                }
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Zoom in")) textureZoom *= 1.25f;
                if (GUILayout.Button("Zoom out")) textureZoom /= 1.25f;
                EditorGUILayout.EndHorizontal();

                float aspect = displayTexture.width / (float)displayTexture.height;
                float width = EditorGUIUtility.currentViewWidth;

                textureScroll = GUILayout.BeginScrollView(textureScroll, false, false, GUILayout.Height(width), GUILayout.Width(width));


                GUILayoutUtility.GetRect(0, 0, textureZoom * width, textureZoom * width / aspect, GUILayout.MinWidth(textureZoom * width));
                EditorGUI.DrawPreviewTexture(new Rect(0, 0, textureZoom * width, textureZoom * width / aspect), displayTexture);

                GUILayout.EndScrollView();

            }

            resolution = EditorGUILayout.Vector3IntField("resolution", resolution);
            textureFormat = (TextureFormat)EditorGUILayout.EnumPopup("Texture Format", textureFormat);

            editorWindowScroll = EditorGUILayout.BeginScrollView(editorWindowScroll);

            seed = (byte)EditorGUILayout.IntSlider("seed", seed, 0, 255);
            scale = EditorGUILayout.Vector3Field("scale", scale);
            persistence = EditorGUILayout.FloatField("persistence", persistence);
            lacunarity = EditorGUILayout.FloatField("lacunarity", lacunarity);
            octave = EditorGUILayout.IntSlider("octave", octave,1,16);
            repeat = EditorGUILayout.Toggle("repeat", repeat);
            flip = EditorGUILayout.Toggle("flip", flip);
            if (repeat)
            {
                repeatX = Mathf.Max(1, Mathf.RoundToInt(scale.x));
                repeatY = Mathf.Max(1, Mathf.RoundToInt(scale.y));
                repeatZ = Mathf.Max(1, Mathf.RoundToInt(scale.z));
            }
            else
            {
                repeatX = repeatY = repeatZ = int.MaxValue;
            }
            EditorGUILayout.LabelField("WriteToChannel");
            writeToR = EditorGUILayout.Toggle("R", writeToR);
            writeToG = EditorGUILayout.Toggle("G", writeToG);
            writeToB = EditorGUILayout.Toggle("B", writeToB);
            writeToA = EditorGUILayout.Toggle("A", writeToA);

            if (GUILayout.Button("GeneratePerlin2D")) GeneratePerlin2D();
            if (GUILayout.Button("GeneratePerlin3D")) GeneratePerlin3D();
            if (GUILayout.Button("GenerateWorley2D")) GenerateVoronoi2D();
            if (GUILayout.Button("GenerateWorley3D")) GenerateVoronoi3D();

            EditorGUILayout.EndScrollView();
        }
        TextureFormat textureFormat=TextureFormat.RGBA32;
        Vector3Int resolution = new Vector3Int(32, 32, 32);
        Vector3 scale = new Vector3(4,4,4);
        float persistence = .5f;
        float lacunarity = 2f;
        int octave = 4;
        bool repeat = true;
        int repeatX,repeatY,repeatZ;
        byte seed = 0;
        int sliceZ=0;
        bool flip = false;
        bool writeToR = true, writeToG = true, writeToB = true, writeToA = true;

        Vector2 textureScroll = Vector2.zero;
        float textureZoom = 1;
        Vector2 editorWindowScroll = Vector2.zero;








        public void GeneratePerlin2D()
        {
            float[] values = new float[resolution.x * resolution.y];
            //left to right, bottom to top
            for (int y = 0; y < resolution.y; ++y)
                for (int x = 0; x < resolution.x; ++x)
                {
                    float value = Noise.PerlinOctave2D(x * scale.x / resolution.x, y * scale.y / resolution.y,
                        octave: octave, persistence: persistence, lacunarity: lacunarity,
                        repeatX: repeatX, repeatY: repeatY, seed: seed);
                    value = value / 2 + .5f;
                    values[x + y * resolution.x] = value;
                }
            Set2DTexture(values);
        }
        public void GeneratePerlin3D()
        {
            float[] values = new float[resolution.x * resolution.y * resolution.z];
            //left to right, bottom to top, ? to ?
            for (int z = 0; z < resolution.z; ++z)
                for (int y = 0; y < resolution.y; ++y)
                    for (int x = 0; x < resolution.x; ++x)
                    {
                        float value = Noise.PerlinOctave3D(x * scale.x / resolution.x, y * scale.y / resolution.y, z * scale.z / resolution.z,
                            octave: octave, persistence: persistence, lacunarity: lacunarity,
                            repeatX: repeatX, repeatY: repeatY, repeatZ: repeatZ, seed: seed);
                        Color color = new Color(value, value, value, value);
                        value = value / 2 + .5f;
                        values[x + y * resolution.x + z * resolution.x * resolution.y] = value;
                    }
            Set3DTexture(values);
        }
        public void GenerateVoronoi2D()
        {
            float[] values = new float[resolution.x * resolution.y];
            //left to right, bottom to top
            for (int y = 0; y < resolution.y; ++y)
                for (int x = 0; x < resolution.x; ++x)
                {
                    float value = Noise.WorleyOctave2D(x * scale.x / resolution.x, y * scale.y / resolution.y,
                        octave: octave, persistence: persistence, lacunarity: lacunarity,
                        repeatX: repeatX, repeatY: repeatY, seed: seed);
                    value = value / 2 + .5f;
                    values[x + y * resolution.x] = value;
                }
            Set2DTexture(values);
        }
        public void GenerateVoronoi3D()
        {
            float[] values = new float[resolution.x * resolution.y * resolution.z];
            //left to right, bottom to top, ? to ?
            for (int z = 0; z < resolution.z; ++z)
                for (int y = 0; y < resolution.y; ++y)
                    for (int x = 0; x < resolution.x; ++x)
                    {
                        float value = Noise.WorleyOctave3D(x * scale.x / resolution.x, y * scale.y / resolution.y, z * scale.z / resolution.z,
                            octave: octave, persistence: persistence, lacunarity: lacunarity,
                            repeatX: repeatX, repeatY: repeatY, repeatZ: repeatZ, seed: seed);
                        value = value / 2 + .5f;
                        values[x + y * resolution.x + z * resolution.x * resolution.y] = value;
                    }
            Set3DTexture(values);
        }
        void Set2DTexture(float[] values)
        {
            if (!(tex2D && tex2D.width == resolution.x && tex2D.height == resolution.y && tex2D.format== textureFormat))
                tex2D = new Texture2D(resolution.x, resolution.y, textureFormat, true);

            Color[] colors = tex2D.GetPixels();
            for(int i=0;i<values.Length;++i)
            {
                float value = values[i];
                if (flip) value = 1 - value;
                if (writeToR) colors[i].r = value;
                if (writeToG) colors[i].g = value;
                if (writeToB) colors[i].b = value;
                if (writeToA) colors[i].a = value;
            }
            tex2D.SetPixels(colors);
            tex2D.Apply();

            tex3D = null;
        }
        void Set3DTexture(float[] values)
        {
            if (!(tex3D && tex3D.width == resolution.x && tex3D.height == resolution.y && tex3D.depth==resolution.z && tex3D.format== textureFormat))
                tex3D = new Texture3D(resolution.x, resolution.y, resolution.z, textureFormat, true);

            Color[] colors = tex3D.GetPixels();
            for (int i = 0; i < values.Length; ++i)
            {
                float value = values[i];
                if (flip) value = 1 - value;
                if (writeToR) colors[i].r = value;
                if (writeToG) colors[i].g = value;
                if (writeToB) colors[i].b = value;
                if (writeToA) colors[i].a = value;
            }
            tex3D.SetPixels(colors);
            tex3D.Apply();
            UpdateZSlice();
        }
        void UpdateZSlice()
        {
            Color[] c1 = tex3D.GetPixels();
            Color[] c2 = new Color[resolution.x * resolution.y];
            for (int i = 0; i < resolution.x * resolution.y; ++i)
                c2[i] = c1[i + sliceZ * resolution.x * resolution.y];
            tex2D = new Texture2D(resolution.x, resolution.y, tex3D.format, true);
            tex2D.SetPixels(c2);
            tex2D.Apply();
        }



        string oldPathPNG;
        void SavePNG()
        {
            if (tex2D)
            {
                string filename = oldPathPNG = EditorUtility.SaveFilePanelInProject("Save PNG", "Texture", "png", "Save PNG", oldPathPNG);
                if (filename.Length == 0) return;
                System.IO.File.WriteAllBytes(filename, tex2D.EncodeToPNG());
                AssetDatabase.ImportAsset(filename);
            }
        }
        string oldPathAsset;
        void SaveTexture3D()
        {
            if (tex3D)
            {
                string filename = oldPathAsset = EditorUtility.SaveFilePanelInProject("Save Texture3D", "NoiseTexture", "asset", oldPathAsset);
                AssetDatabase.DeleteAsset(filename);
                AssetDatabase.CreateAsset(tex3D, filename);
            }
        }
    }
}