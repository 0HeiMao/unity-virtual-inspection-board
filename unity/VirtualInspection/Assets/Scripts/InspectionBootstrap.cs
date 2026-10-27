using UnityEngine;

namespace VirtualInspection
{
    public static class InspectionBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartApplication()
        {
            if (Object.FindObjectOfType<InspectionApp>() != null)
            {
                return;
            }
            new GameObject("Inspection Application").AddComponent<InspectionApp>();
        }
    }
}
