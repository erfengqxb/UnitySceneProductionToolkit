using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using SceneProductionToolkit.Data;
using SceneProductionToolkit.EditorCommon;

namespace SceneProductionToolkit.Editor
{
    public class PathEditorWindow : EditorWindow
    {
        private const string WINDOW_TITLE = "路径编辑器";

        private RecordingData editingData;
        private Vector2 scroll;
        private int selectedKeyframeIndex = -1;

        [MenuItem("Window/Production Tools/Path Editor", false, 1002)]
        public static void ShowWindow()
        {
            var w = GetWindow<PathEditorWindow>(WINDOW_TITLE);
            w.minSize = new Vector2(350, 400);
            w.Show();
        }

        private void OnEnable() { SceneView.duringSceneGui += OnSceneView; }
        private void OnDisable() { SceneView.duringSceneGui -= OnSceneView; }

        private void OnGUI()
        {
            EditorGuiUtils.DrawHeader("路径编辑器", "可视化编辑相机路径关键帧");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("路径数据:", GUILayout.Width(70));
            EditorGUILayout.LabelField(editingData != null ? editingData.pathName : "(无)", EditorStyles.boldLabel);
            if (GUILayout.Button("从录制加载", GUILayout.Width(90))) LoadFromRecorder();
            EditorGUILayout.EndHorizontal();

            if (editingData == null)
            {
                EditorGUILayout.HelpBox("请从录制工具加载一个路径数据进行编辑", MessageType.Info);
                return;
            }
            if (editingData.keyframes == null || editingData.keyframes.Count == 0)
            {
                EditorGUILayout.HelpBox("路径数据为空", MessageType.Info);
                return;
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawKeyframeList();
            DrawKeyframeEditor();
            DrawPathTools();
            EditorGUILayout.EndScrollView();
        }

        private void OnSceneView(SceneView sv)
        {
            if (editingData?.keyframes == null) return;
            var keys = editingData.keyframes;

            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10, 10, 180, 50), GUI.skin.box);
            EditorGUILayout.LabelField($"关键帧: {keys.Count}", EditorStyles.boldLabel);
            GUILayout.EndArea();
            Handles.EndGUI();

            Handles.color = new Color(0.3f, 0.7f, 1f, 0.5f);
            for (int i = 0; i < keys.Count - 1; i++)
                if (keys[i].isKey || keys[i + 1].isKey) Handles.DrawLine(keys[i].position, keys[i + 1].position);

            for (int i = 0; i < keys.Count; i++)
            {
                if (!keys[i].isKey) continue;
                float size = (i == selectedKeyframeIndex) ? 0.25f : 0.12f;
                Handles.color = (i == selectedKeyframeIndex) ? Color.green : Color.yellow;
                Handles.SphereHandleCap(0, keys[i].position, Quaternion.identity, size, EventType.Repaint);

                Handles.BeginGUI();
                Vector3 sp = sv.camera.WorldToScreenPoint(keys[i].position + Vector3.up * 0.5f);
                if (sp.z > 0)
                    GUI.Label(new Rect(sp.x - 10, sv.position.height - sp.y - 10, 30, 20), $"K{i}", EditorStyles.boldLabel);
                Handles.EndGUI();

                if (i == selectedKeyframeIndex)
                {
                    EditorGUI.BeginChangeCheck();
                    Vector3 np = Handles.PositionHandle(keys[i].position, keys[i].rotation);
                    if (EditorGUI.EndChangeCheck())
                    {
                        keys[i] = new CameraKeyframe { time = keys[i].time, position = np, rotation = keys[i].rotation, fov = keys[i].fov, isKey = true, label = keys[i].label };
                        Repaint();
                    }
                }
            }
        }

        private void DrawKeyframeList()
        {
            EditorGuiUtils.DrawFoldoutSection("📋 关键帧列表", true, () =>
            {
                for (int i = 0; i < editingData.keyframes.Count; i++)
                {
                    var kf = editingData.keyframes[i];
                    if (!kf.isKey) continue;
                    EditorGUILayout.BeginHorizontal();
                    GUI.backgroundColor = (i == selectedKeyframeIndex) ? Color.blue : Color.grey;
                    if (GUILayout.Button($"K{i}", GUILayout.Width(30))) { selectedKeyframeIndex = i; SceneView.RepaintAll(); }
                    GUI.backgroundColor = Color.white;
                    EditorGUILayout.LabelField($"t={kf.time:F2}s", GUILayout.Width(70));
                    EditorGUILayout.LabelField($"({kf.position.x:F1}, {kf.position.y:F1}, {kf.position.z:F1})");
                    if (!string.IsNullOrEmpty(kf.label)) EditorGUILayout.LabelField($"[{kf.label}]", EditorStyles.miniLabel);
                    if (GUILayout.Button("×", GUILayout.Width(20))) { editingData.keyframes.RemoveAt(i); if (selectedKeyframeIndex == i) selectedKeyframeIndex = -1; Repaint(); }
                    EditorGUILayout.EndHorizontal();
                }
            });
        }

        private void DrawKeyframeEditor()
        {
            if (selectedKeyframeIndex < 0 || selectedKeyframeIndex >= editingData.keyframes.Count) return;

            EditorGuiUtils.DrawFoldoutSection("✏️ 选中关键帧编辑", true, () =>
            {
                var kf = editingData.keyframes[selectedKeyframeIndex];
                EditorGUI.indentLevel++;

                Vector3 np = EditorGUILayout.Vector3Field("位置", kf.position);
                if (Vector3.Distance(np, kf.position) > 0.001f) editingData.keyframes[selectedKeyframeIndex] = ModKey(kf, pos: np);

                Vector3 eu = kf.rotation.eulerAngles;
                Vector3 ne = EditorGUILayout.Vector3Field("旋转", eu);
                if (Vector3.Distance(ne, eu) > 0.01f) editingData.keyframes[selectedKeyframeIndex] = ModKey(kf, rot: Quaternion.Euler(ne));

                float nf = EditorGUILayout.Slider("视野 (FOV)", kf.fov, 10f, 160f);
                if (Mathf.Abs(nf - kf.fov) > 0.1f) editingData.keyframes[selectedKeyframeIndex] = ModKey(kf, fov: nf);

                string nl = EditorGUILayout.TextField("标注", kf.label ?? "");
                if (nl != kf.label) editingData.keyframes[selectedKeyframeIndex] = ModKey(kf, label: nl);

                EditorGUI.indentLevel--;
            });
        }

        private void DrawPathTools()
        {
            EditorGuiUtils.DrawFoldoutSection("🔧 路径工具", true, () =>
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("平滑路径", GUILayout.Height(24))) SmoothPath();
                if (GUILayout.Button("简化关键帧", GUILayout.Height(24))) SimplifyKeys();
                if (GUILayout.Button("反转路径", GUILayout.Height(24))) ReversePath();
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button("按时间重采样", GUILayout.Height(24))) ResamplePath();
            });
        }

        // ====== 工具方法 ======

        private void LoadFromRecorder()
        {
            var r = FindObjectOfType<CameraPathRecorder>();
            if (r != null && r.CurrentRecording != null) editingData = r.CurrentRecording;
            else EditorUtility.DisplayDialog("提示", "场景中没有活动的录制数据", "确定");
        }

        private CameraKeyframe ModKey(CameraKeyframe orig, Vector3? pos = null, Quaternion? rot = null, float? fov = null, string label = null)
            => new CameraKeyframe { time = orig.time, position = pos ?? orig.position, rotation = rot ?? orig.rotation, fov = fov ?? orig.fov, nearClip = orig.nearClip, farClip = orig.farClip, isKey = orig.isKey, label = label ?? orig.label };

        private void SmoothPath()
        {
            if (editingData.keyframes.Count < 3) return;
            var keys = editingData.keyframes;
            var smoothed = new Vector3[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                if (!keys[i].isKey) { smoothed[i] = keys[i].position; continue; }
                int prev = i, next = i;
                for (int j = i - 1; j >= 0; j--) { if (keys[j].isKey) { prev = j; break; } }
                for (int j = i + 1; j < keys.Count; j++) { if (keys[j].isKey) { next = j; break; } }
                smoothed[i] = (prev == i || next == i) ? keys[i].position : (keys[prev].position + keys[i].position * 2f + keys[next].position) / 4f;
            }
            for (int i = 0; i < keys.Count; i++) keys[i] = ModKey(keys[i], pos: smoothed[i]);
            Debug.Log("[路径编辑] 已平滑");
        }

        private void SimplifyKeys()
        {
            if (editingData.keyframes.Count < 3) return;
            float md = EditorUtility.DisplayDialogComplex("简化关键帧", "删除距离小于多少单位的关键帧?", "0.5", "1.0", "2.0") switch { 0 => 0.5f, 1 => 1.0f, _ => 2.0f };
            int removed = 0;
            for (int i = editingData.keyframes.Count - 2; i >= 1; i--)
            {
                var prev = editingData.keyframes[i - 1];
                var cur = editingData.keyframes[i];
                var next = editingData.keyframes[i + 1];
                if (cur.isKey && Vector3.Distance(cur.position, prev.position) < md && Vector3.Distance(cur.position, next.position) < md)
                { editingData.keyframes.RemoveAt(i); removed++; }
            }
            Debug.Log($"[路径编辑] 简化完成，移除 {removed} 个关键帧");
        }

        private void ReversePath()
        {
            if (editingData.keyframes.Count < 2) return;
            var keys = editingData.keyframes;
            float total = keys[keys.Count - 1].time;
            editingData.keyframes.Reverse();
            for (int i = 0; i < keys.Count; i++)
            {
                var k = keys[i];
                keys[i] = new CameraKeyframe { time = total - k.time, position = k.position, rotation = k.rotation, fov = k.fov, nearClip = k.nearClip, farClip = k.farClip, isKey = k.isKey, label = k.label };
            }
            Debug.Log("[路径编辑] 已反转");
        }

        private void ResamplePath()
        {
            if (editingData.keyframes.Count < 2) return;
            const string prefKey = "PathEditor_SampleCount";
            int defaultCount = EditorPrefs.GetInt(prefKey, 30);
            if (!EditorUtility.DisplayDialog("重采样", $"将路径重采样为指定帧数。\n当前: {editingData.keyframes.Count} 帧\n\n使用 {defaultCount} 帧?", "确定", "取消"))
                return;
            int count = defaultCount;

            var keys = editingData.keyframes;
            float total = keys[keys.Count - 1].time;
            float interval = total / (count - 1);
            var newKeys = new List<CameraKeyframe>();

            for (int i = 0; i < count; i++)
            {
                float t = i * interval;
                int a = 0, b = 0;
                for (int j = 0; j < keys.Count - 1; j++)
                    if (t >= keys[j].time && t <= keys[j + 1].time) { a = j; b = j + 1; break; }

                float seg = keys[b].time - keys[a].time;
                float lerp = seg > 0 ? Mathf.Clamp01((t - keys[a].time) / seg) : 0;
                newKeys.Add(new CameraKeyframe
                {
                    time = t,
                    position = Vector3.Lerp(keys[a].position, keys[b].position, lerp),
                    rotation = Quaternion.Slerp(keys[a].rotation, keys[b].rotation, lerp),
                    fov = Mathf.Lerp(keys[a].fov, keys[b].fov, lerp),
                    isKey = i == 0 || i == count - 1 || i % 5 == 0,
                    label = $"RS{i}"
                });
            }
            editingData.keyframes = newKeys;
            Debug.Log($"[路径编辑] 重采样完成: {count} 帧");
        }
    }
}
