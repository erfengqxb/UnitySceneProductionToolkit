# Unity Scene Production Toolkit 🎬

[![GitHub](https://img.shields.io/badge/GitHub-erfengqxb/UnitySceneProductionToolkit-181717?logo=github)](https://github.com/erfengqxb/UnitySceneProductionToolkit)
![Unity](https://img.shields.io/badge/Unity-2022.3_LTS-000000?logo=unity)
![C#](https://img.shields.io/badge/C%23-_.NET_Standard_2.1-239120?logo=csharp)

**Unity 三维场景内容生产工具集**

一站式 Unity 场景生产管线工具包 — 覆盖**资产导入 → 场景搭建 → 录制流程 → 质量检查 → 批量导出**全链路。专为三维场景内容生产（仿真、数字孪生、虚拟拍摄、场景扫描重建）设计。

---

## 📦 项目结构

```
Assets/
├── Editor/                          # 编辑器扩展工具
│   ├── AssetImport/                 # 资产导入工具
│   │   └── AssetImportToolWindow.cs
│   ├── SceneRecording/              # 场景录制工具
│   │   ├── RecordingToolWindow.cs
│   │   └── PathEditorWindow.cs
│   ├── SceneInspector/              # 场景质量检查工具
│   │   └── SceneInspectorWindow.cs
│   └── ExportTools/                 # 批量导出工具
│       └── BatchExportWindow.cs
├── Scripts/
│   ├── Runtime/                     # 运行时脚本
│   │   ├── CameraPathRecorder.cs    # 相机路径录制
│   │   ├── CameraPathPlayer.cs      # 相机路径回放
│   │   ├── NpcController.cs         # NPC 控制器
│   │   ├── PathFollower.cs          # 路径跟随（NavMesh/Transform）
│   │   └── SceneConfigLoader.cs     # 场景配置加载器
│   ├── Data/                        # 数据模型
│   │   ├── RecordingData.cs         # 录制数据模型
│   │   ├── AssetImportConfig.cs     # 导入配置模型
│   │   └── SceneCheckResult.cs      # 检查结果模型
│   └── EditorCommon/                # Editor 共享工具
│       └── EditorGuiUtils.cs
├── Prefabs/                         # 预设体
├── Scenes/                          # 场景文件
├── Resources/                       # 运行时资源
└── StreamingAssets/                 # 外部配置文件
```

---

## 🧰 工具集功能

### 1. 资产导入工具 `AssetImportToolWindow`

`Window > Production Tools > Asset Import Tool`

| 功能 | 描述 |
|------|------|
| 批量导入 | 支持 FBX / OBJ / glTF / GLB 拖入批量处理 |
| 导入预设 | 按场景类型（室内/室外/人模/道具）保存不同的导入参数 |
| 自动材质映射 | 根据命名规则自动匹配 URP/Lit 材质 |
| 比例修正 | 检测并自动修正单位比例（cm/m/inch） |
| 网格分析 | 显示顶点数、三角面数、LOD 建议 |
| 碰撞体生成 | 自动生成 MeshCollider / BoxCollider 组合 |

### 2. 场景录制工具 `RecordingToolWindow` + `PathEditorWindow`

`Window > Production Tools > Scene Recording`

| 功能 | 描述 |
|------|------|
| 相机路径录制 | 实时录制场景相机 Transform，支持多条路径 |
| 路径编辑 | 可视化编辑路径关键帧（位置/旋转/速度曲线） |
| 路径回放 | 多模式回放（单次/循环/往返），支持平滑插值 |
| NPC 放置 | 一键从 Prefab 库放置 NPC 到路径关键点 |
| 动画控制 | 配置 NPC 行走/待机/看向相机动画参数 |
| 录制预设 | 保存/加载完整录制配置（JSON） |

### 3. 场景检查工具 `SceneInspectorWindow`

`Window > Production Tools > Scene Inspector`

| 检查项 | 说明 |
|--------|------|
| 🔍 遮挡检查 | 检测物体间不合理遮挡（相机→目标被挡） |
| 🔍 穿模检测 | 检测静态物体间 Mesh 交叉穿透 |
| 🔍 视角检查 | 检查关键视角是否有物体遮挡视线 |
| 🔍 光照检查 | 检测未烘焙、漏光、过曝/过暗区域 |
| 🔍 材质检查 | 检测缺失材质、错误 Shader、引用丢失 |
| 🔍 性能检查 | 统计 DrawCall、顶点数、Overdraw 区域 |
| 🔍 Transform 检查 | 检测 Scale 归一化、旋转精度问题 |

### 4. 批量导出工具 `BatchExportWindow`

`Window > Production Tools > Batch Export`

| 功能 | 描述 |
|------|------|
| 场景截图 | 批量生成场景多角度截图（带时间戳） |
| JSON 配置导出 | 导出场景物体列表、Transform、材质信息为 JSON |
| 录制数据导出 | 导出相机路径、NPC 数据为标准化格式 |
| 检查报告生成 | 导出 HTML / Markdown 格式的场景检查报告 |
| 批量处理 | 多场景队列导出，支持命令行调参 |

---

## 🚀 快速开始

### 环境要求

- **Unity 2022.3 LTS** 或更高版本
- **渲染管线**: URP (推荐) / Built-in
- 建议开启 **Addressables** 以管理大型资产

### 使用步骤

1. 用 Unity Hub 打开本项目根目录
2. 打开 `Assets/Scenes/Demo_Scene.unity`
3. 菜单栏 `Window > Production Tools` 选择工具
4. 参考 `Docs/WorkflowGuide.md` 完成完整生产流程

### 流程演示

```
[导入资产] → [场景搭建] → [配置录制] → [播放角色] → [检查问题] → [导出结果]
     ↓             ↓             ↓             ↓             ↓           ↓
 AssetImport   SceneSetup    RecordPath    Add NPC +     Inspector   BatchExport
   Tool        manually     camera path    Animator       Tool        Reports
```

---

## 🔧 核心脚本说明

| 脚本 | 涉及 JD 技能点 |
|------|---------------|
| `CameraPathRecorder` / `CameraPathPlayer` | Camera·Transform·平滑插值·录制流程·RenderTexture |
| `NpcController` | Animator·角色动画·Transform·状态机 |
| `PathFollower` | NavMesh·路径控制·角色移动 |
| `SceneConfigLoader` | JSON 读写·配置管理·File I/O |
| `AssetImportToolWindow` | FBX/OBJ/glTF·材质·Prefab·批量处理·Editor 工具 |
| `RecordingToolWindow` | 编辑器 UI·相机配置·NPC 放置·录制预设 |
| `SceneInspectorWindow` | 渲染调试·碰撞检测·性能分析·QA 流程 |

---

## 📸 作品展示建议

将此项目放入简历时，建议附上：

1. **GitHub 链接** — 包含代码、README 和截图
2. **工具使用录屏** — 展示录制工具操作过程
3. **场景对比截图** — 展示 SceneInspector 发现并解决问题前后
4. **Demo 场景包** — 含 1-2 个完成全流程的场景

---

## 📄 License

MIT — 可自由用于个人作品集和面试展示。
