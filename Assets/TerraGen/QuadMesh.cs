using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Collections;
using System.Threading.Tasks;
using System;


public class QuadMesh : MonoBehaviour
{
    struct Job
    {
        NativeArray<Vector2Int> vertices;
        NativeArray<int> indices;
        const int logLeafSize = 1;
        int nVertices;
        int nIndices;
        void DividePatch(Vector2Int mid, int logSize,
            bool divL, bool divR, bool divD, bool divU)
        {
            int halfSize = 1 << (logSize - 1);
            Vector2Int posLD = min;
            Vector2Int posRD = min + new Vector2Int(halfSize, 0);
            Vector2Int posLU = min + new Vector2Int(0, halfSize);
            Vector2Int posRU = min + new Vector2Int(halfSize, halfSize);
            bool divLD = divL && divD && ShouldDivide(posLD, logSize - 1);
            bool divRD = divR && divD && ShouldDivide(posRD, logSize - 1);
            bool divLU = divL && divU && ShouldDivide(posLU, logSize - 1);
            bool divRU = divR && divU && ShouldDivide(posRU, logSize - 1);
            if (divLD) DividePatch(posLD, logSize - 1, true, divRD, true, divLU);
            else GenerateLeaf(posLD, logSize - 1, true, divRD, true, divLU);
            if (divRD) DividePatch(posRD, logSize - 1, divLD, true, true, divRU);
            else GenerateLeaf(posRD, logSize - 1, divLD, true, true, divRU);
            if (divLU) DividePatch(posLU, logSize - 1, true, divRU, divLD, true);
            else GenerateLeaf(posRD, logSize - 1, divLD, true, true, divRU);
            if (divRU) DividePatch(posRU, logSize - 1, divLU, true, divRD, true);
            else GenerateLeaf(posRU, logSize - 1, divLU, true, divRD, true);
        }
        void GenerateLeaf(Vector2Int min, int logSize,
            bool divL, bool divR, bool divD, bool divU)
        {
            int halfSize = 1 << (logSize - 1);
            int m = nVertices; vertices[nVertices++] = min + new Vector2Int(halfSize, halfSize);
            int ld = nVertices; vertices[nVertices++] = min + new Vector2Int(0, 0);
            int rd = nVertices; vertices[nVertices++] = min + new Vector2Int(halfSize * 2, 0);
            int lu = nVertices; vertices[nVertices++] = min + new Vector2Int(0, halfSize * 2);
            int ru = nVertices; vertices[nVertices++] = min + new Vector2Int(halfSize * 2, halfSize * 2);

            indices[nIndices++] = m;
            indices[nIndices++] = ld;
            if (divL)
            {
                indices[nIndices++] = nVertices;
                indices[nIndices++] = m;
                indices[nIndices++] = nVertices;
                vertices[nVertices++] = min + new Vector2Int(0, halfSize);
            }
            indices[nIndices++] = lu;

            indices[nIndices++] = m;
            indices[nIndices++] = lu;
            if (divL)
            {
                indices[nIndices++] = nVertices;
                indices[nIndices++] = m;
                indices[nIndices++] = nVertices;
                vertices[nVertices++] = min + new Vector2Int(halfSize, halfSize * 2);
            }
            indices[nIndices++] = ru;

            indices[nIndices++] = m;
            indices[nIndices++] = ru;
            if (divL)
            {
                indices[nIndices++] = nVertices;
                indices[nIndices++] = m;
                indices[nIndices++] = nVertices;
                vertices[nVertices++] = min + new Vector2Int(halfSize * 2, halfSize);
            }
            indices[nIndices++] = rd;

            indices[nIndices++] = m;
            indices[nIndices++] = rd;
            if (divL)
            {
                indices[nIndices++] = nVertices;
                indices[nIndices++] = m;
                indices[nIndices++] = nVertices;
                vertices[nVertices++] = min + new Vector2Int(halfSize, 0);
            }
            indices[nIndices++] = ld;
        }
        bool ShouldDivide(Vector2Int min, int logSize)
        {
            int lod = logSize - logLeafSize;//lod0 size=1<<logLeafSize
            if (lod <= 0) return false;
            Vector2Int mid = min + Vector2Int.one * (1 << (logSize - 1));
            return Vector2Int.Distance(mid, cameraPos) < maxDistForLod[lod - 1];
        }
        Vector2Int cameraPos,min;
        int logSize;
        NativeArray<float> maxDistForLod;
        void Execute()
        {
            DividePatch(min, logSize, true, true, true, true);
        }


    }
}
