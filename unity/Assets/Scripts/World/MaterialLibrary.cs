using UnityEngine;

namespace VirtualInspection
{
    /// <summary>运行期材质库，全部基于内置 Standard 着色器，避免依赖 URP/HDRP 资源。</summary>
    public class MaterialLibrary
    {
        public Material Floor;
        public Material Wall;
        public Material Concrete;
        public Material Steel;
        public Material DarkSteel;
        public Material Yellow;
        public Material Orange;
        public Material Blue;
        public Material Red;
        public Material Rubber;
        public Material Screen;
        public Material LightPanel;

        public Material MarkerPending;
        public Material MarkerDone;
        public Material MarkerAbnormal;
        public Material MarkerHighlight;

        public MaterialLibrary()
        {
            Texture2D concreteTexture = ProceduralTextures.Concrete(256, new Color(0.24f, 0.25f, 0.27f), new Color(0.45f, 0.46f, 0.48f), 11);
            Texture2D wallTexture = ProceduralTextures.Concrete(256, new Color(0.56f, 0.58f, 0.61f), new Color(0.79f, 0.80f, 0.82f), 53);

            Floor = Make("地面-混凝土", Color.white, 0.05f, 0.12f, concreteTexture, new Vector2(9f, 7f));
            Wall = Make("墙体", Color.white, 0f, 0.08f, wallTexture, new Vector2(5f, 2f));
            Concrete = Make("混凝土基础", new Color(0.66f, 0.67f, 0.69f), 0.05f, 0.16f, concreteTexture, new Vector2(4f, 2f));

            Steel = Make("钢材", new Color(0.64f, 0.67f, 0.71f), 0.85f, 0.55f);
            DarkSteel = Make("深色钢材", new Color(0.20f, 0.23f, 0.26f), 0.75f, 0.45f);
            Yellow = Make("安全黄", new Color(0.93f, 0.71f, 0.13f), 0.1f, 0.35f);
            Orange = Make("设备橙", new Color(0.87f, 0.43f, 0.16f), 0.25f, 0.4f);
            Blue = Make("设备蓝", new Color(0.18f, 0.43f, 0.73f), 0.3f, 0.45f);
            Red = Make("消防红", new Color(0.75f, 0.15f, 0.12f), 0.1f, 0.35f);
            Rubber = Make("输送带", new Color(0.10f, 0.10f, 0.11f), 0f, 0.18f);

            Screen = Make("屏幕", new Color(0.06f, 0.22f, 0.30f), 0f, 0.6f, null, null, new Color(0.10f, 0.55f, 0.75f), 1.4f);
            LightPanel = Make("灯板", new Color(0.95f, 0.95f, 0.90f), 0f, 0.2f, null, null, new Color(1f, 0.94f, 0.80f), 1.6f);

            MarkerPending = Make("标记-待巡检", new Color(0.20f, 0.80f, 0.92f), 0f, 0.7f, null, null, new Color(0.20f, 0.88f, 1f), 1.9f);
            MarkerDone = Make("标记-已完成", new Color(0.25f, 0.85f, 0.45f), 0f, 0.7f, null, null, new Color(0.20f, 0.92f, 0.45f), 1.5f);
            MarkerAbnormal = Make("标记-异常", new Color(1f, 0.32f, 0.25f), 0f, 0.7f, null, null, new Color(1f, 0.25f, 0.18f), 1.9f);
            MarkerHighlight = Make("标记-高亮", new Color(0.75f, 1f, 0.95f), 0f, 0.8f, null, null, new Color(0.65f, 1f, 0.95f), 2.4f);
        }

        private static Material Make(string name, Color color, float metallic, float smoothness,
            Texture2D texture = null, Vector2? tiling = null, Color? emission = null, float emissionBoost = 1f)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Legacy Shaders/Diffuse");

            Material material = new Material(shader);
            material.name = name;

            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);

            if (texture != null)
            {
                material.mainTexture = texture;
                if (tiling.HasValue) material.mainTextureScale = tiling.Value;
            }

            if (emission.HasValue && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value * emissionBoost);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            return material;
        }
    }
}
