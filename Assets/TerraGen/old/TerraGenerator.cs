using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace fzmnm
{
    public class TerraGenerator : MonoBehaviour
    {
        public int resolution = 256;
        public float size = 512;
        public int seed = 0;
        public float heightScale = 128;
        public float scale = 512;
        public float distortion = .2f;
        public AnimationCurve curve;

        Dictionary<Vector2Int, TerraChunk> chunks=new Dictionary<Vector2Int, TerraChunk>();
        public Material terrainMaterial;

        public Vector2Int debug_coord;
        public int debug_lod;
        [Button]
        public void Debug_Generate()
        {
            if(chunks.Count==0)//Reset in editor
                for (int i = transform.childCount - 1; i >= 0; --i)
                    DestroyImmediate(transform.GetChild(i).gameObject);
            var chunk=CreateChunk(debug_coord);
            chunk.UpdateVisibility(true, debug_lod);
        }
        [Button]
        public void Debug_Clear()
        {
            chunks.Clear();
            for (int i = transform.childCount - 1; i >= 0; --i)
                DestroyImmediate(transform.GetChild(i).gameObject);
        }

        void RemoveChunk(TerraChunk chunk)
        {
            if (chunk == null) return;
            if(chunk.instance.gameObject!=null)
                DestroyImmediate(chunk.instance.gameObject);
            chunks.Remove(chunk.coords);
        }
        TerraChunk CreateChunk(Vector2Int coords)
        {
            if (chunks.ContainsKey(coords))
                RemoveChunk(chunks[coords]);
            TerraChunk chunk = new TerraChunk(this,coords);
            chunks[coords] = chunk;

            chunk.heightField = new float[resolution + 1, resolution + 1];
            for (int x = 0; x < resolution + 1; ++x)
                for (int y = 0; y < resolution + 1; ++y)
                {
                    Vector2 pos = new Vector2((coords.x + (float)x / resolution) * size, (coords.y + (float)y / resolution) * size);
                    Vector2 posDistortion = Vector2.zero;
                    posDistortion.x = Noise.Octave2D(pos.x / scale+245, pos.y / scale, 4) * distortion*scale;
                    posDistortion.y = Noise.Octave2D(pos.x / scale+527, pos.y / scale, 4) * distortion * scale;
                    pos += posDistortion;
                    float value= Noise.Octave2D(pos.x / scale, pos.y / scale, 7);
                    value = curve.Evaluate(value);
                    value = value * heightScale;
                    chunk.heightField[x, y] = value;
                }
            return chunk;
        }

    }
    public class TerraChunk
    {
        public TerraChunk(TerraGenerator container, Vector2Int coords) { this.container = container;this.coords = coords; }
        public int lod = -1;
        public TerraGenerator container;
        public Vector2Int coords;
        public float[,] heightField = null;
        public Color[,] tint=null;
        public GameObject instance=null;
        public int maxResolution => container.resolution;
        public float size => container.size;

        public void UpdateVisibility(bool visible, int lod)
        {
            if (!visible && instance != null)
            {
                this.lod = -1;
                Object.Destroy(instance);
                return;
            }
            if(visible)
            {
                if (instance == null)
                {
                    this.lod = -1;
                    instance = new GameObject($"Chunk({coords.x},{coords.y})");
                    instance.transform.SetParent(container.transform);
                    instance.AddComponent<MeshFilter>();
                    var meshRenderer = instance.AddComponent<MeshRenderer>();
                    meshRenderer.sharedMaterial = new Material(container.terrainMaterial);
                    instance.AddComponent<MeshCollider>();
                }
            }
            if (visible && this.lod != lod)
            {
                this.lod = lod;
                Mesh mesh = GenerateMesh(lod);
                instance.GetComponent<MeshFilter>().sharedMesh = mesh;
                instance.GetComponent<MeshCollider>().sharedMesh = mesh;
            }
        }
        private Mesh GenerateMesh(int lod)
        {
            int resolution = Mathf.Max(1, container.resolution>>lod);

            Mesh mesh = new Mesh();
            mesh.vertices = GenerateVerticesArray(resolution);
            mesh.uv = GetUVArray(resolution);
            var indices = GetIndiceArray(resolution);
            mesh.indexFormat = indices.Length > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, calculateBounds: false);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
        static Dictionary<int, Vector3[]> cachedVerticesArrays = new Dictionary<int, Vector3[]>();
        Vector3[] GenerateVerticesArray(int resolution)
        {
            int resolutionPlusOne = resolution + 1;
            int stride = container.resolution / resolution;

            Vector3[] vertices;
            if (cachedVerticesArrays.ContainsKey(resolution))
                vertices = cachedVerticesArrays[resolution];
            else
                cachedVerticesArrays[resolution] = vertices = new Vector3[resolutionPlusOne * resolutionPlusOne];

            for (int x = 0; x < resolutionPlusOne; ++x)
                for (int y = 0; y < resolutionPlusOne; ++y)
                {
                    //x right y forward
                    Vector3 pos = new Vector3(
                        (coords.x + (float)x / resolution) * size,
                        heightField[x * stride, y * stride],
                        (coords.y + (float)y / resolution) * size);
                    vertices[x * resolutionPlusOne + y] = pos;
                }
            return vertices;
        }
        static Dictionary<int, int[]> cachedIndicesArrays = new Dictionary<int, int[]>();
        static int[] GetIndiceArray(int resolution)
        {
            int resolutionPlusOne = resolution + 1;
            int[] indices;
            if (cachedIndicesArrays.ContainsKey(resolution))
                return cachedIndicesArrays[resolution];
            else 
                cachedIndicesArrays[resolution]=indices = new int[resolution * resolution  * 6];
            int p = 0;
            for (int x = 0; x < resolution; ++x)
                for (int y = 0; y < resolution; ++y)
                {
                    int corner = x * resolutionPlusOne + y;
                    indices[p++] = corner;
                    indices[p++] = corner + 1;
                    indices[p++] = corner + resolutionPlusOne;
                    indices[p++] = corner + 1 + resolutionPlusOne;
                    indices[p++] = corner + resolutionPlusOne;
                    indices[p++] = corner + 1;
                }
            return indices;
        }
        static Dictionary<int, Vector2[]> cachedUVArrays = new Dictionary<int, Vector2[]>();
        static Vector2[] GetUVArray(int resolution)
        {
            int resolutionPlusOne = resolution + 1;
            Vector2[] uv;
            if (cachedUVArrays.ContainsKey(resolution))
                return cachedUVArrays[resolution];
            else
                cachedUVArrays[resolution] = uv = new Vector2[resolutionPlusOne * resolutionPlusOne];
            for (int x = 0; x < resolution + 1; ++x)
                for (int y = 0; y < resolution + 1; ++y)
                    uv[x * resolutionPlusOne + y] = new Vector2((float)x / resolution, (float)y / resolution);
            cachedUVArrays[resolution] = uv;
            return uv;
        }
    }
}
