using UnityEngine;

namespace VirtualInspection
{
    public sealed class InspectableSurface : MonoBehaviour
    {
        [SerializeField] private string displayName = "巡检表面";

        public string DisplayName => displayName;

        public void Configure(string surfaceName)
        {
            displayName = surfaceName;
        }
    }
}
