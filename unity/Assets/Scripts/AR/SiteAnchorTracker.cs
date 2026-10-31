using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace VirtualInspection.AR
{
    /// <summary>
    /// 现场基准图定位器。
    ///
    /// AR Foundation 的 ARAnchor 只在当前会话有效，应用重启后世界原点会重置，
    /// 因此这里用“现场基准图”（image tracking）作为真实世界的坐标原点：
    /// 所有问题标记都保存为基准图的局部坐标，下次启动重新识别到同一张图时即可还原。
    /// </summary>
    public class SiteAnchorTracker : IDisposable
    {
        public const string TargetName = "SiteAnchor";

        private readonly ARTrackedImageManager imageManager;
        private readonly Transform root;
        private ARTrackedImage trackedImage;
        private bool localized;

        public Transform Root { get { return root; } }
        public bool IsLocalized { get { return localized; } }
        public bool IsImageVisible { get { return trackedImage != null; } }

        /// <summary>首次定位成功时触发一次。</summary>
        public event Action Localized;

        public SiteAnchorTracker(ARTrackedImageManager manager, string rootName)
        {
            imageManager = manager;
            GameObject rootGo = new GameObject(string.IsNullOrEmpty(rootName) ? "Site Anchor Root" : rootName);
            root = rootGo.transform;

            if (imageManager != null)
            {
                imageManager.trackedImagesChanged += OnTrackedImagesChanged;
            }
        }

        /// <summary>每帧把基准图根节点对齐到当前追踪到的图像位姿。</summary>
        public void Tick()
        {
            if (trackedImage == null) return;
            root.SetPositionAndRotation(trackedImage.transform.position, trackedImage.transform.rotation);
        }

        public bool TryToLocal(Pose worldPose, out Vector3 localPosition, out Quaternion localRotation)
        {
            if (!localized)
            {
                localPosition = Vector3.zero;
                localRotation = Quaternion.identity;
                return false;
            }

            localPosition = root.InverseTransformPoint(worldPose.position);
            localRotation = Quaternion.Inverse(root.rotation) * worldPose.rotation;
            return true;
        }

        /// <summary>编辑器 / Windows 预览模式使用固定基准点。</summary>
        public void SetSimulatedSite(Vector3 position, Quaternion rotation)
        {
            root.SetPositionAndRotation(position, rotation);
            MarkLocalized();
        }

        private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs args)
        {
            for (int i = 0; i < args.added.Count; i++) Consider(args.added[i]);
            for (int i = 0; i < args.updated.Count; i++) Consider(args.updated[i]);

            for (int i = 0; i < args.removed.Count; i++)
            {
                if (trackedImage != null && args.removed[i].trackableId == trackedImage.trackableId)
                {
                    trackedImage = null;
                }
            }
        }

        private void Consider(ARTrackedImage image)
        {
            if (image == null) return;
            if (image.referenceImage == null) return;
            if (image.referenceImage.name != TargetName) return;
            if (image.trackingState != TrackingState.Tracking) return;

            trackedImage = image;
            root.SetPositionAndRotation(image.transform.position, image.transform.rotation);
            MarkLocalized();
        }

        private void MarkLocalized()
        {
            if (localized) return;
            localized = true;
            if (Localized != null) Localized();
        }

        public void Dispose()
        {
            if (imageManager != null)
            {
                imageManager.trackedImagesChanged -= OnTrackedImagesChanged;
            }
        }
    }
}
