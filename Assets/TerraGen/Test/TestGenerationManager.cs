using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using fzmnm.InfiniteGeneration;
namespace fzmnm.Test
{
    public class TestGenerationManager : MonoBehaviour
    {
        public Material terrainMaterial;
        public Vector2Int min, max;
        public int stride=1;
        [Button]
        public void Generate()
        {
            var mgr = GetComponent<GenerationManager>();
            mgr.Setup();
            mgr.RequestRange(min,max);
            mgr.GenerateAll();
        }
        [Button]
        public void GenerateMesh()
        {
            var mgr = GetComponent<GenerationManager>();
            Vector2Int resolution = DivFloor(max - min,stride);
            float unitLength = mgr.unitLength;
            var heightMap = new AreaMap<float>(min, max);
            var colorMap = new AreaMap<Color32>(min, max);
            //Debug.Log(map.buffer.Length);
            //Debug.Log(resolution);
            mgr.ReadMap("height", heightMap);
            mgr.ReadMap("color", colorMap);

            Mesh mesh;
            if (!GetComponent<MeshFilter>())
            {
                mesh = new Mesh();
                gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                gameObject.AddComponent<MeshRenderer>().sharedMaterial = new Material(terrainMaterial);
                gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            else
            {
                mesh = GetComponent<MeshFilter>().sharedMesh;
                GetComponent<MeshRenderer>().sharedMaterial= new Material(terrainMaterial);
            }
            mesh.Clear();
            mesh.vertices = GenerateVerticesArray(resolution, new Vector3(min.x,0,min.y) * unitLength, unitLength, heightMap.buffer,heightMap.yStride, stride);
            mesh.uv = GetUVArray(resolution);
            //mesh.colors32 = GenerateColorsArray(resolution, colorMap.buffer, colorMap.yStride, stride);
            var indices = GetIndiceArray(resolution);
            mesh.indexFormat = indices.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, calculateBounds: false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            Texture2D tex = new Texture2D(colorMap.size.x, colorMap.size.y,TextureFormat.RGB24,mipChain:true);
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 2;
            tex.SetPixels32(colorMap.buffer);
            tex.Apply();
            gameObject.GetComponent<MeshRenderer>().sharedMaterial.mainTexture = tex;
        }

        static Color32[] GenerateColorsArray(Vector2Int resolution, Color32[] colors, int yStride, int stride)
        {

            Vector2Int resolutionPlusOne = resolution + Vector2Int.one;
            Color32[] vertexColors = new Color32[resolutionPlusOne.x * resolutionPlusOne.y];

            for (int y = 0; y <= resolution.y; ++y)
                for (int x = 0; x <= resolution.x; ++x)
                    vertexColors[x + y * resolutionPlusOne.x] = colors[x * stride + y * stride * yStride];
            return vertexColors;
        }

        static Vector3[] GenerateVerticesArray(Vector2Int resolution, Vector3 bias, float unitLength, float[] heights,int yStride, int stride)
        {
            Vector2Int resolutionPlusOne = resolution + Vector2Int.one;
            Vector3[] vertices=new Vector3[resolutionPlusOne.x * resolutionPlusOne.y];

            for (int y = 0; y <= resolution.y; ++y)
                for (int x = 0; x <= resolution.x; ++x)
                {
                    //x right y forward
                    Vector3 pos = bias + new Vector3(
                        x * stride * unitLength,
                        heights[x*stride  + y *stride * yStride],
                        y * stride * unitLength
                        );
                    vertices[x + y * resolutionPlusOne.x] = pos;
                }
            return vertices;
        }
        static int[] GetIndiceArray(Vector2Int resolution)
        {
            Vector2Int resolutionPlusOne = resolution + Vector2Int.one;
            int[] indices= new int[resolution.x * resolution.y * 6];
            int p = 0;
            for (int y = 0; y < resolution.y; ++y)
                for (int x = 0; x < resolution.x; ++x)
                {
                    int corner = x + y * resolutionPlusOne.x;
                    if ((x + y) % 2 == 0)
                    {
                        indices[p++] = corner;
                        indices[p++] = corner + resolutionPlusOne.x;
                        indices[p++] = corner + 1;
                        indices[p++] = corner + 1 + resolutionPlusOne.x;
                        indices[p++] = corner + 1;
                        indices[p++] = corner + resolutionPlusOne.x;
                    }
                    else
                    {
                        indices[p++] = corner;
                        indices[p++] = corner + resolutionPlusOne.x;
                        indices[p++] = corner + 1 + resolutionPlusOne.x;
                        indices[p++] = corner;
                        indices[p++] = corner + 1 + resolutionPlusOne.x;
                        indices[p++] = corner + 1;
                    }
                }
            return indices;
        }
        static Vector2[] GetUVArray(Vector2Int resolution)
        {
            Vector2Int resolutionPlusOne = resolution + Vector2Int.one;
            Vector2[] uv = new Vector2[resolutionPlusOne.x * resolutionPlusOne.y];
            for (int y = 0; y <= resolution.y; ++y)
                for (int x = 0; x <= resolution.x; ++x)
                    uv[x + y * resolutionPlusOne.x] = new Vector2(x / (float)resolution.x, y / (float)resolution.y);
            return uv;
        }
        protected static Vector2Int DivFloor(Vector2Int a, int b) => new Vector2Int(DivFloor(a.x, b), DivFloor(a.y, b));
        protected static int DivFloor(int a, int b) { return a < 0 ? a / b - 1 : a / b; }//b>0, potential overflow

    }
}

