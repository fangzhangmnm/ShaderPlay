using UnityEngine;
namespace fzmnm
{
    public static partial class Noise
    {

        public static float VoronoiOctave3D(float x, float y, float z, int octave, float persistence = .5f, float lacunarity = 2, int repeatX = 256, int repeatY = 256, int repeatZ = 256, byte seed = 0)
        {
            float value = 0;
            float amplitude = 1;
            for (int i = 0; i < octave; ++i)
            {
                value += (Voronoi3D(x, y, z, repeatX, repeatY, repeatZ, (byte)(seed + i)) - .5f) * 2 * amplitude;
                amplitude *= persistence;
                x *= lacunarity;
                y *= lacunarity;
                z *= lacunarity;
                repeatX = Mathf.RoundToInt(repeatX * lacunarity);
                repeatY = Mathf.RoundToInt(repeatY * lacunarity);
                repeatZ = Mathf.RoundToInt(repeatZ * lacunarity);
            }
            return value;
        }
        public static float VoronoiOctave2D(float x, float y, int octave, float persistence = .5f, float lacunarity = 2, int repeatX = 256, int repeatY = 256, byte seed = 0)
        {
            seed &= 0xff;
            float value = 0;
            float amplitude = 1;
            for (int i = 0; i < octave; ++i)
            {
                value += (Voronoi2D(x, y, repeatX: repeatX, repeatY: repeatY, (byte)(seed + i)) - .5f) * 2 * amplitude;
                amplitude *= persistence;
                x *= lacunarity;
                y *= lacunarity;
                repeatX = Mathf.RoundToInt(repeatX * lacunarity);
                repeatY = Mathf.RoundToInt(repeatY * lacunarity);
            }
            return value;
        }
        public static float Voronoi2D(float x, float y, int repeatX = 256, int repeatY = 256, byte seed = 0)
        {
            int xi = Mathf.FloorToInt(x); float xf = x - xi; xi = PositiveMod(xi, repeatX) & 0xff;
            int yi = Mathf.FloorToInt(y); float yf = y - yi; yi = PositiveMod(yi, repeatY) & 0xff;
            float distSqr = float.MaxValue;
            for (int dx = -1; dx <= 1; ++dx)
                for (int dy = -1; dy <= 1; ++dy)
                {
                    int xx = PositiveMod(xi + dx, repeatX) & 0xff;
                    int yy = PositiveMod(yi + dy, repeatY) & 0xff;
                    float ex = dx + p[p[p[xx] + yy] + seed] / 256f - xf;
                    float ey = dy + p[p[p[xx] + yy] + (seed+4)&0xff] / 256f - yf;
                    distSqr = Mathf.Min(distSqr, ex * ex + ey * ey);
                }
            return Mathf.Sqrt(distSqr);
        }
        public static float Voronoi3D(float x, float y, float z, int repeatX = 256, int repeatY = 256, int repeatZ = 256, byte seed = 0)
        {
            int xi = Mathf.FloorToInt(x); float xf = x - xi; xi = PositiveMod(xi, repeatX) & 0xff;
            int yi = Mathf.FloorToInt(y); float yf = y - yi; yi = PositiveMod(yi, repeatY) & 0xff;
            int zi = Mathf.FloorToInt(z); float zf = z - zi; zi = PositiveMod(zi, repeatZ) & 0xff;
            float distSqr = float.MaxValue;
            for (int dx = -1; dx <= 1; ++dx)
                for (int dy = -1; dy <= 1; ++dy)
                    for (int dz = -1; dz <= 1; ++dz)
                    {
                        int xx = PositiveMod(xi + dx, repeatX) & 0xff;
                        int yy = PositiveMod(yi + dy, repeatY) & 0xff;
                        int zz = PositiveMod(zi + dz, repeatZ) & 0xff;
                        float ex = dx + p[p[p[p[xx] + yy]+zz] + seed] / 256f - xf;
                        float ey = dy + p[p[p[p[xx] + yy] + zz] + (seed + 4) & 0xff] / 256f - yf;
                        float ez = dz + p[p[p[p[xx] + yy] + zz] + (seed + 9) & 0xff] / 256f - zf;
                        distSqr = Mathf.Min(distSqr, ex * ex + ey * ey + ez * ez);
                    }
            return Mathf.Sqrt(distSqr);
        }
    }
}