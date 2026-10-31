using UnityEngine;

namespace VirtualInspection
{
    /// <summary>俯视小地图相机：正交投影渲染到 RenderTexture，隐藏屋顶层避免遮挡。</summary>
    public static class MinimapRig
    {
        public static RenderTexture Create()
        {
            RenderTexture texture = new RenderTexture(400, 280, 16, RenderTextureFormat.ARGB32);
            texture.name = "MinimapTexture";
            texture.Create();

            GameObject go = new GameObject("MinimapCamera");
            go.transform.position = new Vector3(0f, 60f, 0f);
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Camera camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = SiteLayout.HallDepth * 0.5f;
            camera.targetTexture = texture;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.06f, 0.08f, 0.10f, 1f);
            camera.nearClipPlane = 0.5f;
            camera.farClipPlane = 120f;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.depth = -10;
            camera.cullingMask = ~(1 << Shape.MinimapHiddenLayer);

            return texture;
        }
    }
}
