using UnityEngine;
using UnityEngine.SceneManagement;

namespace VirtualInspection
{
    public static class InspectionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartApplication()
        {
            if (SceneManager.GetActiveScene().name == "ARSimulationScene" ||
                Object.FindObjectOfType<InspectionApp>() != null)
            {
                return;
            }
            new GameObject("Inspection Application").AddComponent<InspectionApp>();
        }
    }
}
