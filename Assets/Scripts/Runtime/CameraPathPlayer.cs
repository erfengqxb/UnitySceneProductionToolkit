using System.Collections.Generic;
using UnityEngine;
using SceneProductionToolkit.Data;

namespace SceneProductionToolkit.Runtime
{
    /// <summary>
    /// 相机路径回放器
    /// </summary>
    public class CameraPathPlayer : MonoBehaviour
    {
        [Header("播放设置")]
        [SerializeField] private PlaybackMode playbackMode = PlaybackMode.PingPong;
        [SerializeField] private InterpolationMode interpolation = InterpolationMode.CubicSpline;
        [SerializeField] private float playbackSpeed = 1f;
        [SerializeField] private bool useEasing = true;
        [SerializeField] private AnimationCurve easingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("目标跟踪")]
        [SerializeField] private bool lookAtTarget;
        [SerializeField] private Transform lookAtTransform;
        [SerializeField] private Vector3 lookAtOffset;

        [Header("抖动模拟")]
        [SerializeField] private bool enableHandheldShake;
        [SerializeField] [Range(0f, 0.5f)] private float shakeAmount = 0.02f;
        [SerializeField] private float shakeFrequency = 1.5f;

        [Header("状态")]
        [SerializeField] private bool isPlaying;
        [SerializeField] private float currentTime;
        [SerializeField] private bool isPaused;

        private RecordingData recordingData;
        private Camera targetCamera;
        private float totalDuration;
        private bool playingForward = true;
        private Vector3 shakeOffset;

        public bool IsPlaying => isPlaying;
        public bool IsPaused => isPaused;
        public float CurrentTime => currentTime;
        public float TotalDuration => totalDuration;
        public float Progress => totalDuration > 0f ? currentTime / totalDuration : 0f;
        public RecordingData LoadedData => recordingData;

        private void Awake()
        {
            targetCamera = GetComponent<Camera>();
            if (easingCurve.keys.Length == 0)
                easingCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        }

        private void Update()
        {
            if (!isPlaying || isPaused || recordingData == null) return;
            if (recordingData.keyframes.Count < 2) return;

            float dt = Time.deltaTime * playbackSpeed;

            if (playingForward)
            {
                currentTime += dt;
                if (currentTime >= totalDuration)
                    HandlePlaybackEnd();
            }
            else
            {
                currentTime -= dt;
                if (currentTime <= 0f)
                {
                    currentTime = 0f;
                    HandlePlaybackEnd();
                }
            }

            ApplyCameraAtTime(currentTime);

            if (enableHandheldShake && shakeAmount > 0f)
                ApplyHandheldShake();
        }

        public void LoadRecording(RecordingData data)
        {
            if (data == null)
            {
                Debug.LogWarning("[路径播放] 录制数据为空");
                return;
            }

            recordingData = data;
            totalDuration = data.totalDuration > 0f ? data.totalDuration : GetDurationFromKeys(data.keyframes);

            if (data.keyframes.Count > 0)
                ApplyCameraAtTime(0f);

            Debug.Log($"[路径播放] 加载路径: {data.pathName}, {data.keyframes.Count} 帧, 时长 {totalDuration:F2}s");
        }

        public void Play()
        {
            if (recordingData == null || recordingData.keyframes.Count < 2)
            {
                Debug.LogWarning("[路径播放] 路径数据不足");
                return;
            }

            isPlaying = true;
            isPaused = false;
            playingForward = true;

            if (currentTime >= totalDuration)
                currentTime = 0f;
        }

        public void TogglePause()
        {
            if (!isPlaying) return;
            isPaused = !isPaused;
        }

        public void Stop()
        {
            isPlaying = false;
            isPaused = false;
            currentTime = 0f;
            playingForward = true;

            if (recordingData != null && recordingData.keyframes.Count > 0)
                ApplyCameraAtTime(0f);
        }

        public void Seek(float time)
        {
            currentTime = Mathf.Clamp(time, 0f, totalDuration);
            if (recordingData != null)
                ApplyCameraAtTime(currentTime);
        }

        public void SetInterpolationMode(InterpolationMode mode) { interpolation = mode; }
        public void SetPlaybackSpeed(float speed) { playbackSpeed = Mathf.Max(0.01f, speed); }

        private void ApplyCameraAtTime(float time)
        {
            if (recordingData == null || recordingData.keyframes.Count < 2) return;

            var keys = recordingData.keyframes;
            float easedTime = useEasing && totalDuration > 0f
                ? easingCurve.Evaluate(time / totalDuration) * totalDuration
                : time;

            int indexA = 0;
            int indexB = keys.Count - 1;
            for (int i = 0; i < keys.Count - 1; i++)
            {
                if (easedTime >= keys[i].time && easedTime <= keys[i + 1].time)
                {
                    indexA = i;
                    indexB = i + 1;
                    break;
                }
            }

            float segmentDuration = keys[indexB].time - keys[indexA].time;
            float t = segmentDuration > 0f ? Mathf.Clamp01((easedTime - keys[indexA].time) / segmentDuration) : 0f;

            transform.position = InterpolatePosition(keys, indexA, indexB, t);
            transform.rotation = InterpolateRotation(keys, indexA, indexB, t);

            if (targetCamera != null)
                targetCamera.fieldOfView = Mathf.Lerp(keys[indexA].fov, keys[indexB].fov, t);

            if (lookAtTarget && lookAtTransform != null)
            {
                Vector3 dir = lookAtTransform.position + lookAtOffset - transform.position;
                if (dir.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(dir);
            }
        }

        private Vector3 InterpolatePosition(List<CameraKeyframe> keys, int idxA, int idxB, float t)
        {
            switch (interpolation)
            {
                case InterpolationMode.Linear:
                    return Vector3.Lerp(keys[idxA].position, keys[idxB].position, t);
                case InterpolationMode.SmoothStep:
                    float s = t * t * (3f - 2f * t);
                    return Vector3.Lerp(keys[idxA].position, keys[idxB].position, s);
                case InterpolationMode.CatmullRom:
                    int idx0 = Mathf.Max(0, idxA - 1);
                    int idx3 = Mathf.Min(keys.Count - 1, idxB + 1);
                    return CatmullRom(keys[idx0].position, keys[idxA].position, keys[idxB].position, keys[idx3].position, t);
                case InterpolationMode.CubicSpline:
                case InterpolationMode.Hermite:
                    return HermiteInterpolate(keys[idxA].position, GetTangent(keys, idxA),
                        keys[idxB].position, GetTangent(keys, idxB), t);
                default:
                    return Vector3.Lerp(keys[idxA].position, keys[idxB].position, t);
            }
        }

        private Quaternion InterpolateRotation(List<CameraKeyframe> keys, int idxA, int idxB, float t)
        {
            float s = interpolation == InterpolationMode.SmoothStep ? t * t * (3f - 2f * t) : t;
            return Quaternion.Slerp(keys[idxA].rotation, keys[idxB].rotation, s);
        }

        private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private Vector3 HermiteInterpolate(Vector3 p1, Vector3 t1, Vector3 p2, Vector3 t2, float t)
        {
            float t2f = t * t, t3 = t2f * t;
            float h1 = 2f * t3 - 3f * t2f + 1f, h2 = -2f * t3 + 3f * t2f;
            float h3 = t3 - 2f * t2f + t, h4 = t3 - t2f;
            return h1 * p1 + h2 * p2 + h3 * t1 + h4 * t2;
        }

        private Vector3 GetTangent(List<CameraKeyframe> keys, int index)
        {
            if (index <= 0) return keys[1].position - keys[0].position;
            if (index >= keys.Count - 1) return keys[index].position - keys[index - 1].position;
            return (keys[index + 1].position - keys[index - 1].position) * 0.5f;
        }

        private void HandlePlaybackEnd()
        {
            switch (playbackMode)
            {
                case PlaybackMode.Once:
                    currentTime = totalDuration;
                    ApplyCameraAtTime(totalDuration);
                    isPlaying = false;
                    break;
                case PlaybackMode.Loop:
                    currentTime = 0f;
                    break;
                case PlaybackMode.PingPong:
                    playingForward = !playingForward;
                    currentTime = playingForward ? 0f : totalDuration;
                    break;
            }
        }

        private void ApplyHandheldShake()
        {
            float t = Time.time * shakeFrequency;
            shakeOffset.x = (Mathf.PerlinNoise(t, 0f) * 2f - 1f) * shakeAmount;
            shakeOffset.y = (Mathf.PerlinNoise(0f, t + 100f) * 2f - 1f) * shakeAmount;
            shakeOffset.z = (Mathf.PerlinNoise(t + 200f, t + 300f) * 2f - 1f) * shakeAmount;

            transform.position += transform.right * shakeOffset.x + transform.up * shakeOffset.y + transform.forward * shakeOffset.z;
        }

        private float GetDurationFromKeys(List<CameraKeyframe> keys)
        {
            return keys.Count > 0 ? keys[keys.Count - 1].time : 0f;
        }
    }
}
