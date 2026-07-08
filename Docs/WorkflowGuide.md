# 场景内容生产工作流指南

## 完整生产流程

```
[准备资产] → [导入 Unity] → [场景搭建] → [配置录制] → [QA 检查] → [批量导出]
```

---

## 第一步：资产准备与导入

### 支持格式
| 格式 | 说明 | 注意事项 |
|------|------|---------|
| FBX | 推荐，支持动画/蒙皮 | 确认单位设置 |
| OBJ | 仅几何体 | 需单独处理材质 |
| glTF/GLB | PBR 材质支持好 | 检查纹理引用路径 |
| .blend | 直接导入（需安装 Blender） | 可能需要清理数据 |

### 推荐工作流

1. **打开 Asset Import Tool** (`Window > Production Tools > Asset Import Tool`)
2. **选择预设**：按场景类型选择导入预设
   - 室内场景 → "室内场景"预设 (cm → m, 0.01x)
   - 室外场景 → "室外场景"预设 (保持 1:1)
   - 角色模型 → "角色模型"预设 (带动画)
   - 3DGS 扫描 → "3DGS 扫描重建"预设 (LOD+优化)
3. **拖入文件**：将 FBX/OBJ/glTF 文件拖入导入队列
4. **批量导入**：点击"开始导入"
5. **检查结果**：查看统计区的顶点数、三角面和警告

### 导入后检查清单
- [ ] 比例是否正确？（1 Unity 单位 = 1 米）
- [ ] 材质是否正确转换到 URP？
- [ ] 碰撞体是否正确生成？
- [ ] 纹理是否完整引用？

---

## 第二步：场景搭建

### 场景结构规范

```
场景名称/
├── _Lights/          # 所有灯光
├── _Cameras/         # 所有相机
├── _Environment/     # 环境（天空盒、雾效）
├── _Geometry/        # 静态网格
├── _NPCs/            # 角色/人物
├── _Interactive/     # 可交互物体
└── _Audio/           # 音源
```

### Transform 规范
- 根物体 Scale 保持 (1,1,1)
- 避免负 Scale（会导致法线翻转）
- 位置尽量接近原点（避免浮点精度问题）

### 命名规范
- 使用英文命名，`PascalCase`
- 后缀约定：`_LOD0`, `_Collider`, `_UI`
- 材质命名通配示例：`Wall_Brick_01`, `Floor_Tile_02`

---

## 第三步：录制配置

### 创建录制相机

1. 点击 Recording Tool 中"创建录制相机"
2. 将相机放在场景中合适位置
3. 调整好初始视角

### 录制相机路径

1. 在 Recording Tool 中选中录制相机
2. 设置路径名称和录制帧率（30fps 推荐）
3. 点击"开始录制"
4. 在 Scene 视图中移动相机（可使用 WASD 飞行模式）
5. 需要标记关键帧时点击"插关键帧"
6. 录制完成后点击"停止录制"

### 路径编辑

1. 打开 Path Editor (`Window > Production Tools > Path Editor`)
2. 在 Scene 视图中拖拽关键帧手柄
3. 使用"平滑路径"减少抖动
4. 使用"简化关键帧"删除冗余帧

### 添加 NPC

1. 准备 NPC Prefab（含 Animator 组件）
2. 在 Recording Tool 中设置位置和初始动画
3. 点击"放置 NPC 到场景"
4. 为 NPC 添加路径跟随（PathFollower 组件）

---

## 第四步：质量检查

### 运行检查

1. 打开 Scene Inspector (`Window > Production Tools > Scene Inspector`)
2. 选择要启用的检查项
3. 点击"开始全面检查"

### 常见问题处理

| 问题 | 原因 | 解决方法 |
|------|------|---------|
| 遮挡 | 物体在相机和目标之间 | 调整物体透明度或位置 |
| 穿模 | 静态物体 Mesh 交叉 | 手动调整位置或删除重叠物体 |
| 视角阻挡 | 物体紧贴相机 | 移除或移开阻挡物 |
| 过曝光照 | 光照强度过高 | 降低强度或调整曝光 |
| 缺失材质 | Material slot 为空 | 分配正确材质 |
| 负 Scale | Transform Scale 含负数 | 设为正值或旋转替代 |

### 评分标准
- **90-100**: 优秀，可直接交付
- **70-89**: 良好，修复建议中的 Warning
- **50-69**: 需要修改，修复 Error 级别问题
- **<50**: 严重问题，需要全面修复

---

## 第五步：导出交付

### 导出内容

1. 打开 Batch Export (`Window > Production Tools > Batch Export`)
2. 选择要导出的内容：
   - **场景截图**：多角度高清截图
   - **JSON 配置**：场景物体列表、Transform、材质信息
   - **录制数据**：相机路径、NPC 数据
   - **检查报告**：QA 结果 Markdown/HTML

### 交付物清单
```
Exports_20240101_120000/
├── screenshot_Front.png
├── screenshot_Back.png
├── screenshot_Left.png
├── screenshot_Right.png
├── screenshot_Top.png
├── scene_config.json       # 场景配置
├── recording_data.json     # 录制数据
└── inspection_report.md    # 检查报告
```

---

## 常见问题 FAQ

### Q: 导入的模型比例不对？
A: 检查源文件单位设置。3ds Max 通常用厘米，Blender 用米，选择对应的预设即可。

### Q: 材质全是粉色的？
A: Shader 丢失。在导入工具中勾选"转换为 URP 材质"重新导入。

### Q: 录制路径抖动剧烈？
A: 降低录制帧率，或录制后在 Path Editor 中使用"平滑路径"工具。

### Q: NPC 不走路？
A: 检查是否有 NavMesh（使用 NavMesh 模式时），或切换到 DirectTransform 模式。

### Q: 场景检查发现大量穿模？
A: 使用碰撞体代替精细 Mesh 碰撞，或开启"Compound"碰撞体类型。

---

## 快捷键参考

| 快捷键 | 功能 |
|--------|------|
| `Ctrl+Shift+A` | Asset Import Tool |
| `Ctrl+Shift+R` | Recording Tool |
| `Ctrl+Shift+I` | Scene Inspector |
| `Ctrl+Shift+E` | Batch Export |

> 注：以上快捷键需在 `Shortcuts Manager` 中自行绑定。
