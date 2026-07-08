using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneProductionToolkit.Data
{
    public enum UnitType { Meters, Centimeters, Inches, Feet, Millimeters }
    public enum SceneType { Interior, Exterior, Character, Prop, Environment, ScanReconstruction }
    public enum ColliderType { MeshCollider, BoxCollider, SphereCollider, CapsuleCollider, Compound }
    public enum MaterialType { Opaque, Transparent, Cutout, Hair, Skin, Cloth, Glass, Metal, Vegetation, Water, Terrain }

    [Serializable]
    public class AssetImportConfig
    {
        public string presetName;
        public string description;
        public SceneType sceneType = SceneType.Interior;
        public float globalScaleFactor = 1f;
        public UnitType sourceUnit = UnitType.Meters;
        public UnitType targetUnit = UnitType.Meters;
        public bool convertToUrp = true;
        public bool autoGenerateColliders = true;
        public ColliderType colliderType = ColliderType.MeshCollider;
        public bool optimizeMesh = false;
        public bool generateLODs = false;
        public float[] lodReduction = { 0.5f, 0.2f };
        public List<MaterialMappingRule> materialMappings = new List<MaterialMappingRule>();
        public string[] postProcessScripts;
        public bool createPrefab = true;
        public string prefabOutputPath = "Assets/Prefabs/已导入/";
    }

    [Serializable]
    public class MaterialMappingRule
    {
        public string sourceNamePattern;
        public MaterialType materialType;
        public string urpShaderName = "Universal Render Pipeline/Lit";
        public string builtInShaderName = "Standard";
        public bool enableEmission;
        public Color emissionColor = Color.black;
    }

    [Serializable]
    public class ImportStatistics
    {
        public string fileName;
        public string filePath;
        public long fileSizeBytes;
        public int vertexCount;
        public int triangleCount;
        public int subMeshCount;
        public int materialCount;
        public int missingMaterialCount;
        public float importDurationMs;
        public bool hasAnimation;
        public bool hasSkinning;
        public bool hasBlendShapes;
        public List<string> warnings = new List<string>();
        public bool success;
        public string errorMessage;
    }

    [Serializable]
    public class ImportPresetLibrary
    {
        public List<AssetImportConfig> presets = new List<AssetImportConfig>();

        public AssetImportConfig GetPreset(string name) =>
            presets.Find(p => p.presetName.Equals(name, StringComparison.OrdinalIgnoreCase));

        public void SetPreset(AssetImportConfig preset)
        {
            int idx = presets.FindIndex(p => p.presetName.Equals(preset.presetName, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) presets[idx] = preset;
            else presets.Add(preset);
        }

        public string ToJson() => JsonUtility.ToJson(this, true);
    }
}
