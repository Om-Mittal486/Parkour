using System.IO;
using UnityEditor;
using UnityEngine;

namespace Stillworks.Editor
{
    public static class StillworksSurfaceTextures
    {
        // Small seamless maps with mipmaps keep aggregate readable without distant pixel shimmer.
        public static Texture2D[] Bake()
        {
            const int size = 512;
            const string folder = "Assets/Stillworks/Generated/Textures";
            Directory.CreateDirectory(folder);
            string[] names = { "Pitted concrete", "Cracked asphalt", "Corroded steel", "Flaking paint" };
            var maps = new Texture2D[names.Length];
            for (int kind = 0; kind < maps.Length; kind++)
            {
                var pixels = new Color[size * size];
                float[] fractures = Fractures(size);
                for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    float broad = Noise(u,v,7), fine = Noise(u,v,43);
                    uint h = (uint)(x * 374761393 + y * 668265263); h = (h ^ (h >> 13)) * 1274126177;
                    float grit = (h & 65535) / 65535f;
                    float crack = fractures[y * size + x];
                    float shade = .76f + broad * .19f + (fine - .5f) * .12f;
                    float height = .45f + fine * .25f;
                    Color tint = Color.white;
                    if (kind == 0) { shade -= crack * .1f + (grit > .965f ? .18f : 0); height -= crack * .07f; }
                    if (kind == 1) { shade = .55f + grit * .32f + fine * .08f - crack * .22f; height = grit * .22f + .3f - crack * .09f; }
                    if (kind == 2)
                    {
                        float rust = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.4f,.66f,broad));
                        tint = Color.Lerp(Color.white,new Color(.75f,.42f,.24f),rust);
                        shade = .65f + fine * .22f; height = .35f + rust * .4f;
                    }
                    if (kind == 3)
                    {
                        float chipped = Mathf.SmoothStep(0,1,Mathf.InverseLerp(.55f,.68f,broad + (fine - .5f) * .18f));
                        shade = .8f + fine * .16f - crack * .08f;
                        height = Mathf.Lerp(.65f,.08f,chipped);
                    }
                    pixels[y * size + x] = new Color(tint.r * shade,tint.g * shade,tint.b * shade,Mathf.Clamp01(height));
                }
                var image = new Texture2D(size,size,TextureFormat.RGBA32,false); image.SetPixels(pixels); image.Apply();
                string path = folder + "/" + names[kind] + ".png";
                File.WriteAllBytes(path,image.EncodeToPNG()); Object.DestroyImmediate(image);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear;
                importer.mipmapEnabled = true; importer.anisoLevel = 8; importer.alphaSource = TextureImporterAlphaSource.FromInput;
                importer.alphaIsTransparency = false; importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport(); maps[kind] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return maps;
        }

        private static float Noise(float u, float v, float scale)
        {
            float x = u * scale + 83, y = v * scale + 19;
            return Mathf.Lerp(Mathf.Lerp(Mathf.PerlinNoise(x,y),Mathf.PerlinNoise(x - scale,y),u),
                Mathf.Lerp(Mathf.PerlinNoise(x,y - scale),Mathf.PerlinNoise(x - scale,y - scale),u),v);
        }

        private static float[] Fractures(int size)
        {
            var mask = new float[size * size]; var random = new System.Random(6601);
            void Line(Vector2 a, Vector2 b)
            {
                int steps = Mathf.CeilToInt(Vector2.Distance(a,b) * 1.5f);
                for (int i = 0; i <= steps; i++)
                {
                    Vector2 p = Vector2.Lerp(a,b,(float)i / steps);
                    int x = Mathf.FloorToInt(p.x), y = Mathf.FloorToInt(p.y);
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        float strength = 1 - Mathf.SmoothStep(0,1,Mathf.InverseLerp(.25f,1.1f,Vector2.Distance(p,new Vector2(x + dx,y + dy))));
                        int index = ((y + dy + size * 4) % size) * size + (x + dx + size * 4) % size;
                        mask[index] = Mathf.Max(mask[index],strength);
                    }
                }
            }
            for (int path = 0; path < 2; path++)
            {
                Vector2 p = new Vector2(random.Next(size),random.Next(size));
                for (int segment = 0; segment < 9; segment++)
                {
                    Vector2 q = p + new Vector2(random.Next(22,73),random.Next(-38,39)); Line(p,q);
                    if (segment == 3 || segment == 6) Line(q,q + new Vector2(random.Next(-30,30),random.Next(35,80)));
                    p = q;
                }
            }
            return mask;
        }
    }
}
