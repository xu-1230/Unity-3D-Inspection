using UnityEngine;

namespace VirtualInspection
{
    /// <summary>
    /// 厂区 5 个固定区域的划分，范围与 <see cref="SiteGeometry"/> 的设备基础一致：
    /// 泵区、储罐区、输送区、配电区、控制区。区域之间互不重叠，命中点落在全部区域之外时返回空字符串。
    /// 修改范围时需同步 frontend/src/lib/regions.ts 与 docs/需求规格说明书 6.4。
    /// </summary>
    public static class InspectionRegions
    {
        /// <summary>框外记录在管理端的展示名。</summary>
        public const string OutsideName = "厂区其他";

        private sealed class Zone
        {
            public readonly string Name;
            private readonly float xMin;
            private readonly float xMax;
            private readonly float zMin;
            private readonly float zMax;

            public Zone(string name, float xMin, float xMax, float zMin, float zMax)
            {
                Name = name;
                this.xMin = xMin;
                this.xMax = xMax;
                this.zMin = zMin;
                this.zMax = zMax;
            }

            public bool Contains(float x, float z)
            {
                return x >= xMin && x <= xMax && z >= zMin && z <= zMax;
            }
        }

        private static readonly Zone[] Zones =
        {
            new Zone("泵区", -19.5f, -9.2f, -0.4f, 7.2f),
            new Zone("储罐区", -6.4f, 8.4f, 5f, 12.8f),
            new Zone("输送区", 12.2f, 16.4f, -6f, 6f),
            new Zone("配电区", -0.4f, 10.4f, -11f, -8f),
            new Zone("控制区", 13f, 19f, -12.6f, -7.4f)
        };

        /// <summary>按命中点的世界坐标判定固定区域，框外返回空字符串。</summary>
        public static string Resolve(Vector3 position)
        {
            if (float.IsNaN(position.x) || float.IsNaN(position.z))
            {
                return string.Empty;
            }

            for (int i = 0; i < Zones.Length; i++)
            {
                if (Zones[i].Contains(position.x, position.z))
                {
                    return Zones[i].Name;
                }
            }

            return string.Empty;
        }

        /// <summary>用于界面展示的区域名，框外返回「厂区其他」。</summary>
        public static string DisplayName(Vector3 position)
        {
            string name = Resolve(position);
            return string.IsNullOrEmpty(name) ? OutsideName : name;
        }
    }
}
