using UnityEngine;
using UnityEditor;
public class ScreenshotTools:EditorWindow
{
    [MenuItem("Window/Screenshot Tools")]
    static void Init()
    {
        EditorWindow.GetWindow(typeof(ScreenshotTools)).Show();
    }
    private void OnGUI()
    {
        if (GUILayout.Button("Take Screenshot from Game View")) TakeScreenshot();

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        cubemapRes = EditorGUILayout.IntField("Cubemap Resolution",cubemapRes);
        equirectangularWidth = EditorGUILayout.IntField("Equirectangular Width", equirectangularWidth);
        equirectangularHeight = EditorGUILayout.IntField("Equirectangular Height", equirectangularHeight);
        cubemapCamera = (Camera)EditorGUILayout.ObjectField("Cubemap Camera",cubemapCamera,typeof(Camera),true);
        if (GUILayout.Button("Take Cubemap PNG")) TakeCubemapPNG();
        if (GUILayout.Button("Take Cubemap EXR")) TakeCubemapEXR();
    }
    int cubemapRes = 1024;
    int equirectangularWidth = 2048;
    int equirectangularHeight = 1024;
    Camera cubemapCamera;
    void TakeScreenshot()
    {
        string filename = EditorUtility.SaveFilePanelInProject("Save Screenshot", "Screenshot", "png", "Save Screenshot");
        if (filename.Length == 0) return;
        ScreenCapture.CaptureScreenshot(filename);
        AssetDatabase.ImportAsset(filename);
    }
    void TakeCubemapPNG()
    {
        string filename = EditorUtility.SaveFilePanelInProject("Save Equirectangular", "Equirectangular", "png", "Save Equirectangular");
        if (filename.Length == 0) return;

        Texture2D tx = TakeCubemap(TextureFormat.RGB24);

        System.IO.File.WriteAllBytes(filename, tx.EncodeToPNG());
        AssetDatabase.ImportAsset(filename);
    }
    void TakeCubemapEXR()
    {
        string filename = EditorUtility.SaveFilePanelInProject("Save Equirectangular", "Equirectangular", "exr", "Save Equirectangular");
        if (filename.Length == 0) return;

        Texture2D tx = TakeCubemap(TextureFormat.RGBAFloat);

        System.IO.File.WriteAllBytes(filename, tx.EncodeToEXR());
        AssetDatabase.ImportAsset(filename);
    }
    Texture2D TakeCubemap(TextureFormat format)
    {

        int depthBit = 32;

        RenderTexture cm = new RenderTexture(cubemapRes, cubemapRes, depthBit);
        cm.dimension = UnityEngine.Rendering.TextureDimension.Cube;
        cubemapCamera.RenderToCubemap(cm);

        RenderTexture eq = new RenderTexture(equirectangularWidth, equirectangularHeight, depthBit);
        cm.ConvertToEquirect(eq);

        RenderTexture.active = eq;
        Texture2D tx = new Texture2D(eq.width, eq.height, format, false);
        tx.ReadPixels(new Rect(0, 0, eq.width, eq.height), 0, 0);
        RenderTexture.active = null;

        return tx;
    }
}
