using UnityEngine;

namespace VirtualInspection
{
    public sealed class IssueMarker : MonoBehaviour
    {
        public Issue Issue { get; private set; }
        public bool IsPending { get; private set; }

        public void ConfigureSaved(Issue issue)
        {
            Issue = issue;
            IsPending = false;
        }

        public void ConfigurePending()
        {
            Issue = null;
            IsPending = true;
        }
    }
}
