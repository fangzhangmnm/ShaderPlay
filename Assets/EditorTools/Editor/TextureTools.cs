using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Rendering.PostProcessing;
using UnityEditor;
namespace fzmnm.EditorTools
{
    public class TextureTools : EditorWindow
    {
        [MenuItem("Window/Tools/Texture Tools")]
        static void Init()
        {
            GetWindow(typeof(TextureTools),false,"Texture Tools").Show();
        }
        Texture2D selectedTexture;
        Texture2D inputTexture;
        Texture2D outputTexture;
        AnimationCurve rCurve, gCurve, bCurve, rgbCurve;
        float normalStrength = 2;
        int normalRange = 5;
        float hue, saturate, vibrance, contrast;
        Color lift, gamma, gain;
        Color detailBias=Color.gray;
        string oldPath = "";
        void LoadDefaultColorCorrection()
        {
            hue = 0;
            saturate = vibrance=contrast= 1;
            rgbCurve = rCurve = gCurve = bCurve = AnimationCurve.Linear(0,0,1,1);
            lift = gamma = gain = Color.gray;
        }
        bool autoUpdate = false;
        private void OnGUI()
        {
            selectedTexture = (Texture2D)EditorGUILayout.ObjectField("Select a Texture", selectedTexture, typeof(Texture2D),allowSceneObjects:false);
            if (selectedTexture)
            {
                if (GUILayout.Button("Load Texture (Will Mark Texture Readable)"))
                {
                    SetTextureReadable(selectedTexture, true);
                    outputTexture=inputTexture = selectedTexture;
                    textureZoom = 1;
                }
            }
            if (outputTexture)
            {
                if (GUILayout.Button("Use Output as Input"))
                    inputTexture = outputTexture;
                if (GUILayout.Button("Save PNG")) SavePNG();
                if (GUILayout.Button("Save JPG")) SaveJPG();
            }
            if (outputTexture)
            {
                EditorGUILayout.LabelField("Output Preview:");
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Zoom in")) textureZoom *= 1.25f;
                if (GUILayout.Button("Zoom out")) textureZoom /= 1.25f;
                EditorGUILayout.EndHorizontal();

                float aspect = outputTexture.width / (float)outputTexture.height;
                float width = EditorGUIUtility.currentViewWidth;

                textureScroll = GUILayout.BeginScrollView(textureScroll, false, false, GUILayout.Height(width), GUILayout.Width(width));


                GUILayoutUtility.GetRect(0, 0, textureZoom * width, textureZoom * width / aspect, GUILayout.MinWidth(textureZoom*width));
                EditorGUI.DrawPreviewTexture(new Rect(0, 0, textureZoom * width, textureZoom * width / aspect), outputTexture);

                GUILayout.EndScrollView();
            }
            editorWindowScroll = GUILayout.BeginScrollView(editorWindowScroll, false, true);
            if (inputTexture)
            {
                HorizontalLine("Normal Map:");

                normalStrength = EditorGUILayout.FloatField("Normal Strength", normalStrength);
                normalRange =  Mathf.Clamp(EditorGUILayout.IntField("Range", normalRange),1,10);
                if (GUILayout.Button("Create Normal Map"))CreateNormalMap();


                HorizontalLine("Detail Map:");
                detailBias = EditorGUILayout.ColorField("Normalize (Subtract)", detailBias);
                if (GUILayout.Button("Convert to Detail Map")) ConvertDetailMap();


                HorizontalLine("Color Correction:");

                if (GUILayout.Button("Apply Color Correction")) ColorCorrection();
                if (GUILayout.Button("Load Default Color Correction"))
                {
                    LoadDefaultColorCorrection();
                    if (autoUpdate)
                        ColorCorrection();
                }

                autoUpdate = EditorGUILayout.Toggle("Auto Update", autoUpdate);

                EditorGUI.BeginChangeCheck();

                lift = EditorGUILayout.ColorField("Dark (Lift)", lift);
                gamma = EditorGUILayout.ColorField("Midtone (Gamma)", gamma);
                gain = EditorGUILayout.ColorField("Lights (Gain)", gain);

                rCurve = (EditorGUILayout.CurveField("Red Curve", rCurve));
                gCurve = (EditorGUILayout.CurveField("Green Curve", gCurve));
                bCurve = (EditorGUILayout.CurveField("Blue Curve", bCurve));

                rgbCurve = (EditorGUILayout.CurveField("RGB Curve", rgbCurve));

                hue = EditorGUILayout.Slider("Hue", hue, -.5f, .5f);
                saturate = EditorGUILayout.Slider("Saturation", saturate, 0, 2);
                contrast = EditorGUILayout.Slider("Contrast", contrast, 0, 2);
                vibrance = EditorGUILayout.Slider("Vibrance", vibrance, 0, 2);


                if (EditorGUI.EndChangeCheck() && autoUpdate) ColorCorrection();

            }


            EditorGUILayout.EndScrollView();
        }
        Vector2 textureScroll = Vector2.zero;
        float textureZoom = 1;
        Vector2 editorWindowScroll = Vector2.zero;
        void HorizontalLine(string text="")
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.LabelField(text);
        }

        void CreateNormalMap()
        {
            int w = inputTexture.width, h = inputTexture.height;
            outputTexture = new Texture2D(w, h, TextureFormat.RGB24, true);
            Color[] input = inputTexture.GetPixels();//Left to Right, Down to Up
            Color[] output = new Color[w * h];
            for (int y = 0; y < h; ++y)
                for (int x = 0; x < w; ++x)
                {
                    Vector3 totalNormal = Vector3.zero;

                    for (int del = 1; del <= normalRange; ++del)
                    {
                        //float m = getHeight(input[getID(x, y, w, h)]);
                        float l = Luminance(input[GetID(x - del, y, w, h)]);
                        float r = Luminance(input[GetID(x + del, y, w, h)]);
                        float d = Luminance(input[GetID(x, y - del, w, h)]);
                        float u = Luminance(input[GetID(x, y + del, w, h)]);
                        Vector3 normal = new Vector3((l - r) * normalStrength / (2 * del), (u - d) * normalStrength / (2 * del), 1);
                        totalNormal += normal;
                    }

                    totalNormal = (totalNormal.normalized + Vector3.one) * .5f;
                    output[GetID(x, y, w, h)] = new Color(totalNormal.x, totalNormal.y, totalNormal.z);
                }
            outputTexture.SetPixels(output);
            outputTexture.Apply();
        }

        //float hue = settings.hueShift.value / 360f;         // Remap to [-0.5;0.5]
        //float sat = settings.saturation.value / 100f + 1f;  // Remap to [0;2]
        //float con = settings.contrast.value / 100f + 1f;    // Remap to [0;2]

        void ConvertDetailMap()
        {
            int w = inputTexture.width, h = inputTexture.height;
            outputTexture = new Texture2D(w, h, TextureFormat.RGB24, true);
            Color[] input = inputTexture.GetPixels();//Left to Right, Down to Up
            Color[] output = new Color[w * h];
            for (int y = 0; y < h; ++y)
                for (int x = 0; x < w; ++x)
                {
                    Color c = input[GetID(x, y, w, h)];

                    c.r = c.r - detailBias.r + .5f;
                    c.g = c.g - detailBias.g + .5f;
                    c.b = c.b - detailBias.b + .5f;

                    output[GetID(x, y, w, h)] = c;
                }
            outputTexture.SetPixels(output);
            outputTexture.Apply();
        }

        void ColorCorrection()
        {
            int w = inputTexture.width, h = inputTexture.height;
            outputTexture = new Texture2D(w, h, TextureFormat.RGB24, true);
            Color[] input = inputTexture.GetPixels();//Left to Right, Down to Up
            Color[] output = new Color[w * h];
            for (int y = 0; y < h; ++y)
                for (int x = 0; x < w; ++x)
                {
                    Color c = input[GetID(x, y, w, h)];

                    c.r = LiftGammaGain(c.r, (lift.r * 2 - 1), 1/(gamma.r*2), gain.r*2);
                    c.g = LiftGammaGain(c.g, (lift.g * 2 - 1), 1/(gamma.g*2), gain.g*2);
                    c.b = LiftGammaGain(c.b, (lift.b * 2 - 1), 1/(gamma.b*2), gain.b*2);
                    c.r = rgbCurve.Evaluate(rCurve.Evaluate(c.r));
                    c.g = rgbCurve.Evaluate(gCurve.Evaluate(c.g));
                    c.b = rgbCurve.Evaluate(bCurve.Evaluate(c.b));



                    Vector3 hsv = RgbToHsv(c);
                    hsv.x = Mathf.Repeat(hsv.x + hue, 1);
                    hsv.y = Mathf.Clamp01(hsv.y * saturate);
                    c = HsvToRgb(hsv);

                    c = Contrast(c, .5f, contrast);
                    c = Vibrance(c, vibrance);

                    output[GetID(x, y, w, h)] = c;
                }
            outputTexture.SetPixels(output);
            outputTexture.Apply();
        }
        void SavePNG()
        {
            if (outputTexture)
            {
                string filename = oldPath=EditorUtility.SaveFilePanelInProject("Save PNG", "Texture", "png", "Save PNG", oldPath);
                if (filename.Length == 0) return;
                System.IO.File.WriteAllBytes(filename, outputTexture.EncodeToPNG());
                AssetDatabase.ImportAsset(filename);
            }
        }
        void SaveJPG()
        {
            if (outputTexture)
            {
                string filename = oldPath=EditorUtility.SaveFilePanelInProject("Save JPG", "Texture", "jpg", "Save JPG", oldPath);
                if (filename.Length == 0) return;
                System.IO.File.WriteAllBytes(filename, outputTexture.EncodeToJPG());
                AssetDatabase.ImportAsset(filename);
            }
        }
        void SetTextureReadable(Texture2D texture, bool isReadable)
        {
            if (null == texture) return;

            string assetPath = AssetDatabase.GetAssetPath(texture);
            var tImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (tImporter != null)
            {
                tImporter.isReadable = isReadable;

                AssetDatabase.ImportAsset(assetPath);
                AssetDatabase.Refresh();
            }
        }
        static int GetID(int x, int y, int w, int h) => (x % w + w) % w + ((y % h + h) % h) * w;
        static float LiftGammaGain(float c, float lift, float invgamma, float gain)
        {
            c = c * gain + lift;
            return Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), invgamma);
        }
        static Vector4 RgbToHsv(Color c)
        {
            // Ranges: Hue [0.0, 1.0] Sat [0.0, 1.0] Lum [0.0, HALF_MAX]

            Vector4 K = new Vector4(0.0f, -1.0f / 3.0f, 2.0f / 3.0f, -1.0f);

            //float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));//step(y,x)=x>=y?1:0
            //float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));//step(x,y)=x<=y?1:0
            Vector4 p = c.b <= c.g ? new Vector4(c.g, c.b, K.x, K.y) : new Vector4(c.b, c.g, K.w, K.z);
            Vector4 q = p.x <= c.r ? new Vector4(c.r, p.y, p.z, p.x) : new Vector4(p.x, p.y, p.w, c.r);
            float d = q.x - Mathf.Min(q.w, q.y);
            float e = 1.0e-4f;
            return new Vector4(Mathf.Abs(q.z + (q.w - q.y) / (6.0f * d + e)), d / (q.x + e), q.x,c.a);
        }
        static float Frac(float v) => v - Mathf.Floor(v);
        static Color HsvToRgb(Vector4 c)
        {
            Vector4 K = new Vector4(1.0f, 2.0f / 3.0f, 1.0f / 3.0f, 3.0f);
            //float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
            Vector3 p = new Vector3(
                Mathf.Abs(Frac(c.x + K.x) * 6 - K.w),
                Mathf.Abs(Frac(c.x + K.y) * 6 - K.w),
                Mathf.Abs(Frac(c.x + K.z) * 6 - K.w));
            //return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            return new Color(
                c.z * Mathf.Lerp(K.x, Mathf.Clamp01(p.x - K.x), c.y),
                c.z * Mathf.Lerp(K.x, Mathf.Clamp01(p.y - K.x), c.y),
                c.z * Mathf.Lerp(K.x, Mathf.Clamp01(p.z - K.x), c.y),
                c.w);
        }
        static float Luminance(Color c)=>c.r*0.2126729f+ c.g* 0.7151522f+ c.b* 0.0721750f;
        static Color Vibrance(Color c,float sat)
        {
            float l = Luminance(c);
            return new Color(
                l + sat * (c.r - l),
                l + sat * (c.g - l),
                l + sat * (c.b - l),
                c.a);
        }
        static Color Contrast(Color c, float midpoint, float contrast)
        {
            return new Color(
                (c.r - midpoint) * contrast + midpoint,
                (c.g - midpoint) * contrast + midpoint,
                (c.b - midpoint) * contrast + midpoint,
                c.a);
        }

    }
}