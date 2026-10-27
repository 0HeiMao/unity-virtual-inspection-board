using System;
using UnityEngine;

namespace VirtualInspection
{
    [Serializable]
    public sealed class IssuePosition
    {
        public float x;
        public float y;
        public float z;

        public static IssuePosition FromWorld(Vector3 worldPosition)
        {
            return new IssuePosition
            {
                x = worldPosition.x,
                y = worldPosition.y,
                z = worldPosition.z
            };
        }

        public Vector3 ToWorld()
        {
            return new Vector3(x, y, z);
        }
    }

    [Serializable]
    public sealed class Issue
    {
        public string id;
        public string title;
        public string description;
        public string priority;
        public string status;
        public IssuePosition position;
        public string createdAt;
        public string updatedAt;
    }

    [Serializable]
    public sealed class CreateIssueRequest
    {
        public string title;
        public string description;
        public string priority;
        public IssuePosition position;
    }

    [Serializable]
    public sealed class IssueListResponse
    {
        public Issue[] issues;
    }

    [Serializable]
    public sealed class ApiErrorResponse
    {
        public string error;
    }
}
