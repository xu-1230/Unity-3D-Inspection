using UnityEngine;

namespace VirtualInspection
{
    /// <summary>
    /// 场景加载后自动装配巡检现场，因此任意场景（包括空场景）按 Play 都能直接进入厂区。
    /// </summary>
    public static class InspectionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (UnityEngine.Object.FindObjectOfType<InspectionSiteRoot>() != null) return;

            // AR 场景由 ARIssueApp 负责装配，不能再生成虚拟厂房。
            if (UnityEngine.Object.FindObjectOfType<VirtualInspection.AR.ARIssueApp>() != null) return;

            GameObject root = new GameObject("[虚拟巡检现场]");
            root.AddComponent<InspectionSiteRoot>();
        }
    }
}
