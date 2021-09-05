using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Threading.Tasks;

namespace fzmnm.InfiniteGeneration.Pass
{
    [CreateAssetMenu(menuName = "ChunkGenerationPass/Perlin")]
    public class PerlinPass : GenerationPass
    {

        public int seed = 0;
        public int chunkSize = 64;
        public float heightScale = 128;
        public float landScale = 512;
        public float distortion = .2f;
        public AnimationCurve heightCurve;
        public string outputHeightMapName = "height";

        struct MyJob : IJobParallelFor
        {
            public NativeArray<float> results;
            public float heightScale, landScale, distortion, unitLength;
            public int x0, y0, chunkSize, seed;
            public NativeCurve heightCurve;

            public void Execute(int index)
            {
                Vector2 pos = new Vector2(index % chunkSize + x0, index / chunkSize + y0)*unitLength;
                Vector2 posDistortion = new Vector2Int();
                posDistortion.x = Noise.Octave2D(pos.x / landScale + 245, pos.y / landScale + seed, 4) * landScale * distortion;
                posDistortion.y = Noise.Octave2D(pos.x / landScale + 527, pos.y / landScale + seed, 4) * landScale * distortion;
                pos += posDistortion;
                float value = Noise.Octave2D(pos.x / landScale, pos.y / landScale + seed, 7);
                value = heightCurve.Evaluate(value);
                value *= heightScale;
                results[index] = value;
            }
            public void Dispose()
            {
                if (results.IsCreated) results.Dispose();
                if (heightCurve.IsCreated) heightCurve.Dispose();
            }
        }


        public override int GetChunkSize() => chunkSize;
        public override void Setup(GenerationManager target, int passID)
        {
            target.RegisterMapOutput<float>(passID, outputHeightMapName, chunkSize);
            mapBuffer = new AreaMap<float>(Vector2Int.zero, Vector2Int.one * (chunkSize - 1));
        }
        MyJob job;
        public override IEnumerator GenerateChunk(GenerationManager target, Vector2Int chunkID)
        {
            Debug.Log($"{GetType().Name}: {chunkID} Start");
            (Vector2Int min, Vector2Int max) = ChunkIDToMinMax(chunkID);

            job = new MyJob()
            {
                results = new NativeArray<float>(chunkSize * chunkSize, Allocator.Persistent),
                heightScale = heightScale,
                landScale = landScale,
                distortion = distortion,
                unitLength = target.unitLength,
                x0 = min.x,
                y0 = min.y,
                chunkSize = chunkSize,
                seed = this.seed + target.seed,
                heightCurve = new NativeCurve()
            };

            job.heightCurve.Update(heightCurve, 256);
            JobHandle jobHandle = job.Schedule(chunkSize * chunkSize, 1);
            yield return new WaitUntil(() => jobHandle.IsCompleted);
            jobHandle.Complete();//this is needed
            job.heightCurve.Dispose();
            mapBuffer.Reset(min, max);
            job.results.CopyTo(mapBuffer.buffer);
            job.results.Dispose();

            target.WriteMap(outputHeightMapName, mapBuffer);
            Debug.Log($"{GetType().Name}: {chunkID} Done");
        }
        private void OnEnable()
        {
            //Cleanup native allocated memories on script reloading
            job.Dispose();
        }

        [System.NonSerialized]
        AreaMap<float> mapBuffer;
    }
}
