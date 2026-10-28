using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VirtualInspection.EditorTools
{
    /// <summary>
    /// 一次性演示脚本：打开巡检场景、进入 Play Mode、等后端标记恢复完成后
    /// 用一台临时相机渲染「全景 + 三个优先级的特写 + 环绕」素材，然后退出。
    /// 只在 Editor 侧使用，用 Unity.exe -executeMethod 调用。
    /// </summary>
    public static class MarkerDemoCapture
    {
        private const string ScenePath = "Assets/Scenes/InspectionScene.unity";
        private const int Width = 1280;
        private const int Height = 720;

        private static string _outDir;
        private static double _started;
        private static int _phase;
        private static Camera _cam;
        private static Transform _markerRoot;
        private static readonly List<Transform> _targets = new List<Transform>();
        private static Vector3 _center;
        private static float _radius = 6f;
        private static int _frame;
        private static double _phaseStart;
        private static readonly StringBuilder _report = new StringBuilder();

        [MenuItem("Tools/Virtual Inspection/Capture Marker Demo")]
        public static void Run()
        {
            _outDir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../.tools/unity-capture"));
            Directory.CreateDirectory(_outDir);
            Log("run start out=" + _outDir);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            _started = EditorApplication.timeSinceStartup;
            _phase = 0;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (_phase == 0)
                {
                    if (EditorApplication.timeSinceStartup - _started > 1.0 && !EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        EditorApplication.EnterPlaymode();
                        _phase = 1;
                        _phaseStart = EditorApplication.timeSinceStartup;
                        Log("enter playmode requested");
                    }
                    else if (EditorApplication.timeSinceStartup - _started > 60.0)
                    {
                        Fail("playmode timeout");
                    }

                    return;
                }

                if (_phase == 1)
                {
                    if (!EditorApplication.isPlaying) return;
                    CollectMarkers();
                    if (_targets.Count == 0 && EditorApplication.timeSinceStartup - _phaseStart > 15.0)
                    {
                        Fail("no markers restored from backend");
                        return;
                    }

                    if (_targets.Count >= 5 || EditorApplication.timeSinceStartup - _phaseStart > 15.0)
                    {
                        PrepareCamera();
                        _phase = 2;
                        _phaseStart = EditorApplication.timeSinceStartup;
                        Log("markers=" + _targets.Count);
                    }

                    return;
                }

                if (_phase == 2)
                {
                    // 全景
                    SetCamera(_center + new Vector3(0.4f, 4.2f, -9.5f), _center);
                    Capture("01-overview.png");
                    Log("shot overview center=" + Fmt(_center) + " radius=" + _radius.ToString("0.00", CultureInfo.InvariantCulture));

                    // 高优先级特写（1 号机柜）
                    int hi = IndexOf("1 号机柜");
                    Look(_targets[hi < 0 ? 0 : hi], out var hiPos);
                    Capture("02-high.png");

                    // 中优先级特写（冷却管线）
                    int mid = IndexOf("冷却管线");
                    Look(_targets[mid < 0 ? Mathf.Min(1, _targets.Count - 1) : mid], out var midPos);
                    Capture("03-medium.png");

                    // 低优先级特写（控制台）
                    int low = IndexOf("控制台");
                    Look(_targets[low < 0 ? _targets.Count - 1 : low], out var lowPos);
                    Capture("04-low.png");

                    Log("triplets high=" + Fmt(hiPos) + " medium=" + Fmt(midPos) + " low=" + Fmt(lowPos));
                    _phase = 3;
                    _phaseStart = EditorApplication.timeSinceStartup;
                    return;
                }

                if (_phase == 3)
                {
                    // 环绕：4 秒、约 30fps 的轨道镜头，能看清脉动与光晕
                    var t = (float)(EditorApplication.timeSinceStartup - _phaseStart);
                    if (t > 4.2f)
                    {
                        Log("frames=" + _frame);
                        Finish();
                        return;
                    }

                    var angle = Mathf.Lerp(-35f, 35f, t / 4.2f) * Mathf.Deg2Rad;
                    var dist = Mathf.Clamp(_radius * 1.9f, 7f, 12f);
                    var pos = _center + new Vector3(Mathf.Sin(angle) * dist, 3.6f, -Mathf.Cos(angle) * dist);
                    SetCamera(pos, _center);
                    Capture("orbit-" + _frame.ToString("000") + ".png");
                    _frame++;
                    return;
                }

                if (_phase == 4)
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        EditorApplication.update -= Tick;
                        _phase = 99;
                        Log("done at " + _outDir);
                        if (Application.isBatchMode) EditorApplication.Exit(0);
                    }
                    else if (EditorApplication.timeSinceStartup - _phaseStart > 30.0)
                    {
                        Log("WARN exit playmode timeout");
                        _phase = 99;
                        if (Application.isBatchMode) EditorApplication.Exit(3);
                    }
                }
            }
            catch (Exception error)
            {
                Fail("exception: " + error);
            }
        }

        private static void CollectMarkers()
        {
            if (_markerRoot == null)
            {
                var found = GameObject.Find("Issue Markers");
                if (found != null) _markerRoot = found.transform;
            }

            if (_markerRoot == null) return;

            _targets.Clear();
            for (var i = 0; i < _markerRoot.childCount; i++)
            {
                _targets.Add(_markerRoot.GetChild(i));
            }
        }

        private static int IndexOf(string keyword)
        {
            for (var i = 0; i < _targets.Count; i++)
            {
                var marker = _targets[i].GetComponentInChildren<IssueMarker>();
                var title = marker != null && marker.Issue != null ? marker.Issue.title : string.Empty;
                if (title.Contains(keyword)) return i;
            }

            return -1;
        }

        private static void PrepareCamera()
        {
            var camGo = new GameObject("Demo Capture Camera");
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.05f, 0.07f, 0.06f, 1f);
            _cam.fieldOfView = 55f;
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 200f;

            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            foreach (var target in _targets)
            {
                min = Vector3.Min(min, target.position);
                max = Vector3.Max(max, target.position);
            }

            _center = (min + max) * 0.5f;
            _radius = Mathf.Max(Vector3.Distance(min, max) * 0.5f, 3f);
        }

        private static void Look(Transform target, out Vector3 position)
        {
            position = target.position;
            var from = position + new Vector3(0.55f, 1.05f, -1.9f);
            SetCamera(from, position + new Vector3(0f, 0.05f, 0f));
        }

        private static void SetCamera(Vector3 pos, Vector3 lookAt)
        {
            _cam.transform.position = pos;
            _cam.transform.LookAt(lookAt);
        }

        private static void Capture(string name)
        {
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            _cam.targetTexture = rt;
            _cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            _cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(_outDir, name), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(rt);
        }

        private static string Fmt(Vector3 v)
        {
            return string.Format(CultureInfo.InvariantCulture, "({0:0.00},{1:0.00},{2:0.00})", v.x, v.y, v.z);
        }

        private static void Log(string message)
        {
            _report.AppendLine(message);
            Debug.Log("[MarkerDemoCapture] " + message);
        }

        private static void Fail(string reason)
        {
            Log("FAILED " + reason);
            File.WriteAllText(Path.Combine(_outDir, "report.txt"), _report.ToString(), Encoding.UTF8);
            EditorApplication.update -= Tick;
            _phase = 99;
            if (Application.isBatchMode) EditorApplication.Exit(2);
        }

        private static void Finish()
        {
            File.WriteAllText(Path.Combine(_outDir, "report.txt"), _report.ToString(), Encoding.UTF8);
            if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
            _phase = 4;
            _phaseStart = EditorApplication.timeSinceStartup;
        }
    }
}
