using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using SceneProductionToolkit.Data;
using SceneProductionToolkit.EditorCommon;
using Object = UnityEngine.Object;

namespace SceneProductionToolkit.Editor
{
    public class AssetImportToolWindow : EditorWindow
    {
        private const string WINDOW_TITLE = "资产导入工具";
        private const string PREFS = "AssetImportTool_";
        private static readonly string[] SupportedFormats = { ".fbx", ".obj", ".gltf", ".glb", ".blend" };

        private Vector2 scroll;
        private string searchFilter = "";
        private int presetIndex;
        private List<string> presetNames = new List<string>();
        private AssetImportConfig currentConfig = new AssetImportConfig();
        private List<string> importQueue = new List<string>();
        private bool isImporting;
        private float importProgress;
        private string currentFile;
        private List<ImportStatistics> results = new List<ImportStatistics>();
        private Vector2 resScroll;
        private ImportPresetLibrary library;

        [MenuItem("Window/Production Tools/Asset Import Tool", false, 1000)]
        public static void ShowWindow()
        {
            var w = GetWindow<AssetImportToolWindow>(WINDOW_TITLE);
            w.minSize = new Vector2(480, 600);
            w.Show();
        }

        private void OnEnable()
        {
            LoadPresets();
            LoadPrefs();
            if (library.presets.Count == 0) CreateDefaults();
            if (library.presets.Count > 0) currentConfig = library.presets[presetIndex];
        }

        private void OnDisable() { SavePresets(); SavePrefs(); }

        private void OnGUI()
        {
            EditorGuiUtils.DrawHeader("三维资产导入工具", "FBX / OBJ / glTF / GLB · 批量导入 · 预设配置");
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawDropArea();
            DrawPresets();
            DrawOptions();
            DrawImportBtn();
            DrawProgress();
            DrawStats();
            EditorGUILayout.EndScrollView();
        }

        private void DrawDropArea()
        {
            EditorGuiUtils.DrawFoldoutSection("📂 文件导入队列", true, () =>
            {
                searchFilter = EditorGuiUtils.DrawSearchBar(searchFilter, "筛选文件名...");
                Rect drop = EditorGUILayout.BeginVertical(GUILayout.Height(60));
                EditorGUI.DrawRect(drop, new Color(0.3f, 0.5f, 0.8f, 0.1f));
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("将 FBX / OBJ / glTF / GLB 文件拖入此处", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"当前队列: {importQueue.Count} 个文件", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();
                HandleDrop(drop);

                if (importQueue.Count > 0)
                {
                    for (int i = 0; i < importQueue.Count; i++)
                    {
                        EditorGUILayout.BeginHorizontal();
                        string name = Path.GetFileName(importQueue[i]);
                        bool match = string.IsNullOrEmpty(searchFilter) || name.ToLower().Contains(searchFilter.ToLower());
                        if (!match) GUI.enabled = false;
                        EditorGUILayout.LabelField($"  {i + 1}. {name}");
                        if (GUILayout.Button("×", GUILayout.Width(24))) { importQueue.RemoveAt(i); GUIUtility.ExitGUI(); }
                        GUI.enabled = true;
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("添加文件", GUILayout.Height(24))) AddFromDialog();
                    if (GUILayout.Button("从文件夹添加", GUILayout.Height(24))) AddFromDir();
                    if (GUILayout.Button("清空队列", GUILayout.Height(24))) importQueue.Clear();
                    EditorGUILayout.EndHorizontal();
                }
            });
        }

        private void DrawPresets()
        {
            EditorGuiUtils.DrawFoldoutSection("⚙️ 导入预设", true, () =>
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("预设方案:", GUILayout.Width(80));
                var names = library.presets.ConvertAll(p => p.presetName).ToArray();
                int ni = EditorGUILayout.Popup(presetIndex, names);
                if (ni != presetIndex) { presetIndex = ni; currentConfig = library.presets[presetIndex]; }
                if (GUILayout.Button("保存为预设", GUILayout.Width(90))) SavePreset();
                EditorGUILayout.EndHorizontal();

                if (currentConfig != null)
                {
                    EditorGUI.indentLevel++;
                    currentConfig.globalScaleFactor = EditorGUILayout.FloatField("全局缩放", currentConfig.globalScaleFactor);
                    currentConfig.sourceUnit = (UnitType)EditorGUILayout.EnumPopup("源文件单位", currentConfig.sourceUnit);
                    currentConfig.targetUnit = (UnitType)EditorGUILayout.EnumPopup("目标单位", currentConfig.targetUnit);
                    currentConfig.convertToUrp = EditorGUILayout.Toggle("转换为 URP 材质", currentConfig.convertToUrp);
                    currentConfig.colliderType = (ColliderType)EditorGUILayout.EnumPopup("碰撞体类型", currentConfig.colliderType);
                    currentConfig.createPrefab = EditorGUILayout.Toggle("创建 Prefab", currentConfig.createPrefab);
                    EditorGUI.indentLevel--;
                }
            });
        }

        private void DrawOptions()
        {
            EditorGuiUtils.DrawFoldoutSection("🔧 高级选项", false, () =>
            {
                if (currentConfig == null) return;
                currentConfig.optimizeMesh = EditorGUILayout.Toggle("合并子网格", currentConfig.optimizeMesh);
                currentConfig.generateLODs = EditorGUILayout.Toggle("生成 LOD", currentConfig.generateLODs);
                if (currentConfig.generateLODs)
                {
                    EditorGUI.indentLevel++;
                    Vector2 lod = EditorGUILayout.Vector2Field("LOD 缩减率 (LOD1, LOD2)",
                        currentConfig.lodReduction.Length >= 2
                            ? new Vector2(currentConfig.lodReduction[0], currentConfig.lodReduction[1])
                            : new Vector2(0.5f, 0.2f));
                    currentConfig.lodReduction = new[] { lod.x, lod.y };
                    EditorGUI.indentLevel--;
                }
                if (currentConfig.createPrefab)
                {
                    EditorGUI.indentLevel++;
                    currentConfig.prefabOutputPath = EditorGUILayout.TextField("Prefab 输出路径", currentConfig.prefabOutputPath);
                    EditorGUI.indentLevel--;
                }
            });
        }

        private void DrawImportBtn()
        {
            EditorGUILayout.Space(6);
            GUI.enabled = !isImporting && importQueue.Count > 0;
            if (GUILayout.Button($"开始导入 ({importQueue.Count} 个文件)", GUILayout.Height(36))) StartImport();
            GUI.enabled = true;
        }

        private void DrawProgress()
        {
            if (!isImporting && importProgress <= 0f) return;
            EditorGuiUtils.DrawSeparator();
            if (isImporting)
            {
                EditorGUILayout.LabelField($"正在导入: {currentFile}");
                Rect r = EditorGUILayout.BeginVertical();
                EditorGUI.ProgressBar(r, importProgress, $"{importProgress:P1}");
                GUILayout.Space(20);
                EditorGUILayout.EndVertical();
                if (GUILayout.Button("取消")) isImporting = false;
            }
            else EditorGUILayout.LabelField("✅ 导入完成!");
        }

        private void DrawStats()
        {
            if (results.Count == 0) return;
            EditorGuiUtils.DrawFoldoutSection("📊 导入统计", true, () =>
            {
                int ok = results.FindAll(r => r.success).Count;
                int fail = results.Count - ok;
                EditorGUILayout.BeginHorizontal();
                StatCard("成功", ok, new Color(0.1f, 0.6f, 0.1f, 0.3f));
                StatCard("失败", fail, new Color(0.8f, 0.2f, 0.1f, 0.3f));
                int tv = 0, tt = 0;
                foreach (var r in results) { tv += r.triangleCount; tt += r.vertexCount; }
                StatCard("总三角面", tv);
                StatCard("总顶点", tt);
                EditorGUILayout.EndHorizontal();

                resScroll = EditorGUILayout.BeginScrollView(resScroll, GUILayout.MinHeight(100));
                foreach (var r in results)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(r.success ? "✅" : "❌", GUILayout.Width(20));
                    EditorGUILayout.LabelField(r.fileName, GUILayout.Width(150));
                    EditorGUILayout.LabelField($"V:{r.vertexCount:N0} T:{r.triangleCount:N0}");
                    if (!string.IsNullOrEmpty(r.errorMessage)) EditorGUILayout.LabelField(r.errorMessage, EditorStyles.miniLabel);
                    if (GUILayout.Button("定位", GUILayout.Width(40))) { var a = AssetDatabase.LoadAssetAtPath<Object>(r.filePath); if (a != null) EditorGUIUtility.PingObject(a); }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndScrollView();
                if (GUILayout.Button("清空统计")) results.Clear();
            });
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

        private void StartImport()
        {
            if (importQueue.Count == 0) return;
            isImporting = true;
            importProgress = 0f;
            results.Clear();
            EditorApplication.update += ImportNext;
        }

        private void ImportNext()
        {
            if (!isImporting || importQueue.Count == 0)
            {
                EditorApplication.update -= ImportNext;
                isImporting = false;
                importProgress = 1f;
                Repaint();
                return;
            }
            string path = importQueue[0];
            importQueue.RemoveAt(0);
            currentFile = Path.GetFileName(path);
            results.Add(DoImport(path));
            importProgress = 1f - (float)importQueue.Count / (importQueue.Count + results.Count);
            Repaint();
        }

        private ImportStatistics DoImport(string path)
        {
            var s = new ImportStatistics { fileName = Path.GetFileName(path), filePath = path, fileSizeBytes = new FileInfo(path).Length };
            double start = EditorApplication.timeSinceStartup;
            try
            {
                string assetPath = CopyToAssets(path);
                if (string.IsNullOrEmpty(assetPath)) throw new System.Exception("文件复制失败");
                ApplySettings(assetPath);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (go != null)
                {
                    foreach (var f in go.GetComponentsInChildren<MeshFilter>()) if (f.sharedMesh != null) { s.vertexCount += f.sharedMesh.vertexCount; s.triangleCount += f.sharedMesh.triangles.Length / 3; s.subMeshCount += f.sharedMesh.subMeshCount; }
                    s.materialCount = go.GetComponentsInChildren<Renderer>().Length;
                    s.hasAnimation = go.GetComponentInChildren<Animation>() != null || go.GetComponentInChildren<Animator>() != null;
                    s.hasSkinning = go.GetComponentInChildren<SkinnedMeshRenderer>() != null;
                    if (currentConfig.createPrefab) CreatePrefab(go, assetPath);
                }
                s.success = true;
                if (currentConfig.convertToUrp) ConvertMaterials(assetPath);
            }
            catch (System.Exception e) { s.success = false; s.errorMessage = e.Message; }
            s.importDurationMs = (float)((EditorApplication.timeSinceStartup - start) * 1000);
            return s;
        }

        private string CopyToAssets(string src)
        {
            string folder = "Assets/已导入/";
            Directory.CreateDirectory(folder);
            if (src.StartsWith(Application.dataPath)) return "Assets" + src.Substring(Application.dataPath.Length);
            string dest = Path.Combine(folder, Path.GetFileName(src));
            dest = AssetDatabase.GenerateUniqueAssetPath(dest);
            File.Copy(src, dest, true);
            AssetDatabase.Refresh();
            return dest;
        }

        private void ApplySettings(string path)
        {
            if (AssetImporter.GetAtPath(path) is ModelImporter mi)
            {
                mi.globalScale = currentConfig.globalScaleFactor;
                mi.importMaterials = true;
                mi.materialName = ModelImporterMaterialName.BasedOnMaterialName;
                mi.optimizeMesh = currentConfig.optimizeMesh;
                mi.SaveAndReimport();
            }
        }

        private void ConvertMaterials(string path)
        {
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp == null) return;
            foreach (var m in AssetDatabase.LoadAllAssetsAtPath(path))
                if (m is Material mat && mat.shader != null && (mat.shader.name == "Standard" || mat.shader.name.Contains("Legacy")))
                { mat.shader = urp; EditorUtility.SetDirty(mat); }
            AssetDatabase.SaveAssets();
        }

        private void CreatePrefab(GameObject asset, string path)
        {
            Directory.CreateDirectory(currentConfig.prefabOutputPath);
            string p = Path.Combine(currentConfig.prefabOutputPath, asset.name + ".prefab");
            PrefabUtility.SaveAsPrefabAsset(asset, AssetDatabase.GenerateUniqueAssetPath(p));
        }

        private void HandleDrop(Rect rect)
        {
            var evt = Event.current;
            if (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform)
            {
                if (!rect.Contains(evt.mousePosition)) return;
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                if (evt.type == EventType.DragPerform)
                {
                    DragAndDrop.AcceptDrag();
                    foreach (string p in DragAndDrop.paths)
                    {
                        if (IsSupported(p)) { if (!importQueue.Contains(p)) importQueue.Add(p); }
                        else if (Directory.Exists(p)) AddFromDir(p);
                    }
                    Repaint();
                }
                evt.Use();
            }
        }

        private void AddFromDialog()
        {
            var paths = EditorUtility.OpenFilePanelWithFilters("选择模型文件", "", new[] { "三维模型文件", "fbx,obj,gltf,glb,blend" });
            if (paths != null) foreach (var p in paths) if (!string.IsNullOrEmpty(p) && !importQueue.Contains(p)) importQueue.Add(p);
        }

        private void AddFromDir() { string d = EditorUtility.OpenFolderPanel("选择文件夹", Application.dataPath, ""); if (!string.IsNullOrEmpty(d)) AddFromDir(d); }
        private void AddFromDir(string dir) { foreach (var f in SupportedFormats) foreach (var file in Directory.GetFiles(dir, "*" + f, SearchOption.AllDirectories)) if (!importQueue.Contains(file)) importQueue.Add(file); }
        private bool IsSupported(string path) { string e = Path.GetExtension(path).ToLower(); foreach (var f in SupportedFormats) if (e == f) return true; return false; }

        private void LoadPresets()
        {
            string j = EditorPrefs.GetString(PREFS + "Library", "");
            try { library = string.IsNullOrEmpty(j) ? new ImportPresetLibrary() : JsonUtility.FromJson<ImportPresetLibrary>(j); }
            catch { library = new ImportPresetLibrary(); }
        }
        private void SavePresets() => EditorPrefs.SetString(PREFS + "Library", JsonUtility.ToJson(library));
        private void LoadPrefs() { presetIndex = EditorPrefs.GetInt(PREFS + "Idx", 0); }
        private void SavePrefs() => EditorPrefs.SetInt(PREFS + "Idx", presetIndex);

        private void CreateDefaults()
        {
            library.presets.Add(new AssetImportConfig { presetName = "室内场景", description = "室内扫描模型 cm→m", sourceUnit = UnitType.Centimeters, targetUnit = UnitType.Meters, globalScaleFactor = 0.01f });
            library.presets.Add(new AssetImportConfig { presetName = "室外场景", description = "室外大场景 1:1", sourceUnit = UnitType.Meters, targetUnit = UnitType.Meters, globalScaleFactor = 1f, colliderType = ColliderType.Compound });
            library.presets.Add(new AssetImportConfig { presetName = "角色模型", description = "FBX 带动画", sourceUnit = UnitType.Meters, targetUnit = UnitType.Meters, colliderType = ColliderType.CapsuleCollider });
            library.presets.Add(new AssetImportConfig { presetName = "3DGS 扫描重建", description = "三维重建 + LOD", sourceUnit = UnitType.Centimeters, targetUnit = UnitType.Meters, globalScaleFactor = 0.01f, optimizeMesh = true, generateLODs = true, lodReduction = new[] { 0.5f, 0.15f } });
        }

        private void SavePreset()
        {
            var c = new AssetImportConfig { presetName = $"预设_{library.presets.Count + 1}", globalScaleFactor = currentConfig.globalScaleFactor, sourceUnit = currentConfig.sourceUnit, targetUnit = currentConfig.targetUnit, convertToUrp = currentConfig.convertToUrp, colliderType = currentConfig.colliderType };
            library.SetPreset(c);
            SavePresets();
            for (int i = 0; i < library.presets.Count; i++) if (library.presets[i].presetName == c.presetName) { presetIndex = i; currentConfig = library.presets[i]; break; }
        }
    }
}
