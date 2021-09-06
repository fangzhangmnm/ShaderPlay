using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace fzmnm.InfiniteGeneration
{
    public class GenerationManager : MonoBehaviour
    {
        public int seed = 0;
        public float unitLength = 2;
        public GenerationPass[] generationPasses;

        Pass[] passes;

        public void Setup()
        {
            passes = new Pass[generationPasses.Length];
            for (int i = 0; i < generationPasses.Length; ++i)
            {
                passes[i] = new Pass(generationPasses[i]);
                passes[i].generator.Setup(this,i);
            }
        }
        public void RequestRange(int minX, int minY, int maxX, int maxY) => RequestRange(new Vector2Int(minX, minY), new Vector2Int(maxX, maxY));
        public void RequestRange(Vector2Int min, Vector2Int max)
        {
            if (passes == null)
                Debug.LogAssertion("Please call Setup() before Generate()");
            if (passes.Length == 0)
                Debug.LogAssertion("There is no GenerationPass");
            passes[passes.Length - 1].RequestRange(min, max);
        }
        public void GenerateAll()
        {
            IEnumerator it = GenerateLoop();
            while (it.MoveNext()) {  }
        }
        public IEnumerator GenerateLoop()
        {
            if (passes == null)
                Debug.LogAssertion("Please call Setup() before Generate()");
            PropogatePassRequests();
            foreach(var pass in passes)
            {
                foreach(Vector2Int chunkID in pass.requestedChunks)
                {
                    Debug.Log($"Generating {pass.generator.GetType().Name}:({chunkID})");
                    yield return StartCoroutine(pass.generator.GenerateChunk(this, chunkID));
                    pass.generatedChunks.Add(chunkID);
                }
                pass.requestedChunks.Clear();
            }
            Debug.Log($"Generation Done");
        }

        void PropogatePassRequests()
        {
            for(int i=passes.Length-1;i>=0;--i)
            {
                var pass = passes[i];
                foreach(Vector2Int chunkID in pass.requestedChunks)
                {
                    (Vector2Int min, Vector2Int max) = pass.generator.ChunkIDToMinMax(chunkID);
                    foreach ((string mapName, Vector2Int boundaryMin, Vector2Int boundaryMax) in pass.inputMaps)
                    {
                        for(int j=i-1;j>=0;--j)
                            if(passes[j].outputMaps.Contains(mapName))
                                passes[j].RequestRange(min - boundaryMin, max + boundaryMax);
                    }
                }
            }
        }


        Dictionary<string, ChunkedMapBase> maps = new Dictionary<string, ChunkedMapBase>();

        public void RegisterMapOutput<T>(int passID, string mapName,int chunkSize) where T:struct
        {
            if (!maps.ContainsKey(mapName))
                maps[mapName] = new ChunkedMap<T>(chunkSize: chunkSize);
            passes[passID].outputMaps.Add(mapName);
        }
        public void RegisterEntityMapOutput<T>(int passID, string mapName, int chunkSize)
        {
            if (!maps.ContainsKey(mapName))
                maps[mapName] = new ChunkedEntityMap<T>(chunkSize: chunkSize);
            passes[passID].outputMaps.Add(mapName);
        }

        public void RegisterMapInput(int passID, string mapName, int boundary) 
            => RegisterMapInput(passID, mapName, Vector2Int.one * boundary, Vector2Int.one * boundary);
        public void RegisterMapInput(int passID, string mapName, Vector2Int boundaryMin, Vector2Int boundaryMax)
        {
            passes[passID].inputMaps.Add((mapName, boundaryMin,boundaryMax));
        }
        public void WriteMap<T>(string mapName, AreaMap<T> buffer) where T : struct
        {
            if (maps.TryGetValue(mapName, out ChunkedMapBase mapBase))
                if(mapBase is ChunkedMap<T> map)
                    map.Write(buffer);
        }
        public void ReadMap<T>(string mapName, AreaMap<T> buffer) where T : struct
        {
            if (maps.TryGetValue(mapName, out ChunkedMapBase mapBase))
                if (mapBase is ChunkedMap<T> map)
                    map.Read(buffer);
        }
        public void AddEntity<T>(string mapName,Vector2Int coord, T payload)
        {
            if (maps.TryGetValue(mapName, out ChunkedMapBase mapBase))
                if (mapBase is ChunkedEntityMap<T> map)
                    map.Add(coord, payload);
        }
        public IEnumerable<ChunkedEntityMap<T>.Entity> Read<T>(string mapName, Vector2Int min, Vector2Int max)
        {
            if (maps.TryGetValue(mapName, out ChunkedMapBase mapBase))
                if (mapBase is ChunkedEntityMap<T> map)
                    return map.Read(min,max);
            return null;
        }

        Dictionary<string, object> globalDict=new Dictionary<string, object>();

        public void GlobalDictAdd(string key, object value)
        {
            globalDict[key] = value;
        }
        public T GlobalDictQuery<T>(string key)
        {
            if (globalDict.TryGetValue(key, out object value))
                return (T)value;
            else
                return default;
        }
        public void GlobalDictRemove(string key)
        {
            globalDict.Remove(key);
        }

        private class Pass
        {
            public Pass(GenerationPass generator) { this.generator = generator;}
            public GenerationPass generator;
            public int chunkSize => generator.GetChunkSize();
            public HashSet<Vector2Int> generatedChunks = new HashSet<Vector2Int>();
            public HashSet<Vector2Int> requestedChunks = new HashSet<Vector2Int>();
            public HashSet<string> outputMaps=new HashSet<string>();
            public List<(string, Vector2Int, Vector2Int)> inputMaps=new List<(string, Vector2Int, Vector2Int)>();
            public void RequestRange(Vector2Int min, Vector2Int max)
            {
                Vector2Int minc = DivFloor(min, chunkSize);
                Vector2Int maxc = DivFloor(max, chunkSize);
                Vector2Int coordc = new Vector2Int();
                for (coordc.y = minc.y; coordc.y <= maxc.y; ++coordc.y)
                    for (coordc.x = minc.x; coordc.x <= maxc.x; ++coordc.x)
                        if(!generatedChunks.Contains(coordc))
                            requestedChunks.Add(coordc);
            }
            protected static Vector2Int DivFloor(Vector2Int a, int b) => new Vector2Int(DivFloor(a.x, b), DivFloor(a.y, b));
            protected static int DivFloor(int a, int b) { return a < 0 ? a / b - 1 : a / b; }//b>0, potential overflow
        }
    }
}
