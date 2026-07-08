using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SceneProductionToolkit.Runtime;
using SceneProductionToolkit.EditorCommon;

namespace SceneProductionToolkit.Editor
{
    public class BatchExportWindow : EditorWindow
    {
        private const string WINDOW_TITLE = "批量导出工具";
        private const string PREFS = "BatchExport_";

        private bool exportScreenshots = true;
        private bool exportSceneConfig = true;
        private bool exportRecordingData = true;
        private bool exportInspectionReport = true;

        private int screenshotWidth = 1920;
        private int screenshotHeight = 1080;
        private int supersample = 1;
        private string[] screenshotAngles = { "正面", "背面", "左侧", "右侧", "俯视" };
        private bool screenshotTransparent;

        private string exportDirectory = "导出结果";
        private bool includeTimestamp = true;

        private List<string> completedExports = new List<string>();
        private bool isExporting;
        private float exportProgress;
        private string currentExportName;

        private int exportTaskDone;
        private int exportTaskTotal;
        private string exportOutDir;
        private string exportSceneName;

        private Camera screenshotCamera;

        [MenuItem("Window/Production Tools/Batch Export", false, 1004)]
        public static void ShowWindow()
        {
            var w = GetWindow<BatchExportWindow>(WINDOW_TITLE);
            w.minSize = new Vector2(450, 500);
            w.Show();
        }

        private void OnEnable() { LoadPrefs(); }
        private void OnDisable() { SavePrefs(); }

        private Vector2 scroll;
        private Vector2 resultScroll;

        private void OnGUI()
        {
            EditorGuiUtils.DrawHeader("批量导出工具", "场景截图 · JSON 配置 · 录制数据 · 检查报告");
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawOptions();
            DrawCameraSettings();
            DrawPath();
            DrawExportBtn();
            DrawProgress();
            DrawResults();
            EditorGUILayout.EndScrollView();
        }

        private void DrawOptions()
        {
            EditorGuiUtils.DrawFoldoutSection("📦 导出选项", true, () =>
            {
                exportScreenshots = EditorGUILayout.Toggle("场景截图", exportScreenshots);
                exportSceneConfig = EditorGUILayout.Toggle("场景 JSON 配置", exportSceneConfig);
                exportRecordingData = EditorGUILayout.Toggle("录制数据", exportRecordingData);
                exportInspectionReport = EditorGUILayout.Toggle("检查报告", exportInspectionReport);
            });
        }

        private void DrawCameraSettings()
        {
            if (!exportScreenshots) return;
            EditorGuiUtils.DrawFoldoutSection("📷 截图设置", true, () =>
            {
                screenshotCamera = (Camera)EditorGUILayout.ObjectField("截图相机", screenshotCamera, typeof(Camera), true);
                EditorGUI.indentLevel++;
                screenshotWidth = EditorGUILayout.IntField("宽度", screenshotWidth);
                screenshotHeight = EditorGUILayout.IntField("高度", screenshotHeight);
                supersample = EditorGUILayout.IntPopup("超采样", supersample, new[] { "1x", "2x", "4x" }, new[] { 1, 2, 4 });
                screenshotTransparent = EditorGUILayout.Toggle("透明背景", screenshotTransparent);
                EditorGUI.indentLevel--;

                EditorGUILayout.LabelField("导出角度:", EditorStyles.boldLabel);
                for (int i = 0; i < screenshotAngles.Length; i++)
                    screenshotAngles[i] = EditorGUILayout.TextField($"角度 {i + 1}", screenshotAngles[i]);
            });
        }

        private void DrawPath()
        {
            EditorGuiUtils.DrawFoldoutSection("📁 输出路径", true, () =>
            {
                EditorGUILayout.BeginHorizontal();
                exportDirectory = EditorGUILayout.TextField("导出目录", exportDirectory);
                if (GUILayout.Button("浏览", GUILayout.Width(60)))
                {
                    string sel = EditorUtility.OpenFolderPanel("选择导出目录", Path.Combine(Application.dataPath, exportDirectory), "");
                    if (!string.IsNullOrEmpty(sel))
                        exportDirectory = sel.StartsWith(Application.dataPath) ? "Assets" + sel.Substring(Application.dataPath.Length) : sel;
                }
                EditorGUILayout.EndHorizontal();
                includeTimestamp = EditorGUILayout.Toggle("文件夹名加时间戳", includeTimestamp);
            });
        }

        private void DrawExportBtn()
        {
            EditorGUILayout.Space(6);
            GUI.enabled = !isExporting;
            if (GUILayout.Button("🚀 开始导出", GUILayout.Height(36))) StartExport();
            GUI.enabled = true;
        }

        private void DrawProgress()
        {
            if (!isExporting && exportProgress <= 0f) return;
            EditorGuiUtils.DrawSeparator();
            if (isExporting)
            {
                EditorGUILayout.LabelField($"正在导出: {currentExportName}", EditorStyles.boldLabel);
                Rect r = EditorGUILayout.BeginVertical();
                EditorGUI.ProgressBar(r, exportProgress, $"{exportProgress:P1}");
                GUILayout.Space(20);
                EditorGUILayout.EndVertical();
                Repaint();
            }
            else EditorGuiUtils.DrawInfoBox($"✅ 导出完成! 共 {completedExports.Count} 项", MessageType.Info);
        }

        private void DrawResults()
        {
            if (completedExports.Count == 0) return;
            EditorGuiUtils.DrawFoldoutSection("📄 导出结果", true, () =>
            {
                resultScroll = EditorGUILayout.BeginScrollView(resultScroll, GUILayout.MaxHeight(150));
                foreach (var item in completedExports)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("✅ " + item);
                    if (GUILayout.Button("打开", GUILayout.Width(50)))
                    {
                        string full = Path.GetFullPath(item);
                        if (File.Exists(full) || Directory.Exists(full)) EditorUtility.RevealInFinder(full);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
                if (GUILayout.Button("清空结果")) completedExports.Clear();
            });
        }

        private void StartExport()
        {
            isExporting = true;
            exportProgress = 0f;
            completedExports.Clear();

            string ts = includeTimestamp ? $"_{System.DateTime.Now:yyyyMMdd_HHmmss}" : "";
            exportOutDir = exportDirectory.StartsWith("Assets")
                ? Path.Combine(Application.dataPath, exportDirectory.Substring(7) + ts)
                : Path.Combine(exportDirectory + ts);
            if (!exportOutDir.StartsWith(Application.dataPath) && !Path.IsPathRooted(exportOutDir))
                exportOutDir = Path.Combine(Application.dataPath, exportOutDir);
            Directory.CreateDirectory(exportOutDir);

            exportSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (string.IsNullOrEmpty(exportSceneName)) { EditorUtility.DisplayDialog("错误", "请先保存当前场景", "确定"); isExporting = false; return; }

            exportTaskTotal = 0;
            if (exportScreenshots) exportTaskTotal += screenshotAngles.Length;
            if (exportSceneConfig) exportTaskTotal++;
            if (exportRecordingData) exportTaskTotal++;
            if (exportInspectionReport) exportTaskTotal++;
            exportTaskDone = 0;

            EditorApplication.update += ProcessExport;
        }

        private void ProcessExport()
        {
            if (exportTaskDone >= exportTaskTotal)
            {
                EditorApplication.update -= ProcessExport;
                isExporting = false;
                exportProgress = 1f;
                Debug.Log($"[批量导出] 完成: {exportOutDir}");
                Repaint();
                return;
            }

            exportProgress = (float)exportTaskDone / exportTaskTotal;
            try
            {
                if (exportScreenshots && exportTaskDone < screenshotAngles.Length)
                {
                    string angle = screenshotAngles[exportTaskDone];
                    currentExportName = $"截图 {angle}";
                    string p = Path.Combine(exportOutDir, $"{exportSceneName}_{angle}.png");
                    CaptureScreenshot(p);
                    completedExports.Add(p);
                }
                else if (exportSceneConfig && exportTaskDone == (exportScreenshots ? screenshotAngles.Length : 0))
                {
                    currentExportName = "场景配置";
                    string p = Path.Combine(exportOutDir, $"{exportSceneName}_场景配置.json");
                    ExportSceneConfig(p, exportSceneName);
                    completedExports.Add(p);
                }
                else if (exportRecordingData && exportTaskDone == (exportScreenshots ? screenshotAngles.Length : 0) + (exportSceneConfig ? 1 : 0))
                {
                    currentExportName = "录制数据";
                    string p = Path.Combine(exportOutDir, $"{exportSceneName}_录制数据.json");
                    ExportRecordingData(p);
                    completedExports.Add(p);
                }
                else if (exportInspectionReport)
                {
                    currentExportName = "检查报告";
                    string p = Path.Combine(exportOutDir, $"{exportSceneName}_检查报告.md");
                    File.WriteAllText(p, $"# 场景检查报告: {exportSceneName}\n\n生成时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\n\n---\n*由批量导出工具自动生成*\n");
                    completedExports.Add(p);
                }
                exportTaskDone++;
            }
            catch (System.Exception e) { Debug.LogError($"[批量导出] 失败: {e.Message}"); exportTaskDone++; }
            Repaint();
        }

        private void CaptureScreenshot(string path)
        {
            var cam = screenshotCamera ?? Camera.main;
            if (cam == null) return;

            var origTarget = cam.targetTexture;
            var origClear = cam.clearFlags;
            var origBg = cam.backgroundColor;
            if (screenshotTransparent) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.clear; }

            int w = screenshotWidth * supersample;
            int h = screenshotHeight * supersample;
            var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();

            if (supersample > 1) { var r2 = Resize(tex, screenshotWidth, screenshotHeight); DestroyImmediate(tex); tex = r2; }

            File.WriteAllBytes(path, tex.EncodeToPNG());
            cam.targetTexture = origTarget;
            cam.clearFlags = origClear;
            cam.backgroundColor = origBg;
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            DestroyImmediate(tex);
        }

        private Texture2D Resize(Texture2D src, int w, int h)
        {
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            RenderTexture.active = rt;
            Graphics.Blit(src, rt);
            var r = new Texture2D(w, h, TextureFormat.RGBA32, false);
            r.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            r.Apply();
            RenderTexture.active = null;
            RenderTexture.ReleaseTemporary(rt);
            return r;
        }

        private void ExportSceneConfig(string path, string sceneName)
        {
            var l = FindObjectOfType<SceneConfigLoader>();
            string json = l != null ? l.ExportCurrentScene(sceneName) :
                JsonUtility.ToJson(new { sceneName, time = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), cameras = FindObjectsOfType<Camera>().Length, lights = FindObjectsOfType<Light>().Length }, true);
            File.WriteAllText(path, json);
        }

        private void ExportRecordingData(string path)
        {
            var data = FindObjectOfType<CameraPathRecorder>()?.CurrentRecording ?? FindObjectOfType<CameraPathPlayer>()?.LoadedData;
            File.WriteAllText(path, data != null ? JsonUtility.ToJson(data, true) : "{\"note\":\"没有录制数据\"}");
        }

        private void LoadPrefs()
        {
            exportScreenshots = EditorPrefs.GetBool(PREFS + "SS", true);
            exportSceneConfig = EditorPrefs.GetBool(PREFS + "SC", true);
            exportRecordingData = EditorPrefs.GetBool(PREFS + "RD", true);
            exportDirectory = EditorPrefs.GetString(PREFS + "Dir", "导出结果");
            screenshotWidth = EditorPrefs.GetInt(PREFS + "SW", 1920);
            screenshotHeight = EditorPrefs.GetInt(PREFS + "SH", 1080);
        }
        private void SavePrefs()
        {
            EditorPrefs.SetBool(PREFS + "SS", exportScreenshots);
            EditorPrefs.SetBool(PREFS + "SC", exportSceneConfig);
            EditorPrefs.SetBool(PREFS + "RD", exportRecordingData);
            EditorPrefs.SetString(PREFS + "Dir", exportDirectory);
            EditorPrefs.SetInt(PREFS + "SW", screenshotWidth);
            EditorPrefs.SetInt(PREFS + "SH", screenshotHeight);
        }
    }
}
