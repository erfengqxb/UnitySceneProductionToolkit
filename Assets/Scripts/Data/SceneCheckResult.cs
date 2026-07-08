using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneProductionToolkit.Data
{
    /// <summary>
    /// 场景检查结果模型
    /// </summary>
    [Serializable]
    public class SceneCheckResult
    {
        public long checkTimeMs;
        public string sceneName;
        public float overallScore;
        public CheckSummary summary = new CheckSummary();
        public List<SceneIssue> issues = new List<SceneIssue>();
        public List<string> passedChecks = new List<string>();
    }

    [Serializable]
    public class CheckSummary
    {
        public int totalChecks;
        public int passed;
        public int warnings;
        public int errors;
        public int criticalErrors;
    }

    [Serializable]
    public class SceneIssue
    {
        public string issueId;
        public IssueSeverity severity = IssueSeverity.Warning;
        public CheckCategory category = CheckCategory.Occlusion;
        public string title;
        public string description;
        public string fixSuggestion;
        public string[] affectedObjectPaths;
        public Vector3 worldPosition;
        public string screenshotPath;
        public bool isFixed;
        public string autoFixObjectPath;
    }

    public enum IssueSeverity { Info, Warning, Error, Critical }

    public enum CheckCategory
    {
        Occlusion,     // 遮挡
        Clipping,      // 穿模
        CameraView,    // 视角
        Lighting,      // 光照
        Material,      // 材质
        Performance,   // 性能
        Transform,     // 变换
        FileIntegrity, // 文件完整性
        LOD,           // LOD
        Collider       // 碰撞体
    }

    [Serializable]
    public class InspectorConfig
    {
        public float clippingSampleStep = 0.5f;
        public float maxClippingTolerance = 0.05f;
        public float minCameraDistance = 0.3f;
        public float overExposureLuminance = 0.95f;
        public float underExposureLuminance = 0.05f;
        public int maxDrawCallWarning = 200;
        public int maxVertexWarning = 200000;
        public Dictionary<CheckCategory, bool> checkEnabled = new Dictionary<CheckCategory, bool>
        {
            { CheckCategory.Occlusion, true }, { CheckCategory.Clipping, true },
            { CheckCategory.CameraView, true }, { CheckCategory.Lighting, true },
            { CheckCategory.Material, true }, { CheckCategory.Performance, true },
            { CheckCategory.Transform, true }, { CheckCategory.LOD, true },
            { CheckCategory.Collider, false }
        };

        public string ToJson() => JsonUtility.ToJson(this, true);

        public string ToMarkdownReport(SceneCheckResult result)
        {
            var s = result.summary;
            var md = $"# 场景检查报告: {result.sceneName}\n\n";
            md += $"**检查时间**: {DateTimeOffset.FromUnixTimeMilliseconds(result.checkTimeMs).LocalDateTime:yyyy-MM-dd HH:mm:ss}\n";
            md += $"**总体评分**: {result.overallScore}/100\n\n";
            md += "## 汇总\n\n| 检查项 | 数量 |\n|---|---|\n";
            md += $"| 通过 | {s.passed} |\n| 警告 | {s.warnings} |\n| 错误 | {s.errors} |\n| 严重 | {s.criticalErrors} |\n\n";

            if (result.issues.Count > 0)
            {
                md += "## 问题列表\n\n| 级别 | 类别 | 描述 | 修复建议 |\n|---|---|---|---|\n";
                foreach (var issue in result.issues)
                    md += $"| {issue.severity} | {issue.category} | {issue.title.Replace("|", "\\|")} | {issue.fixSuggestion?.Replace("|", "\\|") ?? ""} |\n";
            }
            return md;
        }
    }
}
