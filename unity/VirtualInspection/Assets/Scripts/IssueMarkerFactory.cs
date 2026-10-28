using UnityEngine;

namespace VirtualInspection
{
    public static class IssueMarkerFactory
    {
        /// <summary>
        /// 内置 "Ignore Raycast" 层。Physics.DefaultRaycastLayers 不含该层，
        /// 因此标记不会挡住其身后表面的命中点，落点始终取真实表面。
        /// 需要点选标记查看详情时，用 <see cref="MarkerMask"/> 单独做一次射线。
        /// </summary>
        public static readonly int MarkerLayer = Mathf.Max(LayerMask.NameToLayer("Ignore Raycast"), 0);

        /// <summary>只包含标记层的射线掩码。</summary>
        public static readonly int MarkerMask = 1 << MarkerLayer;

        /// <summary>可巡检表面的射线掩码：默认射线层去掉标记层。</summary>
        public static readonly int SurfaceMask = Physics.DefaultRaycastLayers & ~MarkerMask;

        public static GameObject Create(
            Transform parent,
            Vector3 worldPosition,
            Issue issue,
            bool pending)
        {
            var root = new GameObject(pending ? "Pending Issue Marker" : $"Issue Marker - {issue.id}");
            root.transform.SetParent(parent, false);
            root.transform.position = worldPosition;

            var marker = root.AddComponent<IssueMarker>();
            if (pending)
            {
                marker.ConfigurePending();
            }
            else
            {
                marker.ConfigureSaved(issue);
            }

            var color = pending ? new Color(1f, 0.63f, 0.18f) : StatusColor(issue.status);
            var priority = pending ? "medium" : issue.priority;
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.name = "Marker Head";
            sphere.layer = MarkerLayer;
            sphere.transform.SetParent(root.transform, false);
            sphere.transform.localPosition = new Vector3(0f, 0.18f, 0f);
            sphere.transform.localScale = Vector3.one * (pending ? 0.28f : HeadScale(priority));
            sphere.GetComponent<Renderer>().sharedMaterial = CreateMarkerMaterial(color);
            sphere.AddComponent<MarkerPulse>().Configure(PulseAmplitude(priority), PulseSpeed(priority));

            if (priority == "high")
            {
                // 高优先级标记多一层半透明光晕，远看也能和低优先级区分开。
                var halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                halo.name = "Marker Halo";
                halo.layer = MarkerLayer;
                halo.transform.SetParent(root.transform, false);
                halo.transform.localPosition = new Vector3(0f, 0.18f, 0f);
                halo.transform.localScale = Vector3.one * (HeadScale(priority) * 2.1f);
                halo.GetComponent<Renderer>().sharedMaterial = CreateHaloMaterial(color);
                halo.AddComponent<MarkerPulse>().Configure(PulseAmplitude(priority) * 1.8f, PulseSpeed(priority) * 0.7f);
                DestroyCollider(halo);
            }

            var stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stem.name = "Marker Stem";
            stem.layer = MarkerLayer;
            stem.transform.SetParent(root.transform, false);
            stem.transform.localPosition = new Vector3(0f, -0.03f, 0f);
            stem.transform.localScale = new Vector3(0.035f, pending ? 0.18f : StemHeight(priority), 0.035f);
            stem.GetComponent<Renderer>().sharedMaterial = CreateMarkerMaterial(color * 0.55f);
            DestroyCollider(stem);

            return root;
        }

        /// <summary>标记头部半径：优先级越高越大，未填写时按中优先级处理。</summary>
        public static float HeadScale(string priority)
        {
            return priority switch
            {
                "high" => 0.32f,
                "low" => 0.17f,
                _ => 0.24f
            };
        }

        /// <summary>标记立柱高度：高优先级更"高挑"，低优先级更矮。</summary>
        public static float StemHeight(string priority)
        {
            return priority switch
            {
                "high" => 0.26f,
                "low" => 0.12f,
                _ => 0.18f
            };
        }

        private static float PulseAmplitude(string priority)
        {
            return priority switch
            {
                "high" => 0.12f,
                "low" => 0.04f,
                _ => 0.08f
            };
        }

        private static float PulseSpeed(string priority)
        {
            return priority switch
            {
                "high" => 4.4f,
                "low" => 2.2f,
                _ => 3.4f
            };
        }

        private static Material CreateHaloMaterial(Color color)
        {
            var material = CreateMarkerMaterial(new Color(color.r, color.g, color.b, 0.22f));
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;
            return material;
        }

        private static Material CreateMarkerMaterial(Color color)
        {
            var material = new Material(Shader.Find("Standard"))
            {
                color = color
            };
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 1.5f);
            return material;
        }

        private static Color StatusColor(string status)
        {
            return status switch
            {
                "open" => new Color(1f, 0.33f, 0.29f),
                "in_progress" => new Color(1f, 0.68f, 0.21f),
                "resolved" => new Color(0.25f, 0.88f, 0.64f),
                _ => new Color(0.65f, 0.75f, 0.8f)
            };
        }

        private static void DestroyCollider(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }
            Object.Destroy(collider);
        }
    }
}
