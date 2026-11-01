using UnityEngine;

namespace VirtualInspection
{
    /// <summary>
    /// 厂区整体尺寸与出生点等公共常量，场景搭建、点位与小地图共用，避免坐标漂移。
    /// </summary>
    public static class SiteLayout
    {
        public const float HallWidth = 40f;
        public const float HallDepth = 28f;
        public const float WallHeight = 8f;
        public const float EyeHeight = 1.62f;

        public static readonly Vector3 PlayerSpawn = new Vector3(-6f, 0.05f, -9.5f);

        /// <summary>把世界坐标换算成小地图上的 0~1 归一化坐标（+Z 为地图上方）。</summary>
        public static Vector2 Normalize(Vector3 worldPosition)
        {
            return new Vector2(
                Mathf.Clamp01((worldPosition.x + HallWidth * 0.5f) / HallWidth),
                Mathf.Clamp01((worldPosition.z + HallDepth * 0.5f) / HallDepth));
        }
    }
}
