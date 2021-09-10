using UnityEngine;
namespace fzmnm
{
    public static partial class Noise
    {
        public static float Voronoi2D(float x, float y, int repeatX = 256, int repeatY = 256, byte seed = 0)
        {
            int xi = Mathf.FloorToInt(x); float xf = x - xi; xi = PositiveMod(xi, repeatX);
            int yi = Mathf.FloorToInt(y); float yf = y - yi; yi = PositiveMod(yi, repeatY);
            float distSqr = float.MaxValue;
            for (int dx = -1; dx <= 1; ++dx)
                for (int dy = -1; dy <= 1; ++dy)
                {
                    int xx = PositiveMod(xi + dx, repeatX);
                    int yy = PositiveMod(yi + dy, repeatY);
                    float ex = dx + p[p[p[xx] + yy] + seed] / 256f - xf;
                    float ey = dy + p[p[p[xx] + yy] + (seed+4)&0xff] / 256f - yf;
                    distSqr = Mathf.Min(distSqr, ex * ex + ey * ey);
                }
            return Mathf.Sqrt(distSqr);
        }
        public static float Voronoi3D(float x, float y, float z, int repeatX = 256, int repeatY = 256, int repeatZ = 256, byte seed = 0)
        {
            int xi = Mathf.FloorToInt(x); float xf = x - xi; xi = PositiveMod(xi, repeatX);
            int yi = Mathf.FloorToInt(y); float yf = y - yi; yi = PositiveMod(yi, repeatY);
            int zi = Mathf.FloorToInt(z); float zf = z - zi; zi = PositiveMod(zi, repeatZ);
            float distSqr = float.MaxValue;
            for (int dx = -1; dx <= 1; ++dx)
                for (int dy = -1; dy <= 1; ++dy)
                    for (int dz = -1; dz <= 1; ++dz)
                    {
                        int xx = PositiveMod(xi + dx, repeatX);
                        int yy = PositiveMod(yi + dy, repeatY);
                        int zz = PositiveMod(zi + dz, repeatZ);
                        float ex = dx + p[p[p[p[xx] + yy]+zz] + seed] / 256f - xf;
                        float ey = dy + p[p[p[p[xx] + yy] + zz] + (seed + 4) & 0xff] / 256f - yf;
                        float ez = dz + p[p[p[p[xx] + yy] + zz] + (seed + 9) & 0xff] / 256f - zf;
                        distSqr = Mathf.Min(distSqr, ex * ex + ey * ey + ez * ez);
                    }
            return Mathf.Sqrt(distSqr);
        }
    }
}