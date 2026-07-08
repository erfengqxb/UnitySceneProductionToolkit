using System;
using System.Collections.Generic;
using UnityEngine;

namespace SceneProductionToolkit.Data
{
    /// <summary>
    /// 相机录制数据模型
    /// </summary>
    [Serializable]
    public class RecordingData
    {
        public string pathName;
        public long recordedAtMs;
        public float totalDuration;
        public int totalFrames;
        public float frameRate;
        public List<CameraKeyframe> keyframes = new List<CameraKeyframe>();
        public string[] tags;
        public string sceneGuid;
    }

    [Serializable]
    public struct CameraKeyframe
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
        public float fov;
        public float nearClip;
        public float farClip;
        public bool isKey;
        public string label;
    }

    [Serializable]
    public class RecordingPreset
    {
        public string presetName;
        public string description;
        public float defaultCaptureRate = 30f;
        public float pathSmoothness = 0.5f;
        public bool enableAutoKeyframe = true;
        public float autoKeyframeInterval = 0.5f;
        public bool loopPlayback;
        public bool pingPongPlayback = true;
        public float playbackSpeed = 1f;
        public CameraAnimationConfig cameraAnimation;
        public List<NpcPlacementConfig> npcPlacements = new List<NpcPlacementConfig>();
    }

    [Serializable]
    public class CameraAnimationConfig
    {
        public bool useEasing = true;
        public InterpolationMode interpolation = InterpolationMode.CubicSpline;
        public bool lookAtTarget;
        public string lookAtTargetPath;
        public bool lockYAxis;
        public float handheldShakeAmount;
        public float shakeFrequency = 1f;
    }

    [Serializable]
    public class NpcPlacementConfig
    {
        public string npcId;
        public string prefabPath;
        public Vector3 position;
        public Vector3 rotation;
        public float scale = 1f;
        public string animationState = "Idle";
        public bool enablePatrol;
        public List<Vector3> patrolPoints = new List<Vector3>();
        public float patrolSpeed = 1.2f;
        public bool lookAtCamera;
    }

    public enum InterpolationMode
    {
        Linear,
        SmoothStep,
        CatmullRom,
        CubicSpline,
        Hermite
    }

    public enum PlaybackMode
    {
        Once,
        Loop,
        PingPong
    }
}
