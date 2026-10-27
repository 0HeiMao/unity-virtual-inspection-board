using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VirtualInspection;

namespace VirtualInspection.Editor
{
    [InitializeOnLoad]
    public static class InspectionProjectSetup
    {
        private const string ScenePath = "Assets/Scenes/InspectionScene.unity";

        static InspectionProjectSetup()
        {
            EditorApplication.delayCall += EnsureProjectScene;
        }

        [MenuItem("Virtual Inspection/Open Inspection Scene")]
        public static void OpenInspectionScene()
        {
            EnsureProjectScene();
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
        }

        private static void EnsureProjectScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            Directory.CreateDirectory("Assets/Scenes");
            if (!File.Exists(ScenePath))
            {
                var currentScene = SceneManager.GetActiveScene();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var appObject = new GameObject("Inspection Application");
                appObject.AddComponent<InspectionApp>();
                EditorSceneManager.SaveScene(scene, ScenePath);
                if (currentScene.path == string.Empty)
                {
                    EditorSceneManager.OpenScene(ScenePath);
                }
            }

            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(scene => scene.path != ScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}
