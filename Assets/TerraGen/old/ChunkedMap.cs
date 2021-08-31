using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;


namespace fzmnm.InfiniteGeneration
{
    public class ChunkedMapBase
    {
        public ChunkedMapBase(int chunkSize)
        {
            this.chunkSize = chunkSize;
        }
        public readonly int chunkSize;
        public (Vector2Int, Vector2Int) ToChunkBoundary(Vector2Int min, Vector2Int max)
            => (DivFloor(min, chunkSize) * chunkSize, DivCeil(max, chunkSize) * chunkSize - Vector2Int.one);
        protected static Vector2Int DivFloor(Vector2Int a, int b) => new Vector2Int(DivFloor(a.x, b), DivFloor(a.y, b));
        protected static Vector2Int DivCeil(Vector2Int a, int b) => new Vector2Int(DivCeil(a.x, b), DivCeil(a.y, b));
        protected static int DivFloor(int a, int b) { return a < 0 ? a / b - 1 : a / b; }//b>0, potential overflow
        protected static int DivCeil(int a, int b) { return DivFloor(a + b - 1, b); }//b>0, potential overflow
    }


    public class ChunkedMap<T> : ChunkedMapBase
    {
        public ChunkedMap(int chunkSize) : base(chunkSize) { }
        Dictionary<Vector2Int, T[]> chunks = new Dictionary<Vector2Int, T[]>();
        public void Read(AreaMap<T> area)
        {
            Vector2Int minc = DivFloor(area.min, chunkSize);
            Vector2Int maxc = DivFloor(area.max, chunkSize);
            Vector2Int coordc = new Vector2Int();
            for (coordc.y = minc.y; coordc.y <= maxc.y; ++coordc.y)
                for (coordc.x = minc.x; coordc.x <= maxc.x; ++coordc.x)
                {
                    if (!chunks.TryGetValue(coordc, out T[] chunk))
                        continue;
                    Vector2Int min1 = Vector2Int.Max(Vector2Int.zero, area.min - coordc * chunkSize);
                    Vector2Int max1 = Vector2Int.Min(Vector2Int.one * (chunkSize - 1), area.max - coordc * chunkSize);
                    Vector2Int coord1 = new Vector2Int();
                    for (coord1.x = min1.x; coord1.x <= max1.x; ++coord1.x)
                        for (coord1.y = min1.y; coord1.y <= max1.y; ++coord1.y)
                        {
                            Vector2Int coord2 = coordc * chunkSize + coord1 - area.min;
                            area.buffer[coord2.x + coord2.y * area.size.x] = chunk[coord1.x + coord1.y * chunkSize];
                        }
                }
        }
        public void Write(AreaMap<T> area)
        {
            Vector2Int minc = DivFloor(area.min, chunkSize);
            Vector2Int maxc = DivFloor(area.max, chunkSize);
            Vector2Int coordc = new Vector2Int();
            for (coordc.y = minc.y; coordc.y <= maxc.y; ++coordc.y)
                for (coordc.x = minc.x; coordc.x <= maxc.x; ++coordc.x)
                {
                    if (!chunks.TryGetValue(coordc, out T[] chunk))
                        chunk = chunks[coordc] = new T[chunkSize * chunkSize];
                    Vector2Int min1 = Vector2Int.Max(Vector2Int.zero, area.min - coordc * chunkSize);
                    Vector2Int max1 = Vector2Int.Min(Vector2Int.one * (chunkSize - 1), area.max - coordc * chunkSize);
                    Vector2Int coord1 = new Vector2Int();
                    for (coord1.x = min1.x; coord1.x <= max1.x; ++coord1.x)
                        for (coord1.y = min1.y; coord1.y <= max1.y; ++coord1.y)
                        {
                            Vector2Int coord2 = coordc * chunkSize + coord1 - area.min;
                            chunk[coord1.x + coord1.y * chunkSize] = area.buffer[coord2.x + coord2.y * area.size.x];
                        }
                }
        }
    }
    public class AreaMap<T>
    {
        public Vector2Int min { get; private set; }
        public Vector2Int max { get; private set; }
        public Vector2Int size { get; private set; }
        public T[] buffer { get; private set; }
        public int yStride { get; private set; }
        public AreaMap(Vector2Int min, Vector2Int max)
        {
            this.min = min;this.max = max;this.size = max - min + Vector2Int.one;this.yStride = size.x;
            this.buffer = new T[size.x * size.y];
        }
        public void Reset(Vector2Int newMin, Vector2Int newMax)
        {
            Vector2Int newSize = newMax - newMin + Vector2Int.one;
            if (newSize != size)
                this.buffer = new T[newSize.x * newSize.y];
            min = newMin;max = newMax;size = newSize;
        }
        public T this[int x, int y]
        {
            get
            {
                return buffer[x-min.x + (y-min.y) * size.x];
            }
            set
            {
                buffer[x - min.x + (y - min.y) * size.x] = value;
            }
        }
        public T this[Vector2Int coord]=>this[coord.x,coord.y];
    }
    public class ChunkedEntityMap<T> : ChunkedMapBase
    {
        public ChunkedEntityMap(int chunkSize) : base(chunkSize) { }
        Dictionary<Vector2Int, List<T>> chunks = new Dictionary<Vector2Int, List<T>>();
        public void AddToChunkUnchecked(Vector2Int chunkID, IEnumerable<T> entities)
        {
            if (!chunks.TryGetValue(chunkID, out List<T> chunk))
                chunk = chunks[chunkID] = new List<T>();
            chunk.AddRange(entities);
        }
        public void Add(Vector2Int coord, T entity)
        {
            Vector2Int coordc = new Vector2Int(DivFloor(coord.x, chunkSize), DivFloor(coord.y, chunkSize));
            if (!chunks.TryGetValue(coordc, out List<T> chunk))
                chunk = chunks[coordc] = new List<T>();
            chunk.Add(entity);
        }
        public void Remove(Vector2Int coord, T entity)
        {
            Vector2Int coordc = new Vector2Int(DivFloor(coord.x, chunkSize), DivFloor(coord.y, chunkSize));
            if (!chunks.TryGetValue(coordc, out List<T> chunk))
                return;
            chunk.Remove(entity);
        }
        public IEnumerable<T> Read(Vector2Int min, Vector2Int max)
        {
            Vector2Int minc = DivFloor(min, chunkSize);
            Vector2Int maxc = DivFloor(max, chunkSize);
            Vector2Int coordc = new Vector2Int();
            for (coordc.y = minc.y; coordc.y <= maxc.y; ++coordc.y)
                for (coordc.x = minc.x; coordc.x <= maxc.x; ++coordc.x)
                {
                    if (!chunks.TryGetValue(coordc, out List<T> chunk))
                        continue;
                    foreach (T entity in chunk)
                        yield return entity;
                }
        }
    }
}