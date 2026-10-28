using UnityEngine;

namespace VirtualInspection
{
    public static class InspectionSceneBuilder
    {
        private static Material _floor;
        private static Material _floorLine;
        private static Material _wall;
        private static Material _wallPanel;
        private static Material _cabinet;
        private static Material _panel;
        private static Material _darkMetal;
        private static Material _pipe;
        private static Material _warning;
        private static Material _light;
        private static Material _crate;
        private static Font _font;

        private const string ZoneInspection = "A 区 · 设备巡检";
        private const string ZoneCooling = "B 区 · 冷却系统";

        public static Camera Build(out InspectionCameraController controller)
        {
            _font = RuntimeFonts.Get();
            CreateMaterials();

            var site = new GameObject("Virtual Inspection Site").transform;
            BuildRoom(site);
            BuildEquipment(site);
            BuildLighting(site);
            return BuildCamera(out controller);
        }

        private static void BuildRoom(Transform parent)
        {
            var floor = CreateBox(
                "Inspection Floor",
                parent,
                new Vector3(0f, -0.15f, 0f),
                new Vector3(16f, 0.3f, 14f),
                _floor,
                true);
            AddSurface(floor, "机房地面", ZoneInspection);

            var backWall = CreateBox(
                "Back Wall",
                parent,
                new Vector3(0f, 3.1f, 7.05f),
                new Vector3(16f, 6.2f, 0.25f),
                _wall,
                true);
            AddSurface(backWall, "北侧墙面", ZoneInspection);

            var leftWall = CreateBox(
                "Left Wall",
                parent,
                new Vector3(-8.05f, 3.1f, 0f),
                new Vector3(0.25f, 6.2f, 14f),
                _wall,
                true);
            AddSurface(leftWall, "西侧墙面", ZoneInspection);

            var rightWall = CreateBox(
                "Right Wall",
                parent,
                new Vector3(8.05f, 3.1f, 0f),
                new Vector3(0.25f, 6.2f, 14f),
                _wall,
                true);
            AddSurface(rightWall, "东侧墙面", ZoneInspection);

            for (var x = -7f; x <= 7f; x += 2f)
            {
                CreateBox(
                    "Floor Grid",
                    parent,
                    new Vector3(x, 0.006f, 0f),
                    new Vector3(0.025f, 0.012f, 13.8f),
                    _floorLine,
                    false);
            }
            for (var z = -6f; z <= 6f; z += 2f)
            {
                CreateBox(
                    "Floor Grid",
                    parent,
                    new Vector3(0f, 0.007f, z),
                    new Vector3(15.8f, 0.012f, 0.025f),
                    _floorLine,
                    false);
            }

            for (var index = 0; index < 5; index++)
            {
                var x = -6.4f + index * 3.2f;
                CreateBox(
                    "Wall Panel",
                    parent,
                    new Vector3(x, 3.4f, 6.89f),
                    new Vector3(2.65f, 2.6f, 0.06f),
                    _wallPanel,
                    false);
            }

            for (var index = 0; index < 4; index++)
            {
                var x = -6.6f + index * 4.4f;
                CreateBox(
                    "Ceiling Beam",
                    parent,
                    new Vector3(x, 6f, 0f),
                    new Vector3(0.22f, 0.18f, 13.8f),
                    _darkMetal,
                    false);
            }
        }

        private static void BuildEquipment(Transform parent)
        {
            BuildCabinet(parent, new Vector3(-4.2f, 1.55f, 4.55f), "1 号机柜", "A-01", new Color(0f, 0.82f, 0.65f));
            BuildCabinet(parent, new Vector3(0f, 1.55f, 4.55f), "2 号机柜", "A-02", new Color(0.28f, 0.64f, 1f));
            BuildCabinet(parent, new Vector3(4.2f, 1.55f, 4.55f), "3 号机柜", "A-03", new Color(1f, 0.57f, 0.22f));

            var pipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pipe.name = "Cooling Pipe";
            pipe.transform.SetParent(parent, false);
            pipe.transform.position = new Vector3(0f, 4.55f, 6.55f);
            pipe.transform.localScale = new Vector3(0.17f, 5.9f, 0.17f);
            pipe.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
            SetMaterial(pipe, _pipe);
            AddSurface(pipe, "冷却管线", ZoneCooling);

            for (var index = 0; index < 7; index++)
            {
                var clamp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                clamp.name = "Pipe Clamp";
                clamp.transform.SetParent(parent, false);
                clamp.transform.position = new Vector3(-5.1f + index * 1.7f, 4.55f, 6.55f);
                clamp.transform.localScale = new Vector3(0.23f, 0.10f, 0.23f);
                clamp.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
                SetMaterial(clamp, _warning);
                RemoveCollider(clamp);
            }

            var console = CreateBox(
                "Control Console",
                parent,
                new Vector3(-1.6f, 0.65f, -0.9f),
                new Vector3(3.2f, 1.3f, 1.0f),
                _cabinet,
                true);
            AddSurface(console, "控制台", ZoneInspection);

            var consoleTop = CreateBox(
                "Console Screen",
                console.transform,
                new Vector3(0f, 0.5f, 0.43f),
                new Vector3(2.25f, 0.45f, 0.05f),
                _light,
                false);
            consoleTop.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);

            CreateBox(
                "Safety Crate A",
                parent,
                new Vector3(5.8f, 0.55f, 1.5f),
                new Vector3(1.4f, 1.1f, 1.4f),
                _crate,
                true);
            CreateBox(
                "Safety Crate B",
                parent,
                new Vector3(6.5f, 1.35f, 1.8f),
                new Vector3(1.1f, 0.5f, 1.1f),
                _crate,
                true);

            CreateWorldLabel(
                "A 区 · 设备巡检",
                parent,
                new Vector3(-6.5f, 5.35f, 6.86f),
                Quaternion.identity,
                0.085f);
            CreateWorldLabel(
                "冷却系统",
                parent,
                new Vector3(5.25f, 5.15f, 6.86f),
                Quaternion.identity,
                0.06f);
        }

        private static void BuildCabinet(
            Transform parent,
            Vector3 position,
            string displayName,
            string code,
            Color indicatorColor)
        {
            var cabinet = CreateBox(
                displayName,
                parent,
                position,
                new Vector3(2.15f, 3.1f, 1.25f),
                _cabinet,
                true);
            AddSurface(cabinet, displayName, ZoneInspection);

            var frontPanel = CreateBox(
                "Front Panel",
                cabinet.transform,
                new Vector3(0f, 0.1f, -0.64f),
                new Vector3(1.86f, 2.55f, 0.06f),
                _panel,
                false);

            for (var row = 0; row < 5; row++)
            {
                CreateBox(
                    "Server Slot",
                    frontPanel.transform,
                    new Vector3(0f, 0.85f - row * 0.4f, -0.04f),
                    new Vector3(1.65f, 0.25f, 0.025f),
                    _darkMetal,
                    false);
                CreateBox(
                    "Server LED",
                    frontPanel.transform,
                    new Vector3(0.64f, 0.85f - row * 0.4f, -0.06f),
                    new Vector3(0.07f, 0.07f, 0.02f),
                    _warning,
                    false);
            }

            var indicator = CreateBox(
                "Cabinet Status Light",
                cabinet.transform,
                new Vector3(0.77f, 1.33f, -0.67f),
                new Vector3(0.16f, 0.16f, 0.06f),
                CreateEmissionMaterial(indicatorColor, 2.2f),
                false);
            indicator.AddComponent<MarkerPulse>();

            CreateWorldLabel(
                code,
                parent,
                position + new Vector3(0f, 1.47f, -0.71f),
                Quaternion.identity,
                0.055f);
        }

        private static void BuildLighting(Transform parent)
        {
            RenderSettings.ambientLight = new Color(0.15f, 0.19f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.025f, 0.04f, 0.05f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 11f;
            RenderSettings.fogEndDistance = 28f;

            var sunObject = new GameObject("Directional Light");
            sunObject.transform.SetParent(parent, false);
            sunObject.transform.rotation = Quaternion.Euler(48f, -34f, 0f);
            var sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.78f, 0.88f, 1f);
            sun.intensity = 0.82f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;

            for (var index = 0; index < 3; index++)
            {
                var fixture = CreateBox(
                    "Ceiling Fixture",
                    parent,
                    new Vector3(-4.5f + index * 4.5f, 5.72f, 1.2f),
                    new Vector3(3.2f, 0.08f, 0.18f),
                    _light,
                    false);

                var lightObject = new GameObject("Ceiling Light");
                lightObject.transform.SetParent(fixture.transform, false);
                lightObject.transform.position += Vector3.down * 0.3f;
                var point = lightObject.AddComponent<Light>();
                point.type = LightType.Point;
                point.color = new Color(0.72f, 0.92f, 1f);
                point.intensity = 1.5f;
                point.range = 8f;
                point.shadows = LightShadows.None;
            }
        }

        private static Camera BuildCamera(out InspectionCameraController controller)
        {
            var cameraObject = new GameObject("Inspection Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 1.7f, -5.7f);
            cameraObject.transform.rotation = Quaternion.LookRotation(
                new Vector3(0f, 1.5f, 4.4f) - cameraObject.transform.position);

            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.018f, 0.03f, 0.038f);
            camera.fieldOfView = 66f;
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 80f;
            controller = cameraObject.AddComponent<InspectionCameraController>();

            var listener = cameraObject.AddComponent<AudioListener>();
            listener.enabled = true;
            return camera;
        }

        private static void CreateMaterials()
        {
            var standard = Shader.Find("Standard");
            _floor = CreateMaterial(standard, new Color(0.12f, 0.15f, 0.17f), 0.15f, 0.4f);
            _floorLine = CreateEmissionMaterial(new Color(0.08f, 0.16f, 0.19f), 0.35f);
            _wall = CreateMaterial(standard, new Color(0.075f, 0.11f, 0.13f), 0.1f, 0.28f);
            _wallPanel = CreateMaterial(standard, new Color(0.095f, 0.14f, 0.17f), 0.25f, 0.35f);
            _cabinet = CreateMaterial(standard, new Color(0.055f, 0.09f, 0.11f), 0.6f, 0.5f);
            _panel = CreateMaterial(standard, new Color(0.035f, 0.065f, 0.08f), 0.48f, 0.42f);
            _darkMetal = CreateMaterial(standard, new Color(0.025f, 0.04f, 0.045f), 0.75f, 0.5f);
            _pipe = CreateMaterial(standard, new Color(0.76f, 0.43f, 0.1f), 0.55f, 0.38f);
            _warning = CreateEmissionMaterial(new Color(1f, 0.59f, 0.16f), 0.8f);
            _light = CreateEmissionMaterial(new Color(0.55f, 0.85f, 1f), 2.1f);
            _crate = CreateMaterial(standard, new Color(0.19f, 0.18f, 0.13f), 0.1f, 0.25f);
        }

        private static Material CreateEmissionMaterial(Color color, float strength)
        {
            var material = CreateMaterial(Shader.Find("Standard"), color, 0f, 0.45f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * strength);
            return material;
        }

        private static Material CreateMaterial(
            Shader shader,
            Color color,
            float metallic,
            float smoothness)
        {
            var material = new Material(shader)
            {
                color = color
            };
            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", metallic);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", smoothness);
            }
            return material;
        }

        private static GameObject CreateBox(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            bool keepCollider)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = position;
            var parentScale = parent == null ? Vector3.one : parent.lossyScale;
            box.transform.localScale = new Vector3(
                scale.x / Mathf.Max(Mathf.Abs(parentScale.x), 0.0001f),
                scale.y / Mathf.Max(Mathf.Abs(parentScale.y), 0.0001f),
                scale.z / Mathf.Max(Mathf.Abs(parentScale.z), 0.0001f));
            SetMaterial(box, material);
            if (!keepCollider)
            {
                RemoveCollider(box);
            }
            return box;
        }

        private static void CreateWorldLabel(
            string value,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            float characterSize)
        {
            var labelObject = new GameObject($"Label - {value}");
            labelObject.transform.SetParent(parent, false);
            labelObject.transform.localPosition = localPosition;
            labelObject.transform.localRotation = localRotation;

            var label = labelObject.AddComponent<TextMesh>();
            label.text = value;
            label.font = _font;
            label.fontSize = 64;
            label.characterSize = characterSize;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = new Color(0.8f, 0.92f, 0.96f, 0.9f);
            labelObject.GetComponent<MeshRenderer>().sharedMaterial = _font.material;
        }

        private static void AddSurface(GameObject target, string displayName, string area)
        {
            target.AddComponent<InspectableSurface>().Configure(displayName, area);
        }

        private static void SetMaterial(GameObject target, Material material)
        {
            target.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void RemoveCollider(GameObject target)
        {
            var collider = target.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }
        }
    }
}
