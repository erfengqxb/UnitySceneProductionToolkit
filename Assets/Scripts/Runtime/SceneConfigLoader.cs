using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SceneProductionToolkit.Data;

namespace SceneProductionToolkit.Runtime
{
    /// <summary>
    /// 场景配置加载器 — JSON 配置读写 / 场景导出 / 热重载
    /// </summary>
    public class SceneConfigLoader : MonoBehaviour
    {
        [Header("配置")]
        [SerializeField] private string configFileName = "SceneConfig.json";
        [SerializeField] private bool loadOnStart = true;
        [SerializeField] private bool hotReload = false;
        [SerializeField] private float hotReloadInterval = 5f;

        [Header("状态")]
        [SerializeField] private bool configLoaded;
        [SerializeField] private string lastLoadedJson;

        private string configFilePath;
        private float hotReloadTimer;
        private Dictionary<string, GameObject> registeredObjects = new Dictionary<string, GameObject>();

        public event System.Action<SceneConfigData> OnConfigLoaded;
        public event System.Action<string> OnConfigError;

        public bool ConfigLoaded => configLoaded;
        public string ConfigFilePath => configFilePath;

        private void Start()
        {
            configFilePath = Path.Combine(Application.streamingAssetsPath, configFileName);
            if (loadOnStart) LoadConfig();
            RegisterExistingObjects();
        }

        private void Update()
        {
            if (hotReload && configLoaded)
            {
                hotReloadTimer += Time.deltaTime;
                if (hotReloadTimer >= hotReloadInterval)
                {
                    hotReloadTimer = 0f;
                    TryHotReload();
                }
            }
        }

        public bool LoadConfig()
        {
            try
            {
                if (!File.Exists(configFilePath))
                {
                    string resourcePath = Path.GetFileNameWithoutExtension(configFileName);
                    TextAsset textAsset = Resources.Load<TextAsset>(resourcePath);
                    if (textAsset != null)
                    {
                        lastLoadedJson = textAsset.text;
                        return ApplyConfig(lastLoadedJson);
                    }
                    Debug.LogWarning($"[场景配置] 配置文件不存在: {configFilePath}");
                    OnConfigError?.Invoke($"配置文件不存在: {configFilePath}");
                    return false;
                }

                lastLoadedJson = File.ReadAllText(configFilePath);
                return ApplyConfig(lastLoadedJson);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[场景配置] 加载失败: {e.Message}");
                OnConfigError?.Invoke(e.Message);
                return false;
            }
        }

        public bool LoadConfigFrom(string fullPath)
        {
            configFilePath = fullPath;
            return LoadConfig();
        }

        public bool ApplyConfig(string json)
        {
            try
            {
                var config = JsonUtility.FromJson<SceneConfigData>(json);
                if (config == null)
                {
                    Debug.LogError("[场景配置] JSON 解析失败");
                    return false;
                }
                ApplySceneConfig(config);
                configLoaded = true;
                OnConfigLoaded?.Invoke(config);
                Debug.Log($"[场景配置] 加载成功: {config.configName}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[场景配置] 解析失败: {e.Message}");
                OnConfigError?.Invoke(e.Message);
                return false;
            }
        }

        public string ExportCurrentScene(string exportName = null)
        {
            var config = new SceneConfigData
            {
                configName = exportName ?? $"场景导出_{System.DateTime.Now:yyyyMMdd_HHmmss}",
                exportTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                version = "1.0"
            };

            foreach (var obj in FindObjectsOfType<Transform>())
            {
                if (obj.parent == null)
                    config.objects.Add(ExtractObjectData(obj));
            }
            foreach (var cam in FindObjectsOfType<Camera>())
            {
                config.cameras.Add(new CameraConfigData
                {
                    name = cam.name, position = cam.transform.position,
                    rotation = cam.transform.rotation.eulerAngles,
                    fov = cam.fieldOfView, nearClip = cam.nearClipPlane, farClip = cam.farClipPlane
                });
            }
            foreach (var light in FindObjectsOfType<Light>())
            {
                config.lights.Add(new LightConfigData
                {
                    name = light.name, position = light.transform.position,
                    rotation = light.transform.rotation.eulerAngles,
                    lightType = light.type.ToString(),
                    color = ColorUtility.ToHtmlStringRGB(light.color), intensity = light.intensity
                });
            }

            lastLoadedJson = JsonUtility.ToJson(config, true);
            return lastLoadedJson;
        }

        public bool ExportToFile(string filePath = null)
        {
            string path = filePath ?? Path.Combine(Application.streamingAssetsPath,
                $"导出_{System.DateTime.Now:yyyyMMdd_HHmmss}.json");
            try
            {
                File.WriteAllText(path, ExportCurrentScene());
                Debug.Log($"[场景配置] 导出成功: {path}");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[场景配置] 导出失败: {e.Message}");
                return false;
            }
        }

        public void RegisterObject(string key, GameObject obj)
        {
            if (!registeredObjects.ContainsKey(key)) registeredObjects.Add(key, obj);
            else registeredObjects[key] = obj;
        }

        public GameObject FindRegisteredObject(string objectId)
        {
            if (registeredObjects.TryGetValue(objectId, out var obj)) return obj;
            var found = GameObject.Find(objectId);
            if (found != null) registeredObjects[objectId] = found;
            return found;
        }

        public void GenerateDefaultConfig()
        {
            var config = new SceneConfigData
            {
                configName = "默认场景配置", version = "1.0", description = "自动生成的默认场景配置"
            };
            lastLoadedJson = JsonUtility.ToJson(config, true);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configFilePath));
                File.WriteAllText(configFilePath, lastLoadedJson);
                Debug.Log($"[场景配置] 默认配置已生成: {configFilePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[场景配置] 写入默认配置失败: {e.Message}");
            }
        }

        private void RegisterExistingObjects()
        {
            foreach (var obj in FindObjectsOfType<GameObject>())
                if (!registeredObjects.ContainsKey(obj.name)) registeredObjects.Add(obj.name, obj);
        }

        private void ApplySceneConfig(SceneConfigData config)
        {
            foreach (var camConfig in config.cameras)
            {
                var camObj = FindRegisteredObject(camConfig.name) ?? new GameObject(camConfig.name);
                var camera = camObj.GetComponent<Camera>() ?? camObj.AddComponent<Camera>();
                camObj.transform.position = camConfig.position;
                camObj.transform.rotation = Quaternion.Euler(camConfig.rotation);
                camera.fieldOfView = camConfig.fov;
                camera.nearClipPlane = camConfig.nearClip;
                camera.farClipPlane = camConfig.farClip;
            }
            foreach (var lightConfig in config.lights)
            {
                var lightObj = FindRegisteredObject(lightConfig.name) ?? new GameObject(lightConfig.name);
                var light = lightObj.GetComponent<Light>() ?? lightObj.AddComponent<Light>();
                lightObj.transform.position = lightConfig.position;
                lightObj.transform.rotation = Quaternion.Euler(lightConfig.rotation);
                if (System.Enum.TryParse<LightType>(lightConfig.lightType, out var lt)) light.type = lt;
                if (ColorUtility.TryParseHtmlString(lightConfig.color, out var col)) light.color = col;
                light.intensity = lightConfig.intensity;
            }
        }

        private ObjectData ExtractObjectData(Transform t)
        {
            var data = new ObjectData
            {
                name = t.name, position = t.position, rotation = t.rotation.eulerAngles,
                scale = t.localScale, isActive = t.gameObject.activeSelf,
                hasMesh = t.GetComponent<MeshFilter>() != null,
                hasCollider = t.GetComponent<Collider>() != null,
                hasAnimator = t.GetComponent<Animator>() != null,
                childCount = t.childCount
            };
            for (int i = 0; i < t.childCount; i++)
                data.children.Add(ExtractObjectData(t.GetChild(i)));
            return data;
        }

        private void TryHotReload()
        {
            if (!File.Exists(configFilePath)) return;
            try
            {
                string content = File.ReadAllText(configFilePath);
                if (content != lastLoadedJson)
                {
                    Debug.Log("[场景配置] 检测到变更，热重载...");
                    ApplyConfig(content);
                }
            }
            catch { }
        }
    }

    [System.Serializable]
    public class SceneConfigData
    {
        public string configName;
        public string version = "1.0";
        public string description;
        public long exportTime;
        public RecordingPreset recordingPreset;
        public List<CameraConfigData> cameras = new List<CameraConfigData>();
        public List<LightConfigData> lights = new List<LightConfigData>();
        public List<ObjectData> objects = new List<ObjectData>();
        public string ToJson() => JsonUtility.ToJson(this, true);
    }

    [System.Serializable]
    public class CameraConfigData
    {
        public string name;
        public Vector3 position;
        public Vector3 rotation;
        public float fov = 60f;
        public float nearClip = 0.1f;
        public float farClip = 1000f;
    }

    [System.Serializable]
    public class LightConfigData
    {
        public string name;
        public Vector3 position;
        public Vector3 rotation;
        public string lightType = "Directional";
        public string color = "FFFFFF";
        public float intensity = 1f;
    }

    [System.Serializable]
    public class ObjectData
    {
        public string name;
        public Vector3 position;
        public Vector3 rotation;
        public Vector3 scale = Vector3.one;
        public bool isActive = true;
        public bool hasMesh;
        public bool hasCollider;
        public bool hasAnimator;
        public int childCount;
        public List<ObjectData> children = new List<ObjectData>();
    }
}
