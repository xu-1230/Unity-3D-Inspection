using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VirtualInspection.AR
{
    /// <summary>
    /// 问题标记的本地存档：写入 Application.persistentDataPath/issue-markers.json。
    /// 写入失败会返回错误原因，调用方据此给出明确的失败提示并回滚。
    /// </summary>
    public class IssueMarkerStore
    {
        public const string FileName = "issue-markers.json";

        private readonly List<IssueMarkerRecord> markers = new List<IssueMarkerRecord>();

        public IReadOnlyList<IssueMarkerRecord> Markers { get { return markers; } }
        public string LoadWarning { get; private set; }

        public string FilePath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        public void Load()
        {
            markers.Clear();
            LoadWarning = null;

            try
            {
                if (!File.Exists(FilePath)) return;

                string json = File.ReadAllText(FilePath);
                IssueMarkerCollection collection = JsonUtility.FromJson<IssueMarkerCollection>(json);
                if (collection != null && collection.markers != null)
                {
                    markers.AddRange(collection.markers);
                }
            }
            catch (Exception exception)
            {
                LoadWarning = exception.Message;
                Debug.LogWarning("[AR 巡检] 读取本地标记失败：" + exception);
            }
        }

        public bool TryAdd(IssueMarkerRecord record, out string error)
        {
            markers.Add(record);
            if (TrySave(out error)) return true;

            markers.Remove(record);
            return false;
        }

        public bool TrySave(out string error)
        {
            error = null;

            try
            {
                string directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                IssueMarkerCollection collection = new IssueMarkerCollection();
                collection.markers = new List<IssueMarkerRecord>(markers);

                string json = JsonUtility.ToJson(collection, true);
                string temporaryPath = FilePath + ".tmp";

                File.WriteAllText(temporaryPath, json);
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(temporaryPath, FilePath);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                Debug.LogError("[AR 巡检] 保存本地标记失败：" + exception);
                return false;
            }
        }
    }
}
