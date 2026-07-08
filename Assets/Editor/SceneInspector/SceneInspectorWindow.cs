using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SceneProductionToolkit.Data;
using SceneProductionToolkit.EditorCommon;
using Object = UnityEngine.Object;

namespace SceneProductionToolkit.Editor
{
    /// <summary>
    /// 场景检查工具 — 遮挡/穿模/视角/光照/材质/性能/Transform
    /// </summary>
    public class SceneInspectorWindow : EditorWindow
    {
        private const string WINDOW_TITLE = "场景检查工具";
        private const string PREFS_KEY = "SceneInspector_";
        private const string REPORT_DIR = "Assets/检查报告";

        private Vector2 scrollPosition;
        private Vector2 issueScrollPosition;
        private InspectorConfig config = new InspectorConfig();
        private SceneCheckResult lastResult;

        private bool isRunning;
        private float checkProgress;
        private string currentCheckName = "";

        private string issueFilter = "";
        private IssueSeverity minSeverity = IssueSeverity.Info;
        private bool autoFixEnabled;

        private int inspectionStep;
        private static readonly string[] StepNames =
        {
            "遮挡检查", "穿模检测", "视角检查",
            "光照检查", "材质检查", "性能检查", "变换检查"
        };

        [MenuItem("Window/Production Tools/Scene Inspector", false, 1003)]
        public static void ShowWindow()
        {
            var window = GetWindow<SceneInspectorWindow>(WINDOW_TITLE);
            window.minSize = new Vector2(500, 600);
            window.Show();
        }

        private void OnEnable() { LoadPrefs(); }
        private void OnDisable() { SavePrefs(); }

        private void OnGUI()
        {
            EditorGuiUtils.DrawHeader("场景检查工具", "遮挡 · 穿模 · 视角 · 光照 · 材质 · 性能 · 变换");

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            DrawConfig();
            DrawRunButton();
            DrawProgress();
            DrawSummary();
            DrawFilter();
            DrawIssues();
            DrawExport();

            EditorGUILayout.EndScrollView();
        }

        private void DrawConfig()
        {
            EditorGuiUtils.DrawFoldoutSection("⚙️ 检查配置", true, () =>
            {
                EditorGUILayout.LabelField("启用检查项:", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                foreach (CheckCategory cat in System.Enum.GetValues(typeof(CheckCategory)))
                {
                    bool enabled = config.checkEnabled.ContainsKey(cat) && config.checkEnabled[cat];
                    config.checkEnabled[cat] = EditorGUILayout.Toggle(GetCNLabel(cat), enabled);
                }
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("检查参数:", EditorStyles.boldLabel);
                EditorGUI.indentLevel++;
                config.maxDrawCallWarning = EditorGUILayout.IntField("DrawCall 警告阈值", config.maxDrawCallWarning);
                config.maxVertexWarning = EditorGUILayout.IntField("顶点数警告阈值", config.maxVertexWarning);
                config.minCameraDistance = EditorGUILayout.FloatField("相机最小距离", config.minCameraDistance);
                EditorGUI.indentLevel--;

                EditorGUILayout.Space(2);
                autoFixEnabled = EditorGUILayout.Toggle("🔄 启用自动修复", autoFixEnabled);
            });
        }

        private void DrawRunButton()
        {
            EditorGUILayout.Space(6);
            GUI.enabled = !isRunning;
            if (GUILayout.Button("🔍 开始全面检查", GUILayout.Height(36))) RunInspection();
            GUI.enabled = true;
        }

        private void DrawProgress()
        {
            if (!isRunning && checkProgress <= 0f) return;
            if (isRunning)
            {
                EditorGUILayout.LabelField($"检查中: {currentCheckName}", EditorStyles.boldLabel);
                Rect r = EditorGUILayout.BeginVertical();
                EditorGUI.ProgressBar(r, checkProgress, currentCheckName);
                GUILayout.Space(20);
                EditorGUILayout.EndVertical();
                Repaint();
            }
            else EditorGuiUtils.DrawInfoBox("✅ 检查完成", MessageType.Info);
        }

        private void DrawSummary()
        {
            if (lastResult == null) return;
            EditorGuiUtils.DrawSeparator();

            var s = lastResult.summary;
            float score = lastResult.overallScore;
            Color sc = score >= 80 ? Color.green : (score >= 60 ? Color.yellow : Color.red);

            Rect sr = EditorGUILayout.BeginVertical(GUILayout.Height(32));
            EditorGUI.DrawRect(sr, new Color(0.1f, 0.1f, 0.1f));
            EditorGUI.ProgressBar(new Rect(sr.x, sr.y, sr.width * score / 100f, sr.height), 1f, $"评分: {score:F0}/100");
            GUILayout.Space(32);
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginHorizontal();
            StatCard("通过", s.passed, new Color(0.1f, 0.6f, 0.1f, 0.3f));
            StatCard("警告", s.warnings, new Color(0.7f, 0.6f, 0.1f, 0.3f));
            StatCard("错误", s.errors, new Color(0.7f, 0.3f, 0.1f, 0.3f));
            StatCard("严重", s.criticalErrors, new Color(0.8f, 0.1f, 0.1f, 0.3f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFilter()
        {
            if (lastResult == null || lastResult.issues.Count == 0) return;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("筛选:", GUILayout.Width(40));
            minSeverity = (IssueSeverity)EditorGUILayout.EnumPopup(minSeverity, GUILayout.Width(80));
            issueFilter = EditorGUILayout.TextField(issueFilter, EditorStyles.toolbarSearchField);
            if (GUILayout.Button("清除", EditorStyles.miniButton, GUILayout.Width(50))) { issueFilter = ""; minSeverity = IssueSeverity.Info; }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawIssues()
        {
            if (lastResult == null || lastResult.issues.Count == 0) return;

            EditorGuiUtils.DrawFoldoutSection($"📋 问题列表 ({lastResult.issues.Count} 项)", true, () =>
            {
                issueScrollPosition = EditorGUILayout.BeginScrollView(issueScrollPosition, GUILayout.MaxHeight(300));
                int n = 0;
                foreach (var issue in lastResult.issues)
                {
                    if (issue.severity < minSeverity) continue;
                    if (!string.IsNullOrEmpty(issueFilter) &&
                        !issue.title.ToLower().Contains(issueFilter.ToLower()) &&
                        !issue.description.ToLower().Contains(issueFilter.ToLower())) continue;
                    n++;
                    DrawIssueItem(issue);
                }
                if (n == 0) EditorGUILayout.LabelField("没有匹配的问题", EditorStyles.miniLabel);
                EditorGUILayout.EndScrollView();
            });
        }

        private void DrawIssueItem(SceneIssue issue)
        {
            Color bg = issue.severity switch
            {
                IssueSeverity.Critical => new Color(0.8f, 0.1f, 0.1f, 0.1f),
                IssueSeverity.Error => new Color(0.8f, 0.3f, 0.1f, 0.08f),
                IssueSeverity.Warning => new Color(0.8f, 0.6f, 0.1f, 0.06f),
                _ => new Color(0.3f, 0.3f, 0.3f, 0.05f)
            };
            var r = EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUI.DrawRect(r, bg);

            EditorGUILayout.BeginHorizontal();
            string icon = issue.severity switch { IssueSeverity.Critical => "🔴", IssueSeverity.Error => "🟠", IssueSeverity.Warning => "🟡", _ => "🔵" };
            EditorGUILayout.LabelField($"{icon} [{GetCNLabel(issue.category)}] {issue.title}", EditorStyles.boldLabel);
            if (GUILayout.Button("定位", GUILayout.Width(40))) LocateIssue(issue);
            if (autoFixEnabled && !string.IsNullOrEmpty(issue.autoFixObjectPath))
                if (GUILayout.Button("修复", GUILayout.Width(40))) AutoFixIssue(issue);
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(issue.description))
                EditorGUILayout.LabelField(issue.description, EditorStyles.miniLabel);
            if (!string.IsNullOrEmpty(issue.fixSuggestion))
                EditorGUILayout.LabelField($"💡 {issue.fixSuggestion}", EditorStyles.miniLabel);

            EditorGUILayout.EndVertical();
        }

        private void DrawExport()
        {
            if (lastResult == null) return;
            EditorGuiUtils.DrawSeparator();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("📄 导出 Markdown", GUILayout.Height(28))) ExportMarkdown();
            if (GUILayout.Button("📊 导出 HTML", GUILayout.Height(28))) ExportHtml();
            if (GUILayout.Button("📋 复制报告", GUILayout.Height(28))) CopyReport();
            EditorGUILayout.EndHorizontal();
        }

        private void StatCard(string label, int value, Color? bg = null)
        {
            var r = EditorGUILayout.BeginVertical(GUILayout.Height(48));
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, r.height), bg ?? new Color(0.2f, 0.2f, 0.2f, 0.5f));
            GUILayout.Space(6);
            EditorGUILayout.LabelField(value.ToString(), EditorStyles.boldLabel, GUILayout.Height(20));
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            GUILayout.Space(4);
            EditorGUILayout.EndVertical();
        }

        // ====== 检查逻辑 ======

        private void RunInspection()
        {
            if (isRunning) return;
            isRunning = true;
            checkProgress = 0f;
            inspectionStep = 0;
            lastResult = new SceneCheckResult
            {
                sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                checkTimeMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };
            EditorApplication.update += StepRunner;
        }

        private void StepRunner()
        {
            if (inspectionStep >= StepNames.Length)
            {
                EditorApplication.update -= StepRunner;
                var s = lastResult.summary;
                s.totalChecks = s.passed + s.warnings + s.errors + s.criticalErrors;
                float p = s.criticalErrors * 20f + s.errors * 10f + s.warnings * 3f;
                lastResult.overallScore = Mathf.Max(0, Mathf.Min(100, 100 - p * 100f / Mathf.Max(1, s.totalChecks)));
                isRunning = false;
                checkProgress = 1f;
                Repaint();
                return;
            }

            currentCheckName = StepNames[inspectionStep];
            checkProgress = (float)inspectionStep / StepNames.Length;
            var cat = (CheckCategory)inspectionStep;

            if (config.checkEnabled.ContainsKey(cat) && config.checkEnabled[cat])
            {
                switch (cat)
                {
                    case CheckCategory.Occlusion: CheckOcclusion(); break;
                    case CheckCategory.Clipping: CheckClipping(); break;
                    case CheckCategory.CameraView: CheckCameraView(); break;
                    case CheckCategory.Lighting: CheckLighting(); break;
                    case CheckCategory.Material: CheckMaterials(); break;
                    case CheckCategory.Performance: CheckPerformance(); break;
                    case CheckCategory.Transform: CheckTransforms(); break;
                }
            }
            inspectionStep++;
            Repaint();
        }

        private void CheckOcclusion()
        {
            var cameras = FindObjectsOfType<Camera>();
            var renderers = FindObjectsOfType<Renderer>();
            foreach (var cam in cameras)
            {
                Vector3 cp = cam.transform.position;
                foreach (var r in renderers)
                {
                    if (!r.enabled || r is ParticleSystemRenderer) continue;
                    Vector3 tp = r.bounds.center;
                    Vector3 dir = tp - cp;
                    float dist = dir.magnitude;
                    if (dist < 0.5f || dist > 50f) continue;
                    if (Physics.Raycast(cp, dir.normalized, out var hit, dist))
                    {
                        if (hit.collider.gameObject != r.gameObject && !hit.collider.transform.IsChildOf(r.transform))
                            AddIssue(IssueSeverity.Warning, CheckCategory.Occlusion, $"相机→{r.name} 被遮挡", $"{hit.collider.name} 遮挡了视线", $"调整 {hit.collider.name} 透明度或位置", r.gameObject.name, hit.point);
                    }
                }
            }
            lastResult.summary.passed++;
        }

        private void CheckClipping()
        {
            var renderers = FindObjectsOfType<MeshRenderer>();
            for (int i = 0; i < renderers.Length && lastResult.issues.Count < 50; i++)
                for (int j = i + 1; j < renderers.Length && lastResult.issues.Count < 50; j++)
                {
                    var a = renderers[i]; var b = renderers[j];
                    if (!a.enabled || !b.enabled || !a.bounds.Intersects(b.bounds)) continue;
                    float d = Vector3.Distance(a.bounds.center, b.bounds.center);
                    float t = (a.bounds.extents.magnitude + b.bounds.extents.magnitude) * 0.1f;
                    if (d < t)
                        AddIssue(IssueSeverity.Warning, CheckCategory.Clipping, $"穿模: {a.name}↔{b.name}", $"间距 {d:F3}m", "调整位置", $"{a.name},{b.name}", (a.bounds.center + b.bounds.center) * 0.5f);
                }
            lastResult.summary.passed++;
        }

        private void CheckCameraView()
        {
            foreach (var cam in FindObjectsOfType<Camera>())
            {
                if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, config.minCameraDistance))
                    AddIssue(IssueSeverity.Error, CheckCategory.CameraView, $"相机视角被阻挡 ({cam.name})", $"{hit.collider.name} 距相机 {hit.distance:F2}m", "移除此物体或调整相机", cam.name, hit.point);

                if (Mathf.Abs(cam.transform.forward.y) > 0.9f)
                    AddIssue(IssueSeverity.Info, CheckCategory.CameraView, $"相机朝向异常 ({cam.name})", "方向近乎垂直", "调整俯仰角", cam.name);
            }
            lastResult.summary.passed++;
        }

        private void CheckLighting()
        {
            var lights = FindObjectsOfType<Light>();
            foreach (var l in lights)
            {
                if (l.intensity > 5f) AddIssue(IssueSeverity.Warning, CheckCategory.Lighting, $"光照过曝 ({l.name})", $"强度 {l.intensity}", "降低强度", l.gameObject.name);
                float lum = l.color.grayscale;
                if (lum < 0.05f || lum > 0.98f) AddIssue(IssueSeverity.Info, CheckCategory.Lighting, $"光照颜色极端 ({l.name})", $"亮度 {lum:F2}", "使用更自然的光照颜色", l.gameObject.name);
            }
            if (lights.Length == 0) AddIssue(IssueSeverity.Error, CheckCategory.Lighting, "场景中没有灯光", "", "添加 Directional Light");
            lastResult.summary.passed++;
        }

        private void CheckMaterials()
        {
            foreach (var r in FindObjectsOfType<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null)
                        AddIssue(IssueSeverity.Error, CheckCategory.Material, $"缺失材质 ({r.name}[{i}])", "", "分配正确材质", r.gameObject.name);
                    else if (mats[i].shader == null)
                        AddIssue(IssueSeverity.Error, CheckCategory.Material, $"Shader 丢失 ({mats[i].name})", "", "重新指定 Shader", r.gameObject.name);
                }
            }
            lastResult.summary.passed++;
        }

        private void CheckPerformance()
        {
            int dc = FindObjectsOfType<MeshRenderer>().Length + FindObjectsOfType<SkinnedMeshRenderer>().Length;
            int verts = 0;
            foreach (var f in FindObjectsOfType<MeshFilter>()) if (f.sharedMesh != null) verts += f.sharedMesh.vertexCount;
            foreach (var sr in FindObjectsOfType<SkinnedMeshRenderer>()) if (sr.sharedMesh != null) verts += sr.sharedMesh.vertexCount;

            if (dc > config.maxDrawCallWarning) AddIssue(IssueSeverity.Warning, CheckCategory.Performance, $"DrawCall 过高: {dc}", $"阈值 {config.maxDrawCallWarning}", "使用 GPU Instancing 或合并网格");
            if (verts > config.maxVertexWarning) AddIssue(IssueSeverity.Warning, CheckCategory.Performance, $"顶点数过高: {verts:N0}", $"阈值 {config.maxVertexWarning:N0}", "使用 LOD");
            if (FindObjectsOfType<MeshRenderer>().Length > 10 && FindObjectsOfType<LODGroup>().Length == 0)
                AddIssue(IssueSeverity.Info, CheckCategory.Performance, "未使用 LOD 组", "", "为远处物体添加 LODGroup");
            lastResult.summary.passed++;
        }

        private void CheckTransforms()
        {
            foreach (var t in FindObjectsOfType<Transform>())
            {
                var s = t.localScale;
                if (s.x < 0 || s.y < 0 || s.z < 0)
                    AddIssue(IssueSeverity.Warning, CheckCategory.Transform, $"负 Scale ({t.name})", $"Scale={s}", "设为正值", t.gameObject.name);
                if (Mathf.Abs(s.x) > 100f || Mathf.Abs(s.y) > 100f || Mathf.Abs(s.z) > 100f)
                    AddIssue(IssueSeverity.Warning, CheckCategory.Transform, $"Scale 过大 ({t.name})", $"Scale={s}", "检查模型单位比例", t.gameObject.name);
                var p = t.position;
                if (Mathf.Abs(p.x) > 10000f || Mathf.Abs(p.y) > 10000f || Mathf.Abs(p.z) > 10000f)
                    AddIssue(IssueSeverity.Warning, CheckCategory.Transform, $"位置超出范围 ({t.name})", $"位置={p}", "将物体移近原点", t.gameObject.name);
                if (lastResult.issues.Count > 100) break;
            }
            lastResult.summary.passed++;
        }

        private void AddIssue(IssueSeverity sv, CheckCategory cat, string title, string desc, string fix, string objPath = "", Vector3 pos = default)
        {
            lastResult.issues.Add(new SceneIssue
            {
                issueId = $"{cat}_{lastResult.issues.Count}",
                severity = sv,
                category = cat,
                title = title,
                description = desc,
                fixSuggestion = fix,
                affectedObjectPaths = string.IsNullOrEmpty(objPath) ? null : new[] { objPath },
                worldPosition = pos,
                autoFixObjectPath = objPath
            });
            if (sv == IssueSeverity.Critical) lastResult.summary.criticalErrors++;
            else if (sv == IssueSeverity.Error) lastResult.summary.errors++;
            else if (sv == IssueSeverity.Warning) lastResult.summary.warnings++;
        }

        private void LocateIssue(SceneIssue issue)
        {
            if (issue.affectedObjectPaths != null && issue.affectedObjectPaths.Length > 0)
            {
                var go = GameObject.Find(issue.affectedObjectPaths[0]);
                if (go != null) { Selection.activeGameObject = go; EditorGUIUtility.PingObject(go); SceneView.lastActiveSceneView?.FrameSelected(); }
            }
        }

        private void AutoFixIssue(SceneIssue issue)
        {
            var go = GameObject.Find(issue.autoFixObjectPath);
            if (go == null) return;
            if (issue.category == CheckCategory.Material)
            {
                var r = go.GetComponent<Renderer>();
                if (r != null)
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] == null) mats[i] = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    r.sharedMaterials = mats;
                }
            }
            else if (issue.category == CheckCategory.Transform && issue.title.Contains("负 Scale"))
                go.transform.localScale = new Vector3(Mathf.Abs(go.transform.localScale.x), Mathf.Abs(go.transform.localScale.y), Mathf.Abs(go.transform.localScale.z));
            issue.isFixed = true;
        }

        private void ExportMarkdown()
        {
            if (lastResult == null) return;
            Directory.CreateDirectory(REPORT_DIR);
            string path = Path.Combine(REPORT_DIR, $"报告_{lastResult.sceneName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.md");
            File.WriteAllText(path, config.ToMarkdownReport(lastResult));
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("导出成功", $"报告已保存:\n{path}", "确定");
        }

        private void ExportHtml()
        {
            if (lastResult == null) return;
            Directory.CreateDirectory(REPORT_DIR);
            string path = Path.Combine(REPORT_DIR, $"报告_{lastResult.sceneName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.html");
            var s = lastResult.summary;
            string items = "";
            foreach (var iss in lastResult.issues)
            {
                string sc = iss.severity switch { IssueSeverity.Critical => "#ff4444", IssueSeverity.Error => "#ff8844", IssueSeverity.Warning => "#ffaa00", _ => "#4488ff" };
                items += $"<tr style='background:{sc}11'><td style='color:{sc}'>{iss.severity}</td><td>{GetCNLabel(iss.category)}</td><td>{iss.title}</td><td>{iss.description}</td><td>{iss.fixSuggestion}</td></tr>";
            }
            string html = $"<!DOCTYPE html><html><head><meta charset='utf-8'><title>场景检查报告 - {lastResult.sceneName}</title><style>body{{font-family:'Segoe UI',Arial,sans-serif;margin:20px}} .score{{font-size:48px;font-weight:bold}} .summary{{display:flex;gap:20px;margin:20px 0}} .card{{padding:15px;border-radius:8px;min-width:80px;text-align:center;color:#fff}} table{{width:100%;border-collapse:collapse}} th,td{{padding:8px 12px;text-align:left;border-bottom:1px solid #ddd}} th{{background:#f5f5f5}}</style></head><body><h1>场景检查报告: {lastResult.sceneName}</h1><p>检查时间: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}</p><div class='score' style='color:{(lastResult.overallScore>=80?"green":lastResult.overallScore>=60?"orange":"red")}'>{lastResult.overallScore}/100</div><div class='summary'><div class='card' style='background:#4CAF50'>通过<br>{s.passed}</div><div class='card' style='background:#FF9800'>警告<br>{s.warnings}</div><div class='card' style='background:#f44336'>错误<br>{s.errors}</div><div class='card' style='background:#b71c1c'>严重<br>{s.criticalErrors}</div></div><h2>问题列表</h2><table><tr><th>级别</th><th>类别</th><th>标题</th><th>描述</th><th>修复建议</th></tr>{items}</table></body></html>";
            File.WriteAllText(path, html);
            AssetDatabase.Refresh();
            System.Diagnostics.Process.Start(path);
        }

        private void CopyReport()
        {
            if (lastResult == null) return;
            EditorGUIUtility.systemCopyBuffer = config.ToMarkdownReport(lastResult);
            EditorUtility.DisplayDialog("已复制", "Markdown 报告已复制到剪贴板", "确定");
        }

        private string GetCNLabel(CheckCategory cat) => cat switch
        {
            CheckCategory.Occlusion => "遮挡", CheckCategory.Clipping => "穿模",
            CheckCategory.CameraView => "视角", CheckCategory.Lighting => "光照",
            CheckCategory.Material => "材质", CheckCategory.Performance => "性能",
            CheckCategory.Transform => "变换", CheckCategory.LOD => "LOD",
            CheckCategory.Collider => "碰撞体", _ => cat.ToString()
        };

        private void LoadPrefs() { autoFixEnabled = EditorPrefs.GetBool(PREFS_KEY + "AutoFix", false); }
        private void SavePrefs() { EditorPrefs.SetBool(PREFS_KEY + "AutoFix", autoFixEnabled); }
    }
}
