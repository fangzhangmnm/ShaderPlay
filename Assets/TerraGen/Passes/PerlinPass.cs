using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System;

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

        struct MyJob : IJobParallelFor, IDisposable
        {
            public float heightScale, landScale, distortion, unitLength;
            public int x0, y0, chunkSize, seed;
            public NativeCurve heightCurve;
            public NativeArray<float> results;
            public MyJob(PerlinPass desc, GenerationManager target, Vector2Int min)
            {
                heightScale = desc.heightScale;
                landScale = desc.landScale;
                distortion = desc.distortion;
                unitLength = target.unitLength;
                x0 = min.x;y0 = min.y;
                chunkSize = desc.chunkSize;
                seed = target.seed + desc.seed;
                heightCurve = new NativeCurve();
                heightCurve.Update(desc.heightCurve,256);
                results = new NativeArray<float>(chunkSize * chunkSize, Allocator.Persistent);
            }

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
        }
        public override IEnumerator GenerateChunk(GenerationManager target, Vector2Int chunkID)
        {
            (Vector2Int min, Vector2Int max) = ChunkIDToMinMax(chunkID);

            MyJob job = new MyJob(this, target, min);

            JobHandle jobHandle = job.Schedule(chunkSize * chunkSize, 1);
            yield return new WaitUntil(() => jobHandle.IsCompleted);

            jobHandle.Complete();//this is needed
            mapBuffer.Reset(min, max);
            job.results.CopyTo(mapBuffer.buffer);
            job.Dispose();

            target.WriteMap(outputHeightMapName, mapBuffer);
        }

        [System.NonSerialized]
        AreaMap<float> mapBuffer = new AreaMap<float>();
    }
}
