using System.Collections.Generic;
using UnityEngine;

namespace TheRacer
{
    /// <summary>Creates shared runtime materials and destroys them with the generated scene.</summary>
    public static class NeonMaterialFactory
    {
        private static readonly Dictionary<int, Material> Cache = new Dictionary<int, Material>();
        private static readonly List<Material> Owned = new List<Material>();

        public static Material NeonBase => Get(0, ThemePalette.NeonCyan, 0.15f, 0.4f, true);
        public static Material DarkGlass => Get(1, new Color(0.015f, 0.025f, 0.06f), 0.72f, 0.96f, false);
        public static Material MatteBody => Get(2, ThemePalette.PanelDark, 0.32f, 0.58f, false);
        public static Material WetMetal => Get(3, new Color(0.08f, 0.12f, 0.18f), 0.88f, 0.9f, false);
        public static Material WheelRubber => Get(4, ThemePalette.Void, 0.05f, 0.28f, false);
        public static Material PlateMaterial => Get(5, Color.white, 0f, 0.5f, true);

        public static Material SilverBody(Texture2D albedo, Texture2D metallic, Texture2D normal)
        {
            return GetTextured(6, "Silver Car Body", albedo, metallic, normal, 0.25f, 0.72f);
        }

        public static Material SilverTire(Texture2D albedo, Texture2D normal)
        {
            return GetTextured(7, "Silver Car Tire", albedo, null, normal, 0f, 0.3f);
        }

        public static Material SilverWheel(Texture2D albedo, Texture2D normal)
        {
            return GetTextured(8, "Silver Car Wheel", albedo, null, normal, 0.65f, 0.78f);
        }

        public static Material BlackRaceTouringBody(Texture2D albedo, Texture2D metallic, Texture2D normal)
        { return GetTextured(9, "Black Race Touring Body", albedo, metallic, normal, 0.35f, 0.72f); }

        public static Material BlackRaceTouringPart(Texture2D albedo, Texture2D normal)
        { return GetTextured(10, "Black Race Touring Part", albedo, null, normal, 0.2f, 0.55f); }

        public static Material FlyingTransportBody(Texture2D albedo, Texture2D metallic, Texture2D normal)
        { return GetTextured(11, "Flying Transport Body", albedo, metallic, normal, 0.3f, 0.68f); }

        public static Material FlyingTransportPart(Texture2D albedo, Texture2D normal)
        { return GetTextured(12, "Flying Transport Part", albedo, null, normal, 0.2f, 0.55f); }

        private static Material Get(int key, Color color, float metallic, float smoothness, bool emission)
        {
            if (Cache.TryGetValue(key, out Material cached) && cached != null)
                return cached;
            Shader shader = Shader.Find(ThemeText.UrpLitShader);
            if (shader == null)
                shader = Shader.Find(ThemeText.StandardShader);
            Material material = new Material(shader) { color = color };
            SetColor(material, ThemeText.BaseColorProperty, color);
            SetColor(material, ThemeText.ColorProperty, color);
            SetFloat(material, ThemeText.MetallicProperty, metallic);
            SetFloat(material, ThemeText.SmoothnessProperty, smoothness);
            if (emission)
            {
                material.EnableKeyword(ThemeText.EmissionKeyword);
                SetColor(material, ThemeText.EmissionProperty, ThemePalette.Neon(Color.white, 1f));
            }
            Cache[key] = material;
            Owned.Add(material);
            return material;
        }

        private static Material GetTextured(int key, string materialName, Texture2D albedo,
            Texture2D metallic, Texture2D normal, float metallicValue, float smoothness)
        {
            if (Cache.TryGetValue(key, out Material cached) && cached != null)
                return cached;

            Shader shader = Shader.Find(ThemeText.UrpLitShader);
            if (shader == null)
                shader = Shader.Find(ThemeText.StandardShader);
            Material material = new Material(shader) { name = materialName, color = Color.white };
            SetColor(material, ThemeText.BaseColorProperty, Color.white);
            SetColor(material, ThemeText.ColorProperty, Color.white);
            SetFloat(material, ThemeText.MetallicProperty, metallicValue);
            SetFloat(material, ThemeText.SmoothnessProperty, smoothness);
            SetTexture(material, ThemeText.BaseMapProperty, albedo);
            SetTexture(material, ThemeText.MainTextureProperty, albedo);
            SetTexture(material, ThemeText.MetallicMapProperty, metallic);
            SetTexture(material, ThemeText.NormalMapProperty, normal);
            if (metallic != null)
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (normal != null)
                material.EnableKeyword("_NORMALMAP");
            Cache[key] = material;
            Owned.Add(material);
            return material;
        }

        /// <summary>Destroys cached materials when the generated runtime scene is torn down.</summary>
        public static void ReleaseAll()
        {
            for (int i = 0; i < Owned.Count; i++)
            {
                if (Owned[i] == null)
                    continue;
                if (Application.isPlaying)
                    Object.Destroy(Owned[i]);
                else
                    Object.DestroyImmediate(Owned[i]);
            }
            Owned.Clear();
            Cache.Clear();
        }

        private static void SetColor(Material material, string property, Color color)
        {
            if (material.HasProperty(property))
                material.SetColor(property, color);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property))
                material.SetFloat(property, value);
        }

        private static void SetTexture(Material material, string property, Texture texture)
        {
            if (texture != null && material.HasProperty(property))
                material.SetTexture(property, texture);
        }
    }
}
