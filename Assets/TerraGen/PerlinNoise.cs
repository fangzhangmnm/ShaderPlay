using UnityEngine;
namespace fzmnm
{
    public static partial class Noise
    {
        public static float Octave3D(float x, float y, float z, int octave, float persistence = .5f, float lacunarity = 2, int seed = 0)
        {
            seed &= 0xff;
            float value = 0;
            float amplitude = 1;
            for (int i = 0; i < octave; ++i)
            {
                value += (Perlin3D(x + (p[(seed + i) & 0xff]), y, z) - .5f) * 2 * amplitude;
                amplitude *= persistence;
                x *= lacunarity;
                y *= lacunarity;
                z *= lacunarity;
            }
            return value;
        }
        public static float Octave2D(float x, float y,  int octave, float persistence = .5f, float lacunarity=2, int seed=0)
        {
            seed &= 0xff;
            float value = 0;
            float amplitude = 1;
            for(int i=0;i<octave;++i)
            {
                value += (Perlin2D(x+(p[(seed+i)&0xff]), y) -.5f)*2 * amplitude;
                amplitude *= persistence;
                x *= lacunarity;
                y *= lacunarity;
            }
            return value;
        }
        //credits https://adrianb.io/2014/08/09/perlinnoise.html
        public static float Perlin2D(float x, float y, int repeatX = 256, int repeatY = 256)
        {
            int xi = Mathf.FloorToInt(x); x -= xi; xi = PositiveMod(xi, repeatX); int xii = (xi + 1) % repeatX;
            int yi = Mathf.FloorToInt(y); y -= yi; yi = PositiveMod(yi, repeatY); int yii = (yi + 1) % repeatY;
            float xf = Fade(x), yf = Fade(y);
            float value=
                Mathf.Lerp(
                    Mathf.Lerp(
                        Grad(p[p[xi] + yi], x, y),
                        Grad(p[p[xii] + yi], x - 1, y),
                    xf),
                    Mathf.Lerp(
                        Grad(p[p[xi] + yii], x, y - 1),
                        Grad(p[p[xii] + yii], x - 1, y - 1),
                    xf),
                yf);
            return (value + 1) / 2;
        }
        public static float Perlin3D(float x, float y, float z, int repeatX = 256, int repeatY = 256, int repeatZ = 256)
        {
            repeatX = Mathf.Clamp(repeatX, 1, 256);
            repeatY = Mathf.Clamp(repeatY, 1, 256);
            repeatZ = Mathf.Clamp(repeatZ, 1, 256);
            int xi = Mathf.FloorToInt(x); x -= xi; xi = PositiveMod(xi, repeatX); int xii = (xi + 1) % repeatX;
            int yi = Mathf.FloorToInt(y); y -= yi; yi = PositiveMod(yi, repeatY); int yii = (yi + 1) % repeatY;
            int zi = Mathf.FloorToInt(z); z -= zi; zi = PositiveMod(zi, repeatZ); int zii = (zi + 1) % repeatZ;
            float xf = Fade(x), yf = Fade(y), zf = Fade(z);
            float value =
                Mathf.Lerp(
                    Mathf.Lerp(
                        Mathf.Lerp(
                            Grad(p[p[p[xi] + yi] + zi], x, y, z),
                            Grad(p[p[p[xii] + yi] + zi], x - 1, y, z),
                        xf),
                        Mathf.Lerp(
                            Grad(p[p[p[xi] + yii] + zi], x, y - 1, z),
                            Grad(p[p[p[xii] + yii] + zi], x - 1, y - 1, z),
                        xf),
                    yf),
                    Mathf.Lerp(
                        Mathf.Lerp(
                            Grad(p[p[p[xi] + yi] + zii], x, y, z - 1),
                            Grad(p[p[p[xii] + yi] + zii], x - 1, y, z - 1),
                        xf),
                        Mathf.Lerp(
                            Grad(p[p[p[xi] + yii] + zii], x, y - 1, z - 1),
                            Grad(p[p[p[xii] + yii] + zii], x - 1, y - 1, z - 1),
                        xf),
                    yf),
                zf);
            return (value + 1) / 2;
        }
        private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        private static int PositiveMod(int x, int m) => x > 0 ? x % m : x % m + m;

        private static float Grad(int h, float x, float y,float z)
        {
            switch (h & 0xF)
            {
                case 0x0: return x + y;
                case 0x1: return -x + y;
                case 0x2: return x - y;
                case 0x3: return -x - y;
                case 0x4: return x + z;
                case 0x5: return -x + z;
                case 0x6: return x - z;
                case 0x7: return -x - z;
                case 0x8: return y + z;
                case 0x9: return -y + z;
                case 0xA: return y - z;
                case 0xB: return -y - z;
                case 0xC: return y + x;
                case 0xD: return -y + z;
                case 0xE: return y - x;
                case 0xF: return -y - z;
                default: return 0;
            }
        }
        private static float Grad(int h, float x, float y)
        {
            switch (h & 0x3)
            {
                case 0x0: return x+y;
                case 0x1: return -x+y;
                case 0x2: return x-y;
                case 0x3: return -x-y;
                default: return 0;
            }
        }
        private static readonly int[] p = { 151,160,137,91,90,15,                 // Hash lookup table as defined by Ken Perlin.  This is a randomly
        131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,    // arranged array of all numbers from 0-255 inclusive.
        190, 6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,
        88,237,149,56,87,174,20,125,136,171,168, 68,175,74,165,71,134,139,48,27,166,
        77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,55,46,245,40,244,
        102,143,54, 65,25,63,161, 1,216,80,73,209,76,132,187,208, 89,18,169,200,196,
        135,130,116,188,159,86,164,100,109,198,173,186, 3,64,52,217,226,250,124,123,
        5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,
        223,183,170,213,119,248,152, 2,44,154,163, 70,221,153,101,155,167, 43,172,9,
        129,22,39,253, 19,98,108,110,79,113,224,232,178,185, 112,104,218,246,97,228,
        251,34,242,193,238,210,144,12,191,179,162,241, 81,51,145,235,249,14,239,107,
        49,192,214, 31,181,199,106,157,184, 84,204,176,115,121,50,45,127, 4,150,254,
        138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180,
        151,160,137,91,90,15,                 // Doubled permutation to avoid overflow
        131,13,201,95,96,53,194,233,7,225,140,36,103,30,69,142,8,99,37,240,21,10,23,    
        190, 6,148,247,120,234,75,0,26,197,62,94,252,219,203,117,35,11,32,57,177,33,
        88,237,149,56,87,174,20,125,136,171,168, 68,175,74,165,71,134,139,48,27,166,
        77,146,158,231,83,111,229,122,60,211,133,230,220,105,92,41,55,46,245,40,244,
        102,143,54, 65,25,63,161, 1,216,80,73,209,76,132,187,208, 89,18,169,200,196,
        135,130,116,188,159,86,164,100,109,198,173,186, 3,64,52,217,226,250,124,123,
        5,202,38,147,118,126,255,82,85,212,207,206,59,227,47,16,58,17,182,189,28,42,
        223,183,170,213,119,248,152, 2,44,154,163, 70,221,153,101,155,167, 43,172,9,
        129,22,39,253, 19,98,108,110,79,113,224,232,178,185, 112,104,218,246,97,228,
        251,34,242,193,238,210,144,12,191,179,162,241, 81,51,145,235,249,14,239,107,
        49,192,214, 31,181,199,106,157,184, 84,204,176,115,121,50,45,127, 4,150,254,
        138,236,205,93,222,114,67,29,24,72,243,141,128,195,78,66,215,61,156,180};
    }
}
