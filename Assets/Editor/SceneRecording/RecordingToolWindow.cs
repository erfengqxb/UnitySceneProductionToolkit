using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SceneProductionToolkit.Runtime;
using SceneProductionToolkit.Data;
using SceneProductionToolkit.EditorCommon;

namespace SceneProductionToolkit.Editor
{
    public class RecordingToolWindow : EditorWindow
    {
        private const string WINDOW_TITLE = "场景录制工具";
        private const string PREFS = "RecordingTool_";
        private const string PRESET_DIR = "Assets/Resources/录制预设";

        private Vector2 scroll;
        private CameraPathRecorder activeRecorder;
        private CameraPathPlayer activePlayer;

        private bool isRecording;
        private string pathName = "新路径";
        private float captureRate = 30f;
        private bool autoKeyframe = true;
        private float autoKeyInterval = 0.5f;

        private bool isPlaying;
        private string[] playbackModes = { "单次", "循环", "往返" };
        private int selPlayMode = 2;
        private string[] interpModes = { "线性", "平滑", "CatmullRom", "三次样条", "Hermite" };
        private int selInterp = 2;
        private float playbackSpeed = 1f;
        private bool useEasing = true;
        private bool lookAtTarget;
        private bool enableShake;

        private GameObject selectedNpcPrefab;
        private string npcId = "NPC";
        private Vector3 npcPos = Vector3.zero;
        private Vector3 npcRot = Vector3.zero;
        private string selAnim = "Idle";
        private readonly string[] animStates = { "Idle", "Walking", "Running", "LookingAtCamera", "Talking" };

        private string presetName = "";
        private Vector2 presetScroll;
        private List<RecordingData> savedRecordings = new List<RecordingData>();

        [MenuItem("Window/Production Tools/Scene Recording", false, 1001)]
        public static void ShowWindow()
        {
            var w = GetWindow<RecordingToolWindow>(WINDOW_TITLE);
            w.minSize = new Vector2(400, 500);
            w.Show();
        }

        private void OnEnable() { LoadPrefs(); RefreshRefs(); SceneView.duringSceneGui += OnSceneGUI; }
        private void OnDisable() { SceneView.duringSceneGui -= OnSceneGUI; SavePrefs(); }

        private void OnGUI()
        {
            EditorGuiUtils.DrawHeader("场景录制工具", "相机路径录制 / 回放 · NPC 放置 · 录制预设");

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("刷新引用", EditorStyles.toolbarButton, GUILayout.Width(80))) RefreshRefs();
            if (GUILayout.Button("创建录制相机", EditorStyles.toolbarButton, GUILayout.Width(100))) CreateCam();
            GUILayout.FlexibleSpace();
            EditorGuiUtils.DrawTimestamp();
            EditorGUILayout.EndHorizontal();

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawRecorder();
            DrawPlayer();
            DrawNpc();
            DrawPresets();
            EditorGUILayout.EndScrollView();
        }

        private void OnSceneGUI(SceneView sv)
        {
            if (activeRecorder == null && activePlayer == null) return;
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 200, 80), GUI.skin.box);
            if (isRecording) { EditorGUILayout.LabelField("🔴 录制中"); EditorGUILayout.LabelField($"帧: {activeRecorder?.FrameCount ?? 0}"); EditorGUILayout.LabelField($"时长: {activeRecorder?.RecordedDuration:F1}s"); }
            else if (isPlaying) { EditorGUILayout.LabelField("▶️ 播放中"); EditorGUILayout.LabelField($"进度: {activePlayer?.Progress:P0}"); }
            GUILayout.EndArea();
            Handles.EndGUI();

            var data = activeRecorder?.CurrentRecording ?? activePlayer?.LoadedData;
            if (data?.keyframes != null && data.keyframes.Count >= 2)
            {
                var keys = data.keyframes;
                Handles.color = new Color(0.2f, 0.8f, 1f, 0.6f);
                for (int i = 0; i < keys.Count - 1; i++)
                    if (keys[i].isKey || keys[i + 1].isKey) Handles.DrawLine(keys[i].position, keys[i + 1].position);
                foreach (var kf in keys)
                {
                    if (!kf.isKey) continue;
                    Handles.color = Color.yellow;
                    Handles.SphereHandleCap(0, kf.position, Quaternion.identity, 0.15f, EventType.Repaint);
                }
            }
        }

        private void DrawRecorder()
        {
            EditorGuiUtils.DrawFoldoutSection("🎥 相机录制", true, () =>
            {
                activeRecorder = (CameraPathRecorder)EditorGUILayout.ObjectField("录制相机", activeRecorder, typeof(CameraPathRecorder), true);
                if (activeRecorder == null) { EditorGUILayout.HelpBox("请选择或创建一个带有 CameraPathRecorder 组件的相机", MessageType.Info); return; }

                EditorGUI.indentLevel++;
                pathName = EditorGUILayout.TextField("路径名称", pathName);
                captureRate = EditorGUILayout.FloatField("录制帧率", captureRate);
                autoKeyframe = EditorGUILayout.Toggle("自动关键帧", autoKeyframe);
                if (autoKeyframe) autoKeyInterval = EditorGUILayout.FloatField("关键帧间隔(秒)", autoKeyInterval);

                GUI.enabled = !isRecording;
                if (GUILayout.Button("开始录制", GUILayout.Height(30))) { activeRecorder.StartRecording(pathName); isRecording = true; }
                GUI.enabled = isRecording;
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("插关键帧", GUILayout.Height(24))) activeRecorder?.InsertManualKeyframe($"KF_{activeRecorder.CurrentRecording?.keyframes.Count ?? 0}");
                if (GUILayout.Button("撤销关键帧", GUILayout.Height(24))) activeRecorder?.UndoKeyframes();
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("停止录制", GUILayout.Height(30))) StopRec();
                GUI.enabled = true;
                EditorGUI.indentLevel--;

                if (isRecording) EditorGuiUtils.DrawInfoBox("🔴 录制中 — 在 Scene 视图中移动相机", MessageType.Warning);
            });
        }

        private void DrawPlayer()
        {
            EditorGuiUtils.DrawFoldoutSection("▶️ 路径回放", true, () =>
            {
                activePlayer = (CameraPathPlayer)EditorGUILayout.ObjectField("回放相机", activePlayer, typeof(CameraPathPlayer), true);
                if (activePlayer == null) { EditorGUILayout.HelpBox("请选择带有 CameraPathPlayer 组件的相机", MessageType.Info); return; }

                EditorGUI.indentLevel++;
                if (savedRecordings.Count > 0)
                {
                    string[] names = savedRecordings.ConvertAll(r => r.pathName).ToArray();
                    int sel = EditorGUILayout.Popup("选择路径", 0, names);
                    if (GUILayout.Button("加载")) activePlayer.LoadRecording(savedRecordings[sel]);
                }

                selPlayMode = EditorGUILayout.Popup("播放模式", selPlayMode, playbackModes);
                selInterp = EditorGUILayout.Popup("插值模式", selInterp, interpModes);
                playbackSpeed = EditorGUILayout.Slider("播放速度", playbackSpeed, 0.1f, 5f);
                useEasing = EditorGUILayout.Toggle("缓动", useEasing);

                GUI.enabled = activePlayer.LoadedData != null;
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(isPlaying ? "⏸ 暂停" : "▶ 播放", GUILayout.Height(28))) TogglePlay();
                if (GUILayout.Button("⏹ 停止", GUILayout.Height(28))) StopPlay();
                EditorGUILayout.EndHorizontal();
                if (activePlayer.LoadedData != null)
                {
                    float p = EditorGUILayout.Slider("进度", activePlayer.Progress, 0f, 1f);
                    activePlayer.Seek(p * activePlayer.TotalDuration);
                }
                GUI.enabled = true;
                EditorGUI.indentLevel--;
            });
        }

        private void DrawNpc()
        {
            EditorGuiUtils.DrawFoldoutSection("🧑 NPC 放置", false, () =>
            {
                selectedNpcPrefab = (GameObject)EditorGUILayout.ObjectField("NPC Prefab", selectedNpcPrefab, typeof(GameObject), false);
                npcId = EditorGUILayout.TextField("NPC ID", npcId);
                npcPos = EditorGUILayout.Vector3Field("位置", npcPos);
                npcRot = EditorGUILayout.Vector3Field("旋转", npcRot);

                int ai = System.Array.IndexOf(animStates, selAnim);
                ai = ai < 0 ? 0 : ai;
                ai = EditorGUILayout.Popup("初始动画", ai, animStates);
                selAnim = animStates[ai];

                if (GUILayout.Button("放置 NPC 到场景", GUILayout.Height(30))) PlaceNpc();
            });
        }

        private void DrawPresets()
        {
            EditorGuiUtils.DrawFoldoutSection("💾 预设管理", false, () =>
            {
                EditorGUILayout.BeginHorizontal();
                presetName = EditorGUILayout.TextField("预设名称", presetName);
                if (GUILayout.Button("保存预设", GUILayout.Width(80))) SavePreset();
                EditorGUILayout.EndHorizontal();

                if (Directory.Exists(PRESET_DIR))
                {
                    string[] files = Directory.GetFiles(PRESET_DIR, "*.json");
                    if (files.Length > 0)
                    {
                        presetScroll = EditorGUILayout.BeginScrollView(presetScroll, GUILayout.MaxHeight(120));
                        foreach (string f in files)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.LabelField(Path.GetFileNameWithoutExtension(f));
                            if (GUILayout.Button("加载", GUILayout.Width(50))) LoadPresetFile(f);
                            if (GUILayout.Button("删除", GUILayout.Width(50))) { File.Delete(f); Repaint(); }
                            EditorGUILayout.EndHorizontal();
                        }
                        EditorGUILayout.EndScrollView();
                    }
                }

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("导出录制数据")) ExportData();
                if (GUILayout.Button("导入录制数据")) ImportData();
                EditorGUILayout.EndHorizontal();
            });
        }

        private void RefreshRefs()
        {
            var rs = FindObjectsOfType<CameraPathRecorder>();
            if (rs.Length > 0 && activeRecorder == null) activeRecorder = rs[0];
            var ps = FindObjectsOfType<CameraPathPlayer>();
            if (ps.Length > 0 && activePlayer == null) activePlayer = ps[0];
        }

        private void CreateCam()
        {
            var obj = new GameObject("录制相机");
            obj.AddComponent<Camera>();
            obj.AddComponent<CameraPathRecorder>();
            obj.AddComponent<CameraPathPlayer>();
            if (SceneView.lastActiveSceneView != null)
            {
                obj.transform.position = SceneView.lastActiveSceneView.camera.transform.position;
                obj.transform.rotation = SceneView.lastActiveSceneView.camera.transform.rotation;
            }
            Selection.activeGameObject = obj;
            activeRecorder = obj.GetComponent<CameraPathRecorder>();
            activePlayer = obj.GetComponent<CameraPathPlayer>();
        }

        private void StopRec()
        {
            if (activeRecorder == null) return;
            var data = activeRecorder.StopRecording();
            if (data != null) { savedRecordings.Add(data); if (activePlayer != null) activePlayer.LoadRecording(data); }
            isRecording = false;
        }

        private void TogglePlay()
        {
            if (activePlayer == null) return;
            if (isPlaying) { activePlayer.TogglePause(); isPlaying = !activePlayer.IsPaused; }
            else { activePlayer.SetPlaybackSpeed(playbackSpeed); activePlayer.SetInterpolationMode((InterpolationMode)selInterp); activePlayer.Play(); isPlaying = true; }
        }

        private void StopPlay() { if (activePlayer != null) { activePlayer.Stop(); isPlaying = false; } }

        private void PlaceNpc()
        {
            GameObject obj;
            if (selectedNpcPrefab == null)
            {
                obj = new GameObject(npcId);
                obj.transform.position = npcPos;
                obj.transform.rotation = Quaternion.Euler(npcRot);
                obj.AddComponent<Animator>();
                var c = obj.AddComponent<NpcController>();
                int ai = System.Array.IndexOf(animStates, selAnim);
                if (ai >= 0 && ai < 5) c.SetState((NpcController.NpcState)ai);
            }
            else
            {
                obj = (GameObject)PrefabUtility.InstantiatePrefab(selectedNpcPrefab);
                obj.name = npcId;
                obj.transform.position = npcPos;
                obj.transform.rotation = Quaternion.Euler(npcRot);
            }
            Selection.activeGameObject = obj;
        }

        private void SavePreset()
        {
            if (string.IsNullOrEmpty(presetName)) { EditorUtility.DisplayDialog("提示", "请输入预设名称", "确定"); return; }
            Directory.CreateDirectory(PRESET_DIR);
            var p = new RecordingPreset { presetName = presetName, defaultCaptureRate = captureRate, autoKeyframeInterval = autoKeyInterval, playbackSpeed = playbackSpeed };
            File.WriteAllText(Path.Combine(PRESET_DIR, presetName + ".json"), JsonUtility.ToJson(p, true));
        }

        private void LoadPresetFile(string path)
        {
            if (!File.Exists(path)) return;
            var p = JsonUtility.FromJson<RecordingPreset>(File.ReadAllText(path));
            if (p != null) { presetName = p.presetName; captureRate = p.defaultCaptureRate; playbackSpeed = p.playbackSpeed; }
        }

        private void ExportData()
        {
            var data = activeRecorder?.CurrentRecording ?? activePlayer?.LoadedData;
            if (data == null) { EditorUtility.DisplayDialog("提示", "没有可导出的录制数据", "确定"); return; }
            string path = EditorUtility.SaveFilePanel("导出录制数据", "", data.pathName, "json");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }

        private void ImportData()
        {
            string path = EditorUtility.OpenFilePanel("导入录制数据", "", "json");
            if (string.IsNullOrEmpty(path)) return;
            var data = JsonUtility.FromJson<RecordingData>(File.ReadAllText(path));
            if (data == null) { EditorUtility.DisplayDialog("错误", "格式不正确", "确定"); return; }
            savedRecordings.Add(data);
            if (activePlayer != null) activePlayer.LoadRecording(data);
        }

        private void LoadPrefs()
        {
            pathName = EditorPrefs.GetString(PREFS + "PathName", "新路径");
            captureRate = EditorPrefs.GetFloat(PREFS + "Rate", 30f);
            playbackSpeed = EditorPrefs.GetFloat(PREFS + "Speed", 1f);
            selPlayMode = EditorPrefs.GetInt(PREFS + "PlayMode", 2);
        }
        private void SavePrefs()
        {
            EditorPrefs.SetString(PREFS + "PathName", pathName);
            EditorPrefs.SetFloat(PREFS + "Rate", captureRate);
            EditorPrefs.SetFloat(PREFS + "Speed", playbackSpeed);
            EditorPrefs.SetInt(PREFS + "PlayMode", selPlayMode);
        }
    }
}
