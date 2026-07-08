using System.Collections.Generic;
using UnityEngine;
using SceneProductionToolkit.Data;

namespace SceneProductionToolkit.Runtime
{
    /// <summary>
    /// 相机路径录制器
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraPathRecorder : MonoBehaviour
    {
        [Header("录制设置")]
        [SerializeField] private float captureRate = 30f;
        [SerializeField] private bool autoKeyframe = true;
        [SerializeField] private float autoKeyframeInterval = 0.5f;
        [SerializeField] private bool generateRenderTexture = false;
        [SerializeField] private int rtResolution = 512;

        [Header("状态")]
        [SerializeField] private bool isRecording = false;
        [SerializeField] private string pathName = "默认路径";

        private Camera targetCamera;
        private RecordingData currentRecording;
        private float captureTimer;
        private float autoKeyTimer;
        private float totalTime;
        private int frameCount;
        private RenderTexture previewRT;

        private Vector3 lastKeyPosition;
        private Quaternion lastKeyRotation;
        private float lastKeyFov;

        // 撤销栈 - 用 List 模拟 Stack
        private List<List<CameraKeyframe>> undoStack;
        private const int MaxUndoDepth = 20;

        public bool IsRecording => isRecording;
        public RecordingData CurrentRecording => currentRecording;
        public float RecordedDuration => totalTime;
        public int FrameCount => frameCount;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            undoStack = new List<List<CameraKeyframe>>();
        }

        private void OnEnable()
        {
            if (generateRenderTexture && previewRT == null)
            {
                previewRT = new RenderTexture(rtResolution, rtResolution, 24, RenderTextureFormat.ARGBHalf);
                targetCamera.targetTexture = previewRT;
            }
        }

        private void OnDisable()
        {
            if (previewRT != null && targetCamera != null)
            {
                targetCamera.targetTexture = null;
            }
        }

        private void OnDestroy()
        {
            if (previewRT != null)
            {
                previewRT.Release();
                Destroy(previewRT);
            }
        }

        private void Update()
        {
            if (!isRecording || currentRecording == null) return;

            float dt = Time.deltaTime;
            totalTime += dt;
            captureTimer += dt;

            if (captureTimer >= 1f / captureRate)
            {
                captureTimer -= 1f / captureRate;
                CaptureFrame();
            }

            if (autoKeyframe)
            {
                autoKeyTimer += dt;
                if (autoKeyTimer >= autoKeyframeInterval)
                {
                    autoKeyTimer = 0f;
                    TryInsertKeyframe();
                }
            }
        }

        public void StartRecording(string name = null)
        {
            if (isRecording) return;

            isRecording = true;
            pathName = name ?? $"路径_{System.DateTime.Now:yyyyMMdd_HHmmss}";
            totalTime = 0f;
            frameCount = 0;
            captureTimer = 0f;
            autoKeyTimer = 0f;

            currentRecording = new RecordingData
            {
                pathName = pathName,
                recordedAtMs = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                frameRate = captureRate,
                sceneGuid = gameObject.scene.name
            };

            CaptureFrame();
            InsertKeyframe(true);

            Debug.Log($"[路径录制] 开始录制: {pathName}");
        }

        public RecordingData StopRecording()
        {
            if (!isRecording) return currentRecording;

            isRecording = false;

            if (currentRecording != null)
            {
                currentRecording.totalDuration = totalTime;
                currentRecording.totalFrames = frameCount;
            }

            Debug.Log($"[路径录制] 完成: {pathName}, 时长 {totalTime:F2}s, {frameCount} 帧, {currentRecording?.keyframes.Count ?? 0} 个关键帧");

            var result = currentRecording;
            currentRecording = null;
            return result;
        }

        public void InsertManualKeyframe(string label = null)
        {
            if (!isRecording) return;
            InsertKeyframe(true, label);
            autoKeyTimer = 0f;
        }

        public void PushUndoState()
        {
            if (currentRecording == null) return;
            undoStack.Add(new List<CameraKeyframe>(currentRecording.keyframes));
            if (undoStack.Count > MaxUndoDepth)
            {
                undoStack[0].Clear();
                undoStack.RemoveAt(0);
            }
        }

        public bool UndoKeyframes()
        {
            if (undoStack.Count == 0 || currentRecording == null) return false;
            currentRecording.keyframes = undoStack[undoStack.Count - 1];
            undoStack.RemoveAt(undoStack.Count - 1);
            return true;
        }

        public void ClearPath()
        {
            if (currentRecording == null) return;
            PushUndoState();
            currentRecording.keyframes.Clear();
            totalTime = 0f;
            frameCount = 0;
        }

        public void LoadRecordingData(RecordingData data)
        {
            currentRecording = data;
            if (data.keyframes.Count > 0)
            {
                var last = data.keyframes[data.keyframes.Count - 1];
                totalTime = last.time;
                frameCount = data.totalFrames;
            }
        }

        public RenderTexture GetPreviewRT()
        {
            if (previewRT == null && generateRenderTexture)
            {
                previewRT = new RenderTexture(rtResolution, rtResolution, 24, RenderTextureFormat.ARGBHalf);
                targetCamera.targetTexture = previewRT;
            }
            return previewRT;
        }

        private void CaptureFrame()
        {
            if (currentRecording == null) return;

            currentRecording.keyframes.Add(new CameraKeyframe
            {
                time = totalTime,
                position = transform.position,
                rotation = transform.rotation,
                fov = targetCamera.fieldOfView,
                nearClip = targetCamera.nearClipPlane,
                farClip = targetCamera.farClipPlane,
                isKey = false
            });
            frameCount++;
        }

        private void TryInsertKeyframe()
        {
            if (Vector3.Distance(transform.position, lastKeyPosition) > 0.1f ||
                Quaternion.Angle(transform.rotation, lastKeyRotation) > 2f ||
                Mathf.Abs(targetCamera.fieldOfView - lastKeyFov) > 1f)
            {
                InsertKeyframe(false);
            }
        }

        private void InsertKeyframe(bool forceKey, string label = null)
        {
            if (currentRecording == null) return;

            currentRecording.keyframes.Add(new CameraKeyframe
            {
                time = totalTime,
                position = transform.position,
                rotation = transform.rotation,
                fov = targetCamera.fieldOfView,
                nearClip = targetCamera.nearClipPlane,
                farClip = targetCamera.farClipPlane,
                isKey = true,
                label = label
            });

            lastKeyPosition = transform.position;
            lastKeyRotation = transform.rotation;
            lastKeyFov = targetCamera.fieldOfView;
        }
    }
}
