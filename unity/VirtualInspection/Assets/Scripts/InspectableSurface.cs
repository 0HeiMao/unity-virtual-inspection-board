using UnityEngine;

namespace VirtualInspection
{
    public sealed class InspectableSurface : MonoBehaviour
    {
        [SerializeField] private string displayName = "巡检表面";
        [SerializeField] private string areaName = "未归属区域";

        public string DisplayName => displayName;

        public string AreaName => areaName;

        public void Configure(string surfaceName)
        {
            Configure(surfaceName, areaName);
        }

        public void Configure(string surfaceName, string area)
        {
            if (!string.IsNullOrWhiteSpace(surfaceName))
            {
                displayName = surfaceName.Trim();
            }
            if (!string.IsNullOrWhiteSpace(area))
            {
                areaName = area.Trim();
            }
        }
    }
}
