using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Threading.Tasks;

namespace fzmnm.InfiniteGeneration
{
    public abstract class GenerationPass : ScriptableObject
    {
        public abstract int GetChunkSize();
        public abstract void Setup(GenerationManager target, int passID);
        public abstract IEnumerator GenerateChunk(GenerationManager target, Vector2Int chunkID);
        public (Vector2Int, Vector2Int) ChunkIDToMinMax(Vector2Int chunkID) => (chunkID * GetChunkSize(), chunkID * GetChunkSize() + Vector2Int.one * (GetChunkSize() - 1));

    }
}

