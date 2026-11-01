using System.Collections.Generic;
using UnityEngine;

namespace VirtualInspection
{
    /// <summary>单项运行参数（表头 + 实测值 + 是否超标）。</summary>
    public class InspectionReading
    {
        public readonly string Label;
        public readonly string Value;
        public readonly bool Abnormal;

        public InspectionReading(string label, string value, bool abnormal = false)
        {
            Label = label;
            Value = value;
            Abnormal = abnormal;
        }
    }

    /// <summary>一个巡检点的静态定义：位置、所属区域、参数与巡检要点。</summary>
    public class InspectionDefinition
    {
        public string Id;
        public string Name;
        public string Zone;
        public Vector3 Position;
        public string Note;
        public InspectionReading[] Readings = new InspectionReading[0];

        public bool HasAbnormal
        {
            get
            {
                if (Readings == null) return false;
                for (int i = 0; i < Readings.Length; i++)
                {
                    if (Readings[i].Abnormal) return true;
                }
                return false;
            }
        }
    }

    /// <summary>巡检点位清单，坐标与 SiteGeometry 中的设备布置一一对应。</summary>
    public static class InspectionCatalog
    {
        public static List<InspectionDefinition> Create()
        {
            List<InspectionDefinition> list = new List<InspectionDefinition>();

            list.Add(new InspectionDefinition
            {
                Id = "P-101A",
                Name = "P-101A 循环泵机组",
                Zone = "泵区 A",
                Position = new Vector3(-12.4f, 0.15f, 5.2f),
                Note = "运行平稳，油位与机封状态正常；确认出口压力稳定后记录。",
                Readings = new InspectionReading[]
                {
                    new InspectionReading("轴承温度", "62 ℃"),
                    new InspectionReading("出口压力", "0.42 MPa"),
                    new InspectionReading("振动值", "2.1 mm/s")
                }
            });

            list.Add(new InspectionDefinition
            {
                Id = "P-101B",
                Name = "P-101B 备用泵机组",
                Zone = "泵区 A",
                Position = new Vector3(-12.4f, 0.15f, 1.8f),
                Note = "轴承温度与振动同时超标，判断为轴承磨损，建议切换运行泵并通知维修班组。",
                Readings = new InspectionReading[]
                {
                    new InspectionReading("轴承温度", "118 ℃", true),
                    new InspectionReading("出口压力", "0.31 MPa"),
                    new InspectionReading("振动值", "6.8 mm/s", true)
                }
            });

            list.Add(new InspectionDefinition
            {
                Id = "T-201",
                Name = "T-201 原料储罐",
                Zone = "储罐区",
                Position = new Vector3(-3f, 0.15f, 5.9f),
                Note = "液位处于设计区间，罐体与基础无渗漏，罐顶呼吸阀通畅。",
                Readings = new InspectionReading[]
                {
                    new InspectionReading("液位", "68 %"),
                    new InspectionReading("罐内压力", "0.02 MPa"),
                    new InspectionReading("罐壁温度", "34 ℃")
                }
            });

            list.Add(new InspectionDefinition
            {
                Id = "T-202",
                Name = "T-202 成品储罐",
                Zone = "储罐区",
                Position = new Vector3(5f, 0.15f, 5.9f),
                Note = "液位正常，罐体色带与爬梯完好，注意核对液位计与 DCS 读数。",
                Readings = new InspectionReading[]
                {
                    new InspectionReading("液位", "46 %"),
                    new InspectionReading("罐内压力", "0.03 MPa"),
                    new InspectionReading("罐壁温度", "36 ℃")
                }
            });

            list.Add(new InspectionDefinition
            {
                Id = "MCC-01",
                Name = "MCC-01 低压配电柜",
                Zone = "配电区",
                Position = new Vector3(4f, 0f, -7.6f),
                Note = "柜内无异常发热与异味，指示灯状态正常，柜门关闭严密。",
                Readings = new InspectionReading[]
                {
                    new InspectionReading("三相电流", "320 A"),
                    new InspectionReading("柜内温度", "41 ℃"),
                    new InspectionReading("绝缘状态", "良好")
                }
            });

            list.Add(new InspectionDefinition
            {
                Id = "CV-301",
                Name = "CV-301 出料传送带",
                Zone = "输送区",
                Position = new Vector3(12.4f, 0f, 0f),
                Note = "皮带无跑偏、无破损，托辊转动灵活，急停按钮外观完好。",
                Readings = new InspectionReading[]
                {
                    new InspectionReading("运行速度", "0.8 m/s"),
                    new InspectionReading("电机温度", "55 ℃"),
                    new InspectionReading("皮带跑偏", "无")
                }
            });

            list.Add(new InspectionDefinition
            {
                Id = "PR-401",
                Name = "PR-401 蒸汽管廊",
                Zone = "管廊区",
                Position = new Vector3(10f, 0f, -10.9f),
                Note = "保温层无破损，疏水阀动作正常，管架无异常振动，注意保持安全距离。",
                Readings = new InspectionReading[]
                {
                    new InspectionReading("保温层表面温度", "168 ℃"),
                    new InspectionReading("蒸汽压力", "0.75 MPa"),
                    new InspectionReading("疏水阀状态", "正常")
                }
            });

            list.Add(new InspectionDefinition
            {
                Id = "CP-501",
                Name = "CP-501 现场控制台",
                Zone = "控制区",
                Position = new Vector3(15.6f, 0f, -7.2f),
                Note = "核对现场仪表与画面数据一致，报警记录已确认，控制台散热正常。",
                Readings = new InspectionReading[]
                {
                    new InspectionReading("系统压力", "0.60 MPa"),
                    new InspectionReading("在线报警", "0 条"),
                    new InspectionReading("通讯状态", "正常")
                }
            });

            return list;
        }
    }
}
