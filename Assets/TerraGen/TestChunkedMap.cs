using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using fzmnm.InfiniteGeneration;
namespace fzmnm.Test
{
    public class TestChunkedMap : MonoBehaviour
    {
        [Button]
        public void Test()
        {
            ChunkedMap<float> map = new ChunkedMap<float>(4);
            var area = new AreaMap<float>(Vector2Int.zero, Vector2Int.one * 7);
            for (int y = 0; y < 8; ++y)
                for (int x = 0; x < 8; ++x)
                    area[x, y] = y * 10 + x;
            map.Write(area);
            var area1 = new AreaMap<float>(Vector2Int.one * 2, Vector2Int.one * 6);
            map.Read(area1);
            debug = "";
            for (int y = 2; y <= 6; ++y)
            {
                string s = "";
                for (int x = 2; x <= 6; ++x)
                    s += $"{area1[x, y]} ";
                debug = s + "\n" + debug;
            }
            ChunkedEntityMap<Vector2Int> map2 = new ChunkedEntityMap<Vector2Int>(4);
            for (int y = 0; y < 8; ++y)
                for (int x = 0; x < 8; ++x)
                    map2.Add(new Vector2Int(x, y), new Vector2Int(x, y));
            foreach (var e in map2.Read(Vector2Int.zero, Vector2Int.one * 3))
                debug = debug + $"{e}\n";

        }
        [Multiline(10)]
        public string debug;
    }

}
