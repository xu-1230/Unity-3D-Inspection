using UnityEngine;

namespace VirtualInspection
{
    /// <summary>不依赖外部美术资源，运行时生成混凝土类贴图。</summary>
    public static class ProceduralTextures
    {
        public static Texture2D Concrete(int size, Color low, Color high, int seed)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            texture.name = "ProceduralConcrete_" + seed;
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;

            System.Random random = new System.Random(seed);
            float offsetX = (float)random.NextDouble() * 120f;
            float offsetY = (float)random.NextDouble() * 120f;

            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (float)x / size;
                    float ny = (float)y / size;

                    float noise = Mathf.PerlinNoise(offsetX + nx * 5.5f, offsetY + ny * 5.5f) * 0.55f
                                + Mathf.PerlinNoise(offsetX + nx * 21f, offsetY + ny * 21f) * 0.28f
                                + (float)random.NextDouble() * 0.17f;

                    float grain = Mathf.PerlinNoise(offsetX + nx * 90f, offsetY + ny * 90f) * 0.12f;
                    noise = Mathf.Clamp01(noise * 0.9f + grain);

                    pixels[y * size + x] = Color.Lerp(low, high, noise);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }
    }
}
