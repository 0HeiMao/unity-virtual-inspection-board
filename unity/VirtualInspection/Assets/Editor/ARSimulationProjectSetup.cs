using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Simulation;
using VirtualInspection;

namespace VirtualInspection.Editor
{
    [InitializeOnLoad]
    public static class ARSimulationProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/ARSimulationScene.unity";

        static ARSimulationProjectSetup()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-openARSimulation") >= 0)
            {
                EditorApplication.delayCall += OpenArScene;
            }
            else
            {
                EditorApplication.delayCall += EnsureConfigured;
            }
        }

        [MenuItem("Virtual Inspection/Configure AR Simulation")]
        public static void EnsureConfigured()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            ConfigureXrSettings();
            EnsureArScene();
        }

        [MenuItem("Virtual Inspection/Open AR Simulation Scene")]
        public static void OpenArScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            EnsureConfigured();
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
        }

        private static void ConfigureXrSettings()
        {
            var store = GetOrCreateSettingsStore();
            if (!store.HasSettingsForBuildTarget(BuildTargetGroup.Standalone))
            {
                store.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Standalone);
            }
            if (!store.HasManagerSettingsForBuildTarget(BuildTargetGroup.Standalone))
            {
                store.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Standalone);
            }

            var generalSettings = store.SettingsForBuildTarget(BuildTargetGroup.Standalone);
            generalSettings.InitManagerOnStart = true;
            var manager = generalSettings.Manager;
            manager.automaticLoading = true;
            manager.automaticRunning = true;

            const string loaderName = "UnityEngine.XR.Simulation.SimulationLoader";
            if (!XRPackageMetadataStore.IsLoaderAssigned(loaderName, BuildTargetGroup.Standalone))
            {
                XRPackageMetadataStore.AssignLoader(
                    manager,
                    typeof(SimulationLoader).FullName,
                    BuildTargetGroup.Standalone);
            }

            EditorUtility.SetDirty(generalSettings);
            EditorUtility.SetDirty(manager);
            AssetDatabase.SaveAssets();
        }

        private static XRGeneralSettingsPerBuildTarget GetOrCreateSettingsStore()
        {
            var method = typeof(XRGeneralSettingsPerBuildTarget).GetMethod(
                "GetOrCreate",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (method == null)
            {
                throw new MissingMethodException("XRGeneralSettingsPerBuildTarget.GetOrCreate");
            }
            return (XRGeneralSettingsPerBuildTarget)method.Invoke(null, null);
        }

        private static void EnsureArScene()
        {
            Directory.CreateDirectory("Assets/Scenes");
            if (!File.Exists(ScenePath))
            {
                var activeScene = SceneManager.GetActiveScene();
                if (string.IsNullOrEmpty(activeScene.path) && activeScene.isDirty)
                {
                    Debug.LogWarning("AR Simulation scene was not created because the active untitled scene has unsaved changes.");
                    return;
                }

                var additive = !string.IsNullOrEmpty(activeScene.path);
                var scene = EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    additive ? NewSceneMode.Additive : NewSceneMode.Single);
                var appObject = new GameObject("AR Simulation Application");
                appObject.AddComponent<ARInspectionApp>();
                EnsureArRig();
                EditorSceneManager.SaveScene(scene, ScenePath);
                if (additive)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
            else
            {
                var activeScene = SceneManager.GetActiveScene();
                var scene = SceneManager.GetSceneByPath(ScenePath);
                var additive = !scene.IsValid() || !scene.isLoaded;
                if (additive)
                {
                    scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                }
                if (scene.IsValid() && scene.isLoaded)
                {
                    SceneManager.SetActiveScene(scene);
                    EnsureArRig();
                    EditorSceneManager.SaveScene(scene);
                    if (activeScene.IsValid() && activeScene != scene)
                    {
                        SceneManager.SetActiveScene(activeScene);
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }

            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(scene => scene.path != ScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, false));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        private static void EnsureArRig()
        {
            var session = UnityEngine.Object.FindObjectOfType<ARSession>();
            if (session == null)
            {
                var sessionObject = new GameObject("AR Session");
                sessionObject.AddComponent<ARSession>();
                sessionObject.AddComponent<ARInputManager>();
            }

            var origin = UnityEngine.Object.FindObjectOfType<XROrigin>();
            if (origin == null)
            {
                var originObject = new GameObject("XR Origin");
                origin = originObject.AddComponent<XROrigin>();

                var offset = new GameObject("Camera Offset");
                offset.transform.SetParent(originObject.transform, false);

                var cameraObject = new GameObject("AR Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.transform.SetParent(offset.transform, false);
                var camera = cameraObject.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.018f, 0.03f, 0.038f);
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 80f;
                cameraObject.AddComponent<AudioListener>();
                cameraObject.AddComponent<ARCameraManager>();
                cameraObject.AddComponent<ARCameraBackground>();

                var poseDriver = cameraObject.AddComponent<TrackedPoseDriver>();
                var positionAction = new InputAction(
                    "Position",
                    InputActionType.Value,
                    "<XRHMD>/centerEyePosition",
                    expectedControlType: "Vector3");
                positionAction.AddBinding("<HandheldARInputDevice>/devicePosition");
                poseDriver.positionInput = new InputActionProperty(positionAction);

                var rotationAction = new InputAction(
                    "Rotation",
                    InputActionType.Value,
                    "<XRHMD>/centerEyeRotation",
                    expectedControlType: "Quaternion");
                rotationAction.AddBinding("<HandheldARInputDevice>/deviceRotation");
                poseDriver.rotationInput = new InputActionProperty(rotationAction);

#pragma warning disable CS0618
                cameraObject.AddComponent<ARPoseDriver>();
#pragma warning restore CS0618

                origin.CameraFloorOffsetObject = offset;
                origin.Camera = camera;
            }

            if (origin.GetComponent<ARPlaneManager>() == null)
            {
                var planeManager = origin.gameObject.AddComponent<ARPlaneManager>();
                planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            }
            if (origin.GetComponent<ARRaycastManager>() == null)
            {
                origin.gameObject.AddComponent<ARRaycastManager>();
            }
            if (origin.GetComponent<ARAnchorManager>() == null)
            {
                origin.gameObject.AddComponent<ARAnchorManager>();
            }

            UnityEngine.Object.FindObjectOfType<ARSession>().enabled = true;
            UnityEngine.Object.FindObjectOfType<ARCameraManager>().enabled = true;
            UnityEngine.Object.FindObjectOfType<ARCameraBackground>().enabled = true;
            origin.GetComponent<ARPlaneManager>().enabled = true;
            origin.GetComponent<ARRaycastManager>().enabled = true;
            origin.GetComponent<ARAnchorManager>().enabled = true;
        }
    }
}
