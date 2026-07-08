using UnityEditor;
using UnityEngine;

namespace SceneProductionToolkit.EditorCommon
{
    /// <summary>
    /// Editor GUI 工具类
    /// 提供通用的 GUI 辅助方法，减少 Editor Window 中的重复代码
    /// </summary>
    public static class EditorGuiUtils
    {
        /// <summary> 标题颜色 - 深蓝色 </summary>
        private static readonly Color HeaderColor = new Color(0.2f, 0.4f, 0.8f);

        /// <summary> 警告背景色 </summary>
        public static readonly Color WarningBgColor = new Color(0.8f, 0.6f, 0.1f, 0.15f);

        /// <summary> 错误背景色 </summary>
        public static readonly Color ErrorBgColor = new Color(0.8f, 0.2f, 0.1f, 0.15f);

        /// <summary> 成功背景色 </summary>
        public static readonly Color SuccessBgColor = new Color(0.1f, 0.8f, 0.2f, 0.15f);

        /// <summary>
        /// 绘制带样式的标题栏
        /// </summary>
        public static void DrawHeader(string title, string subtitle = null)
        {
            EditorGUILayout.Space(4);

            var rect = EditorGUILayout.BeginVertical();

            // 背景
            EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, 36),
                new Color(0.15f, 0.15f, 0.15f));
            EditorGUI.DrawRect(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, 36),
                HeaderColor * new Color(1f, 1f, 1f, 0.1f));

            GUILayout.Space(4);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel, GUILayout.Height(20));

            if (!string.IsNullOrEmpty(subtitle))
            {
                EditorGUILayout.LabelField(subtitle, EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.Space(4);
        }

        /// <summary>
        /// 绘制信息告警框
        /// </summary>
        public static void DrawInfoBox(string message, MessageType type = MessageType.Info)
        {
            EditorGUILayout.HelpBox(message, type);
        }

        /// <summary>
        /// 绘制可折叠 Section
        /// </summary>
        public static bool DrawFoldoutSection(string title, bool defaultExpanded, System.Action content)
        {
            EditorGUILayout.Space(2);

            // 使用 EditorGUILayout.BeginFoldoutHeaderGroup
            bool expanded = EditorGUILayout.BeginFoldoutHeaderGroup(
                EditorPrefs.GetBool($"SceneProductionToolkit_{title}", defaultExpanded), title);

            EditorPrefs.SetBool($"SceneProductionToolkit_{title}", expanded);

            if (expanded)
            {
                EditorGUI.indentLevel++;
                content?.Invoke();
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.EndFoldoutHeaderGroup();
            EditorGUILayout.Space(2);

            return expanded;
        }

        /// <summary>
        /// 绘制统计卡片
        /// </summary>
        public static void DrawStatCard(string label, string value, Color? bgColor = null)
        {
            var rect = EditorGUILayout.BeginVertical(GUILayout.Height(48));
            EditorGUI.DrawRect(
                new Rect(rect.x, rect.y, rect.width, rect.height),
                bgColor ?? new Color(0.2f, 0.2f, 0.2f, 0.5f));

            GUILayout.Space(6);
            EditorGUILayout.LabelField(value, EditorStyles.boldLabel, GUILayout.Height(20));
            EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
            GUILayout.Space(4);

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制按钮行（多个按钮在一行）
        /// </summary>
        public static void DrawButtonRow(params (string label, System.Action action)[] buttons)
        {
            EditorGUILayout.BeginHorizontal();
            foreach (var (label, action) in buttons)
            {
                if (GUILayout.Button(label, GUILayout.Height(28)))
                {
                    action?.Invoke();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制文件路径选择器
        /// </summary>
        public static string DrawFilePathField(string label, string currentPath, string extension, string relativeTo = null)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(label, GUILayout.Width(80));
            string newPath = EditorGUILayout.TextField(currentPath);
            if (GUILayout.Button("浏览...", GUILayout.Width(70)))
            {
                string dir = string.IsNullOrEmpty(relativeTo)
                    ? Application.dataPath
                    : System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, relativeTo));

                string selected = EditorUtility.OpenFilePanel("选择文件", dir, extension);
                if (!string.IsNullOrEmpty(selected))
                {
                    newPath = selected;
                }
            }
            EditorGUILayout.EndHorizontal();
            return newPath;
        }

        /// <summary>
        /// 绘制进度条
        /// </summary>
        public static void DrawProgressBar(float progress, string label = null)
        {
            Rect rect = EditorGUILayout.BeginVertical();
            EditorGUI.ProgressBar(rect, progress, label ?? $"{progress:P1}");
            GUILayout.Space(20);
            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制分割线
        /// </summary>
        public static void DrawSeparator()
        {
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
        }

        /// <summary>
        /// 绘制小标签-值对
        /// </summary>
        public static void DrawKeyValue(string key, string value)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(key, EditorStyles.miniLabel, GUILayout.Width(120));
            EditorGUILayout.LabelField(value);
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制上下文 HelpBox（带帮助按钮）
        /// </summary>
        public static void DrawContextHelp(string title, string helpText)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            if (GUILayout.Button("?", GUILayout.Width(20), GUILayout.Height(16)))
            {
                EditorUtility.DisplayDialog(title, helpText, "确定");
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制搜索栏
        /// </summary>
        public static string DrawSearchBar(string searchText, string placeholder = "搜索...")
        {
            EditorGUILayout.BeginHorizontal();

            string newText = EditorGUILayout.TextField(searchText, EditorStyles.toolbarSearchField);

            if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20)))
            {
                newText = "";
                GUI.FocusControl(null);
            }

            EditorGUILayout.EndHorizontal();
            return newText;
        }

        /// <summary>
        /// 发出 Ping 到 Project 窗口选中物体
        /// </summary>
        public static void PingObject(Object obj)
        {
            if (obj != null)
            {
                EditorGUIUtility.PingObject(obj);
            }
        }

        /// <summary>
        /// 绘制时间戳提示
        /// </summary>
        public static void DrawTimestamp()
        {
            EditorGUILayout.LabelField($"Updated: {System.DateTime.Now:HH:mm:ss}", EditorStyles.miniLabel);
        }

        /// <summary>
        /// 获取编辑器主题色
        /// </summary>
        public static Color ThemeColor
        {
            get { return EditorGUIUtility.isProSkin ? Color.white : Color.black; }
        }
    }
}
