using UnityEngine;

namespace VirtualInspection
{
    /// <summary>
    /// 用基础图元逐件拼装一座小型化工车间：
    /// 厂房结构 -> 地面标线 -> 泵区 -> 储罐区 -> 配电区 -> 输送区 -> 管廊 -> 控制区 -> 现场设施 -> 照明。
    /// </summary>
    public static class SiteGeometry
    {
        public static void Build(Transform root, MaterialLibrary m)
        {
            BuildShell(Group(root, "厂房结构"), m);
            BuildFloorMarkings(Group(root, "地面标线"), m);
            BuildPumpArea(Group(root, "泵区"), m);
            BuildTankArea(Group(root, "储罐区"), m);
            BuildSwitchgear(Group(root, "配电区"), m);
            BuildConveyor(Group(root, "输送区"), m);
            BuildPipeRack(Group(root, "管廊区"), m);
            BuildControlDesk(Group(root, "控制区"), m);
            BuildProps(Group(root, "现场设施"), m);
            BuildLights(Group(root, "照明"), m);
        }

        private static Transform Group(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // ---------------------------------------------------------------- 厂房

        private static void BuildShell(Transform t, MaterialLibrary m)
        {
            Shape.Box(t, "地面", new Vector3(0f, -0.15f, 0f), new Vector3(40f, 0.3f, 28f), m.Floor);
            Shape.Box(t, "北墙", new Vector3(0f, 4f, 14.2f), new Vector3(40.8f, 8f, 0.4f), m.Wall);
            Shape.Box(t, "南墙", new Vector3(0f, 4f, -14.2f), new Vector3(40.8f, 8f, 0.4f), m.Wall);
            Shape.Box(t, "东墙", new Vector3(20.2f, 4f, 0f), new Vector3(0.4f, 8f, 28f), m.Wall);
            Shape.Box(t, "西墙", new Vector3(-20.2f, 4f, 0f), new Vector3(0.4f, 8f, 28f), m.Wall);

            // 墙裙
            Shape.Box(t, "墙裙-北", new Vector3(0f, 0.65f, 13.96f), new Vector3(40f, 1.3f, 0.06f), m.DarkSteel, false);
            Shape.Box(t, "墙裙-南", new Vector3(0f, 0.65f, -13.96f), new Vector3(40f, 1.3f, 0.06f), m.DarkSteel, false);
            Shape.Box(t, "墙裙-东", new Vector3(19.96f, 0.65f, 0f), new Vector3(0.06f, 1.3f, 28f), m.DarkSteel, false);
            Shape.Box(t, "墙裙-西", new Vector3(-19.96f, 0.65f, 0f), new Vector3(0.06f, 1.3f, 28f), m.DarkSteel, false);

            // 卷帘门（入口）
            Shape.Box(t, "卷帘门", new Vector3(0f, 2.4f, -13.90f), new Vector3(6.4f, 4.8f, 0.12f), m.DarkSteel, false);
            for (int i = 0; i < 12; i++)
            {
                Shape.Box(t, "卷帘门叶片", new Vector3(0f, 0.25f + i * 0.4f, -13.82f), new Vector3(6.2f, 0.05f, 0.05f), m.Steel, false);
            }
            Shape.Box(t, "门楣", new Vector3(0f, 4.95f, -13.88f), new Vector3(7f, 0.28f, 0.3f), m.Yellow, false);

            GameObject roof = Shape.Box(t, "屋顶", new Vector3(0f, 8.15f, 0f), new Vector3(41.2f, 0.3f, 29.2f), m.DarkSteel, false);
            Shape.HideFromMinimap(roof);

            for (int i = -2; i <= 2; i++)
            {
                GameObject beam = Shape.Box(t, "屋架", new Vector3(i * 8f, 7.78f, 0f), new Vector3(0.28f, 0.42f, 28f), m.DarkSteel, false);
                Shape.HideFromMinimap(beam);
            }

            Vector3[] pillars =
            {
                new Vector3(-10.5f, 0f, -7.5f), new Vector3(10.5f, 0f, -7.5f),
                new Vector3(-10.5f, 0f, 7.5f), new Vector3(10.5f, 0f, 7.5f),
                new Vector3(-19.4f, 0f, -13.4f), new Vector3(19.4f, 0f, -13.4f),
                new Vector3(-19.4f, 0f, 13.4f), new Vector3(19.4f, 0f, 13.4f)
            };

            for (int i = 0; i < pillars.Length; i++)
            {
                Shape.Box(t, "立柱", pillars[i] + new Vector3(0f, 4f, 0f), new Vector3(0.7f, 8f, 0.7f), m.Wall);
                Shape.Box(t, "柱脚", pillars[i] + new Vector3(0f, 0.15f, 0f), new Vector3(0.95f, 0.3f, 0.95f), m.DarkSteel, false);
            }
        }

        // ---------------------------------------------------------------- 地标

        private static void BuildFloorMarkings(Transform t, MaterialLibrary m)
        {
            Shape.Box(t, "通道边线A", new Vector3(-7.5f, 0.012f, -1f), new Vector3(0.14f, 0.02f, 24f), m.Yellow, false);
            Shape.Box(t, "通道边线B", new Vector3(0.5f, 0.012f, -1f), new Vector3(0.14f, 0.02f, 24f), m.Yellow, false);

            for (int i = 0; i < 16; i++)
            {
                float z = -11f + i * 1.5f;
                Shape.Box(t, "巡检路线", new Vector3(-3.5f, 0.014f, z), new Vector3(0.8f, 0.02f, 0.16f), m.Yellow, false);
            }

            FloorFrame(t, m, new Vector3(14f, 0f, 0f), 3.6f, 11.2f);
            FloorFrame(t, m, new Vector3(16f, 0f, -10f), 6f, 5f);
            FloorFrame(t, m, new Vector3(5f, 0f, -9.5f), 10f, 3f);
        }

        private static void FloorFrame(Transform t, MaterialLibrary m, Vector3 center, float width, float depth)
        {
            const float y = 0.014f;
            Shape.Box(t, "区域边线", new Vector3(center.x, y, center.z + depth * 0.5f), new Vector3(width, 0.02f, 0.1f), m.Yellow, false);
            Shape.Box(t, "区域边线", new Vector3(center.x, y, center.z - depth * 0.5f), new Vector3(width, 0.02f, 0.1f), m.Yellow, false);
            Shape.Box(t, "区域边线", new Vector3(center.x + width * 0.5f, y, center.z), new Vector3(0.1f, 0.02f, depth), m.Yellow, false);
            Shape.Box(t, "区域边线", new Vector3(center.x - width * 0.5f, y, center.z), new Vector3(0.1f, 0.02f, depth), m.Yellow, false);
        }

        // ---------------------------------------------------------------- 泵区

        private static void BuildPumpArea(Transform t, MaterialLibrary m)
        {
            Shape.Box(t, "泵组基础", new Vector3(-14f, 0.075f, 3.5f), new Vector3(10f, 0.15f, 7f), m.Concrete);

            BuildPump(t, m, new Vector3(-15.4f, 0.15f, 5.2f), "P-101A");
            BuildPump(t, m, new Vector3(-15.4f, 0.15f, 1.8f), "P-101B");

            // 出口集管
            Shape.Pipe(t, "出口集管", new Vector3(-16.5f, 3.05f, 0.9f), new Vector3(-16.5f, 3.05f, 6.1f), 0.15f, m.Steel);
            Shape.Cylinder(t, "集管支架A", new Vector3(-16.5f, 1.52f, 2.5f), 0.09f, 3.05f, m.DarkSteel, Vector3.up, false);
            Shape.Cylinder(t, "集管支架B", new Vector3(-16.5f, 1.52f, 4.6f), 0.09f, 3.05f, m.DarkSteel, Vector3.up, false);
            Shape.Sphere(t, "集管弯头", new Vector3(-16.5f, 3.05f, 6.1f), new Vector3(0.42f, 0.42f, 0.42f), m.Steel, false);

            // 护栏（东侧留出入口）
            Rail(t, m, new Vector3(-19f, 0.15f, 0.25f), new Vector3(-19f, 0.15f, 6.75f));
            Rail(t, m, new Vector3(-19f, 0.15f, 6.75f), new Vector3(-9.3f, 0.15f, 6.75f));
            Rail(t, m, new Vector3(-19f, 0.15f, 0.25f), new Vector3(-9.3f, 0.15f, 0.25f));
        }

        private static void BuildPump(Transform t, MaterialLibrary m, Vector3 basePos, string name)
        {
            Transform pump = Group(t, name);

            Shape.Box(pump, "泵底座", basePos + new Vector3(0f, 0.125f, 0f), new Vector3(3f, 0.25f, 1.5f), m.DarkSteel);
            Shape.Box(pump, "泵底板", basePos + new Vector3(0f, 0.02f, 0f), new Vector3(3.4f, 0.04f, 1.85f), m.DarkSteel, false);
            Shape.Cylinder(pump, "泵体", basePos + new Vector3(-0.65f, 0.7f, 0f), 0.45f, 1.1f, m.Orange, Vector3.right);
            Shape.Sphere(pump, "泵壳", basePos + new Vector3(-1.18f, 0.7f, 0f), new Vector3(1.18f, 1.18f, 1.18f), m.Orange);
            Shape.Cylinder(pump, "电机", basePos + new Vector3(0.58f, 0.67f, 0f), 0.38f, 1.2f, m.Steel, Vector3.right);
            Shape.Box(pump, "联轴器护罩", basePos + new Vector3(0f, 0.7f, 0f), new Vector3(0.55f, 0.6f, 0.66f), m.Yellow, false);

            Shape.Pipe(pump, "泵进口管", basePos + new Vector3(-1.18f, 0.95f, 0f), basePos + new Vector3(-1.18f, 2.95f, 0f), 0.13f, m.Steel);
            Shape.Pipe(pump, "泵出口管", basePos + new Vector3(-0.65f, 1.05f, 0f), basePos + new Vector3(-0.65f, 2.95f, 0f), 0.12f, m.Steel);
            Shape.Sphere(pump, "进口弯头", basePos + new Vector3(-1.18f, 2.95f, 0f), new Vector3(0.36f, 0.36f, 0.36f), m.Steel, false);

            Shape.Box(pump, "出口阀门", basePos + new Vector3(-0.65f, 2.05f, 0f), new Vector3(0.22f, 0.22f, 0.22f), m.DarkSteel, false);
            Shape.Cylinder(pump, "阀门手轮", basePos + new Vector3(-0.65f, 2.05f, -0.28f), 0.22f, 0.05f, m.Red, Vector3.forward, false);
            Shape.Cylinder(pump, "压力表", basePos + new Vector3(-0.65f, 2.5f, -0.22f), 0.13f, 0.08f, m.Steel, Vector3.forward, false);
        }

        // ---------------------------------------------------------------- 储罐

        private static void BuildTankArea(Transform t, MaterialLibrary m)
        {
            Shape.Box(t, "储罐基础", new Vector3(1f, 0.075f, 9f), new Vector3(14f, 0.15f, 7.6f), m.Concrete);

            BuildTank(t, m, new Vector3(-3f, 0.15f, 9f), m.Blue);
            BuildTank(t, m, new Vector3(5f, 0.15f, 9f), m.Orange);

            Shape.Pipe(t, "罐间管线", new Vector3(-3f, 3.6f, 6.2f), new Vector3(5f, 3.6f, 6.2f), 0.14f, m.Steel);
            Shape.Pipe(t, "罐间竖管A", new Vector3(-3f, 3.6f, 6.2f), new Vector3(-3f, 5.2f, 6.2f), 0.12f, m.Steel);
            Shape.Pipe(t, "罐间竖管B", new Vector3(5f, 3.6f, 6.2f), new Vector3(5f, 5.2f, 6.2f), 0.12f, m.Steel);
        }

        private static void BuildTank(Transform t, MaterialLibrary m, Vector3 basePos, Material band)
        {
            Shape.Cylinder(t, "罐体", basePos + new Vector3(0f, 3f, 0f), 2.3f, 6f, m.Steel, Vector3.up);
            Shape.Sphere(t, "罐顶", basePos + new Vector3(0f, 6f, 0f), new Vector3(4.65f, 1.6f, 4.65f), m.Steel);
            Shape.Cylinder(t, "罐体色带", basePos + new Vector3(0f, 4.7f, 0f), 2.32f, 0.55f, band, Vector3.up, false);
            Shape.Cylinder(t, "罐顶接管", basePos + new Vector3(0f, 6.5f, 0f), 0.16f, 0.5f, m.Steel, Vector3.up, false);

            // 直爬梯
            for (int i = 0; i < 2; i++)
            {
                float x = basePos.x + (i == 0 ? -0.3f : 0.3f);
                Shape.Box(t, "爬梯立杆", new Vector3(x, basePos.y + 3f, basePos.z - 2.36f), new Vector3(0.06f, 5.8f, 0.06f), m.Yellow, false);
            }
            for (int i = 0; i < 13; i++)
            {
                float y = basePos.y + 0.5f + i * 0.42f;
                Shape.Box(t, "爬梯踏步", new Vector3(basePos.x, y, basePos.z - 2.36f), new Vector3(0.66f, 0.05f, 0.07f), m.Yellow, false);
            }

            // 罐底出口
            Shape.Pipe(t, "罐底出口", basePos + new Vector3(0f, 0.85f, -2.3f), basePos + new Vector3(0f, 0.85f, -3.5f), 0.13f, m.Steel);
        }

        // ---------------------------------------------------------------- 配电

        private static void BuildSwitchgear(Transform t, MaterialLibrary m)
        {
            Shape.Box(t, "配电柜基础", new Vector3(5f, 0.06f, -9.5f), new Vector3(10f, 0.12f, 2.4f), m.Concrete);

            for (int i = 0; i < 4; i++)
            {
                float x = 2f + i * 2f;
                Shape.Box(t, "低压配电柜", new Vector3(x, 1.16f, -9.5f), new Vector3(1.7f, 2.2f, 0.9f), m.Steel);
                Shape.Box(t, "柜门", new Vector3(x, 1.16f, -9.03f), new Vector3(1.5f, 1.95f, 0.03f), m.DarkSteel, false);
                Shape.Box(t, "柜门把手", new Vector3(x + 0.6f, 1f, -9f), new Vector3(0.05f, 0.3f, 0.05f), m.Steel, false);

                for (int j = 0; j < 3; j++)
                {
                    Material lamp = j == 2 ? m.MarkerAbnormal : m.MarkerDone;
                    Shape.Sphere(t, "指示灯", new Vector3(x - 0.45f + j * 0.22f, 2.02f, -9f), new Vector3(0.07f, 0.07f, 0.07f), lamp);
                }

                Shape.Box(t, "柜顶铭牌", new Vector3(x, 2.34f, -9.03f), new Vector3(1.2f, 0.1f, 0.02f), m.Yellow, false);
            }

            Shape.Box(t, "电缆桥架", new Vector3(5f, 2.72f, -9.5f), new Vector3(8.6f, 0.12f, 0.5f), m.DarkSteel, false);
            for (int i = 0; i < 3; i++)
            {
                Shape.Box(t, "桥架吊杆", new Vector3(1f + i * 4f, 3.3f, -9.5f), new Vector3(0.05f, 1.2f, 0.05f), m.DarkSteel, false);
            }
        }

        // ---------------------------------------------------------------- 输送

        private static void BuildConveyor(Transform t, MaterialLibrary m)
        {
            for (int i = 0; i < 6; i++)
            {
                float z = -4.5f + i * 1.8f;
                Shape.Box(t, "输送带支腿", new Vector3(13.6f, 0.45f, z), new Vector3(0.1f, 0.9f, 0.1f), m.DarkSteel, false);
                Shape.Box(t, "输送带支腿", new Vector3(14.9f, 0.45f, z), new Vector3(0.1f, 0.9f, 0.1f), m.DarkSteel, false);
            }

            Shape.Box(t, "输送带机架", new Vector3(14.25f, 0.95f, 0f), new Vector3(1.7f, 0.12f, 10.4f), m.Steel);
            Shape.Box(t, "输送带", new Vector3(14.25f, 1.05f, 0f), new Vector3(1.35f, 0.08f, 10f), m.Rubber);
            Shape.Box(t, "输送带侧板A", new Vector3(13.42f, 1.16f, 0f), new Vector3(0.06f, 0.3f, 10f), m.Yellow, false);
            Shape.Box(t, "输送带侧板B", new Vector3(15.08f, 1.16f, 0f), new Vector3(0.06f, 0.3f, 10f), m.Yellow, false);

            Shape.Cylinder(t, "端部滚筒", new Vector3(14.25f, 1f, 5.1f), 0.3f, 1.5f, m.DarkSteel, Vector3.right, false);
            Shape.Cylinder(t, "端部滚筒", new Vector3(14.25f, 1f, -5.1f), 0.3f, 1.5f, m.DarkSteel, Vector3.right, false);
            Shape.Box(t, "驱动电机", new Vector3(15.35f, 1.05f, -4.3f), new Vector3(0.55f, 0.55f, 0.7f), m.Orange, false);
            Shape.Box(t, "电机护罩", new Vector3(15.35f, 1.05f, -3.75f), new Vector3(0.6f, 0.6f, 0.08f), m.Yellow, false);

            Shape.Box(t, "急停立柱", new Vector3(12.4f, 0.6f, 3.5f), new Vector3(0.08f, 1.2f, 0.08f), m.DarkSteel, false);
            Shape.Sphere(t, "急停按钮", new Vector3(12.4f, 1.3f, 3.5f), new Vector3(0.16f, 0.13f, 0.16f), m.MarkerAbnormal);
            Shape.Box(t, "急停标牌", new Vector3(12.4f, 1.05f, 3.56f), new Vector3(0.22f, 0.16f, 0.02f), m.Yellow, false);
        }

        // ---------------------------------------------------------------- 管廊

        private static void BuildPipeRack(Transform t, MaterialLibrary m)
        {
            float[] posts = { -18f, -10f, -2f, 6f, 14f };
            for (int i = 0; i < posts.Length; i++)
            {
                Shape.Cylinder(t, "管廊立柱", new Vector3(posts[i], 2.5f, -12.3f), 0.13f, 5f, m.Steel, Vector3.up);
                Shape.Box(t, "管廊横梁", new Vector3(posts[i], 5f, -12.3f), new Vector3(0.24f, 0.24f, 2.6f), m.Steel, false);
            }

            Shape.Pipe(t, "工艺管线", new Vector3(-19f, 5.2f, -12.9f), new Vector3(15f, 5.2f, -12.9f), 0.17f, m.Steel);
            Shape.Pipe(t, "蒸汽管线", new Vector3(-19f, 5.42f, -12.3f), new Vector3(15f, 5.42f, -12.3f), 0.28f, m.Concrete);
            Shape.Pipe(t, "公用管线", new Vector3(-19f, 5.16f, -11.72f), new Vector3(15f, 5.16f, -11.72f), 0.13f, m.Yellow);

            // 蒸汽下降管与阀门（PR-401 巡检对象）
            Shape.Sphere(t, "下降管弯头", new Vector3(10f, 5.42f, -12.3f), new Vector3(0.55f, 0.55f, 0.55f), m.Steel, false);
            Shape.Pipe(t, "蒸汽下降管", new Vector3(10f, 5.42f, -12.3f), new Vector3(10f, 1.45f, -12.3f), 0.16f, m.Steel);
            Shape.Cylinder(t, "疏水阀", new Vector3(10f, 1.55f, -12.3f), 0.2f, 0.4f, m.MarkerAbnormal, Vector3.up, false);
            Shape.Cylinder(t, "疏水阀手轮", new Vector3(10f, 1.55f, -11.95f), 0.3f, 0.05f, m.Red, Vector3.forward, false);
            Shape.Cylinder(t, "压力表", new Vector3(10f, 2.35f, -12.05f), 0.14f, 0.08f, m.Steel, Vector3.forward, false);
        }

        // ---------------------------------------------------------------- 控制

        private static void BuildControlDesk(Transform t, MaterialLibrary m)
        {
            Shape.Box(t, "控制区地垫", new Vector3(16f, 0.015f, -10f), new Vector3(6f, 0.03f, 5f), m.DarkSteel, false);

            Shape.Box(t, "控制台台面", new Vector3(16f, 0.92f, -8.6f), new Vector3(3.4f, 0.1f, 1.4f), m.DarkSteel);
            Shape.Box(t, "控制台挡板", new Vector3(16f, 0.8f, -9.26f), new Vector3(3.4f, 0.62f, 0.06f), m.DarkSteel, false);

            Vector3[] legs =
            {
                new Vector3(14.5f, 0.45f, -9.2f), new Vector3(17.5f, 0.45f, -9.2f),
                new Vector3(14.5f, 0.45f, -8f), new Vector3(17.5f, 0.45f, -8f)
            };
            for (int i = 0; i < legs.Length; i++)
            {
                Shape.Box(t, "控制台支腿", legs[i], new Vector3(0.08f, 0.9f, 0.08f), m.DarkSteel, false);
            }

            for (int i = 0; i < 3; i++)
            {
                float x = 15.1f + i * 0.9f;
                Shape.Box(t, "显示器", new Vector3(x, 1.44f, -8.85f), new Vector3(0.82f, 0.58f, 0.06f), m.DarkSteel, false);
                Shape.Box(t, "屏幕", new Vector3(x, 1.44f, -8.81f), new Vector3(0.75f, 0.51f, 0.02f), m.Screen, false);
                Shape.Box(t, "显示器支架", new Vector3(x, 1.13f, -8.9f), new Vector3(0.1f, 0.2f, 0.1f), m.DarkSteel, false);
            }

            // 墙面大屏
            Shape.Box(t, "大屏边框", new Vector3(16f, 2.7f, -13.9f), new Vector3(3.2f, 1.8f, 0.12f), m.DarkSteel, false);
            Shape.Box(t, "大屏画面", new Vector3(16f, 2.7f, -13.83f), new Vector3(2.95f, 1.55f, 0.02f), m.Screen, false);

            // 值班座椅（避开 CP-501 巡检点）
            Shape.Box(t, "座椅", new Vector3(16.9f, 0.48f, -7.1f), new Vector3(0.6f, 0.1f, 0.6f), m.DarkSteel, false);
            Shape.Box(t, "座椅靠背", new Vector3(16.9f, 0.85f, -7.4f), new Vector3(0.6f, 0.7f, 0.08f), m.DarkSteel, false);
            Shape.Cylinder(t, "座椅立柱", new Vector3(16.9f, 0.23f, -7.1f), 0.06f, 0.46f, m.Steel, Vector3.up, false);
        }

        // ---------------------------------------------------------------- 设施

        private static void BuildProps(Transform t, MaterialLibrary m)
        {
            Vector3[] extinguishers =
            {
                new Vector3(-19.2f, 0f, 12.6f), new Vector3(19.2f, 0f, -12.6f),
                new Vector3(-18.6f, 0f, -6.5f)
            };
            for (int i = 0; i < extinguishers.Length; i++)
            {
                Shape.Cylinder(t, "灭火器", extinguishers[i] + new Vector3(0f, 0.3f, 0f), 0.13f, 0.6f, m.Red, Vector3.up);
                Shape.Cylinder(t, "灭火器喷管", extinguishers[i] + new Vector3(0.14f, 0.5f, 0f), 0.02f, 0.25f, m.DarkSteel, Vector3.up, false);
            }

            for (int i = 0; i < 2; i++)
            {
                Shape.Cylinder(t, "物料桶", new Vector3(-18.4f, 0.45f, -4.4f + i * 1.05f), 0.35f, 0.9f, m.Blue, Vector3.up);
            }

            Shape.Box(t, "工具车", new Vector3(-8.4f, 0.45f, -5.6f), new Vector3(0.9f, 0.9f, 0.6f), m.Orange);
            Shape.Box(t, "工具车台面", new Vector3(-8.4f, 0.92f, -5.6f), new Vector3(0.98f, 0.05f, 0.68f), m.DarkSteel, false);

            Shape.Box(t, "托盘", new Vector3(10f, 0.08f, 11f), new Vector3(1.4f, 0.16f, 1.1f), m.DarkSteel);
            Shape.Box(t, "托盘货物", new Vector3(10f, 0.4f, 11f), new Vector3(1.1f, 0.45f, 0.9f), m.Yellow, false);

            Vector3[] cones =
            {
                new Vector3(8.5f, 0f, -6.6f), new Vector3(-7.2f, 0f, 8.6f),
                new Vector3(11.6f, 0f, -4.2f), new Vector3(-2.4f, 0f, -6.2f)
            };
            for (int i = 0; i < cones.Length; i++)
            {
                Shape.Cylinder(t, "安全锥", cones[i] + new Vector3(0f, 0.24f, 0f), 0.16f, 0.48f, m.Orange, Vector3.up, false);
                Shape.Cylinder(t, "安全锥反光带", cones[i] + new Vector3(0f, 0.32f, 0f), 0.145f, 0.1f, m.LightPanel, Vector3.up, false);
                Shape.Box(t, "锥形底座", cones[i] + new Vector3(0f, 0.02f, 0f), new Vector3(0.42f, 0.04f, 0.42f), m.Orange, false);
            }

            Shape.Box(t, "消火栓箱", new Vector3(18f, 1.15f, 13.88f), new Vector3(0.62f, 0.82f, 0.22f), m.Red, false);
            Shape.Box(t, "安全宣传板", new Vector3(-19.9f, 1.9f, 4f), new Vector3(0.06f, 1.1f, 1.8f), m.DarkSteel, false);
            Shape.Box(t, "宣传板画面", new Vector3(-19.86f, 1.9f, 4f), new Vector3(0.02f, 0.95f, 1.6f), m.Screen, false);
        }

        // ---------------------------------------------------------------- 照明

        private static void BuildLights(Transform t, MaterialLibrary m)
        {
            Vector3[] positions =
            {
                new Vector3(-12f, 7.55f, -7f), new Vector3(0f, 7.55f, -7f), new Vector3(12f, 7.55f, -7f),
                new Vector3(-12f, 7.55f, 7f), new Vector3(0f, 7.55f, 7f), new Vector3(12f, 7.55f, 7f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject housing = Shape.Box(t, "灯具外壳", positions[i], new Vector3(1.9f, 0.12f, 0.5f), m.DarkSteel, false);
                Shape.HideFromMinimap(housing);

                GameObject panel = Shape.Box(t, "灯板", positions[i] + new Vector3(0f, -0.075f, 0f), new Vector3(1.7f, 0.03f, 0.38f), m.LightPanel, false);
                Shape.HideFromMinimap(panel);

                GameObject lightGo = new GameObject("点光源");
                lightGo.transform.SetParent(t, false);
                lightGo.transform.position = positions[i] + new Vector3(0f, -0.22f, 0f);
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 17f;
                light.intensity = 1.15f;
                light.color = new Color(1f, 0.96f, 0.88f);
                light.shadows = LightShadows.None;
            }
        }

        // ---------------------------------------------------------------- 工具

        private static void Rail(Transform parent, MaterialLibrary m, Vector3 a, Vector3 b)
        {
            const float height = 1.15f;
            Vector3 delta = b - a;
            float length = delta.magnitude;
            if (length < 0.05f) return;

            bool alongX = Mathf.Abs(delta.x) > Mathf.Abs(delta.z);
            Vector3 topSize = alongX ? new Vector3(length, 0.07f, 0.07f) : new Vector3(0.07f, 0.07f, length);
            Vector3 midSize = alongX ? new Vector3(length, 0.05f, 0.05f) : new Vector3(0.05f, 0.05f, length);

            Shape.Box(parent, "护栏扶手", (a + b) * 0.5f + Vector3.up * height, topSize, m.Yellow, false);
            Shape.Box(parent, "护栏横杆", (a + b) * 0.5f + Vector3.up * 0.62f, midSize, m.Yellow, false);

            int posts = Mathf.Max(2, Mathf.RoundToInt(length / 1.8f) + 1);
            for (int i = 0; i < posts; i++)
            {
                float tt = (float)i / (posts - 1);
                Vector3 p = Vector3.Lerp(a, b, tt);
                Shape.Box(parent, "护栏立柱", p + Vector3.up * (height * 0.5f), new Vector3(0.07f, height, 0.07f), m.Yellow, false);
            }
        }
    }
}
