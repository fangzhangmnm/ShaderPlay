using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System;

//TODO align square and vertex

namespace fzmnm.InfiniteGeneration.Pass
{
    [CreateAssetMenu(menuName = "ChunkGenerationPass/Color")]
    public class ColorPass : GenerationPass
    {
        public int chunkSize = 64;
        public string inputHeightMapName = "height";
        public string outputColorMapName="color";

        [System.Serializable]
        public struct HeightColorSetting
        {
            public float maxHeight;
            public Color32 color;
        }
        public HeightColorSetting[] layers;

        struct MyJob : IJobParallelFor, IDisposable
        {
            public int x0, y0, chunkSize;
            [ReadOnly] public NativeArray<float> heights;
            [ReadOnly] public NativeArray<HeightColorSetting> layers;
            public NativeArray<Color32> colors;

            public MyJob(ColorPass desc, GenerationManager target, Vector2Int min)
            {
                x0 = min.x; y0 = min.y;
                chunkSize = desc.chunkSize;
                layers = new NativeArray<HeightColorSetting>(desc.layers, Allocator.Persistent);
                heights = new NativeArray<float>(chunkSize * chunkSize, Allocator.Persistent);
                colors = new NativeArray<Color32>(chunkSize * chunkSize, Allocator.Persistent);
            }

            public void Execute(int index)
            {
                float height = heights[index];
                int i = 0;
                while (height > layers[i].maxHeight && i < layers.Length - 1) ++i;
                colors[index] = layers[i].color;
            }
            public void Dispose()
            {
                if (heights.IsCreated) heights.Dispose();
                if (colors.IsCreated) colors.Dispose();
                if (layers.IsCreated) layers.Dispose();
            }
        }
        public override int GetChunkSize() => chunkSize;
        public override void Setup(GenerationManager target, int passID)
        {
            target.RegisterMapInput(passID, inputHeightMapName, 0);
            target.RegisterMapOutput<Color32>(passID, outputColorMapName, chunkSize);
        }
        public override IEnumerator GenerateChunk(GenerationManager target, Vector2Int chunkID)
        {
            (Vector2Int min, Vector2Int max) = ChunkIDToMinMax(chunkID);

            MyJob job = new MyJob(this, target, min);
            heightBuffer.Reset(min, max); 
            target.ReadMap(inputHeightMapName, heightBuffer);
            job.heights.CopyFrom(heightBuffer.buffer);

            JobHandle jobHandle = job.Schedule(chunkSize * chunkSize, 1);
            yield return new WaitUntil(() => jobHandle.IsCompleted);

            jobHandle.Complete();//this is needed
            colorBuffer.Reset(min, max);
            job.colors.CopyTo(colorBuffer.buffer);
            job.Dispose();

            target.WriteMap(outputColorMapName, colorBuffer);
        }

        [System.NonSerialized]
        AreaMap<float> heightBuffer = new AreaMap<float>();
        [System.NonSerialized]
        AreaMap<Color32> colorBuffer = new AreaMap<Color32>();
    }
}
