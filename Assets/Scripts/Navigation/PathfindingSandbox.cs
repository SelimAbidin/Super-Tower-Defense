using UnityEngine;

namespace SuperTowerDefense.Navigation
{
    public sealed class PathfindingSandbox : MonoBehaviour
    {
        [SerializeField, Range(0, 3)] private int scenario = 2;
        [SerializeField] private DestinationPolicy policy = DestinationPolicy.Nearest;
        [SerializeField, Min(0.05f)] private float secondsPerTick = 0.3f;
        [SerializeField] private Material[] materials;
        [SerializeField] private Material routeMaterial;
        private PathfindingSimulation simulation;
        private Transform board;
        private Transform[] markers;
        private LineRenderer[] routes;
        private bool running;
        private double accumulator;
        public PathfindingSimulation Simulation => simulation;

        public void Configure(Material[] palette, Material lineMaterial)
        {
            materials = palette;
            routeMaterial = lineMaterial;
        }

        private void Awake() => ResetSandbox();

        // Wall-clock time controls playback speed only. AdvanceTick owns all simulation changes.
        private void Update()
        {
            if (!running) return;
            accumulator += Time.unscaledDeltaTime;
            int budget = 8;
            double interval = Mathf.Max(0.05f, secondsPerTick);
            while (accumulator >= interval && budget-- > 0)
            {
                AdvanceTick();
                accumulator -= interval;
            }
        }

        public void ResetSandbox()
        {
            running = false;
            accumulator = 0;
            simulation = new PathfindingSimulation(scenario, policy);
            var old = transform.Find("Board");
            if (old != null)
            {
                old.name = "Retired Board";
                old.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject);
            }
            board = new GameObject("Board").transform;
            board.SetParent(transform, false);
            var grid = simulation.Grid;
            for (int node = 0; node < grid.NodeCount; node++)
            {
                bool open = grid.IsWalkable(node);
                int mat = open ? (grid.GetX(node) + grid.GetY(node)) % 2 : 2;
                Primitive("Tile " + grid.GetX(node) + "," + grid.GetY(node), PrimitiveType.Cube,
                    World(node, open ? -0.08f : 0.22f), new Vector3(0.94f, open ? 0.12f : 0.65f, 0.94f), mat);
                if (!open) continue;
                simulation.Fields.TryRoute(node, simulation.AllowedMask(0), out _, out _, out int distance);
                Label(distance < 0 ? "X" : distance.ToString(), World(node, 0.025f), 0.11f, new Color(0.72f, 0.79f, 0.83f));
            }
            for (int i = 0; i < simulation.ExitCount; i++)
            {
                Primitive("Exit " + (i + 1), PrimitiveType.Cube, World(simulation.Exit(i), 0.35f), new Vector3(0.7f, 0.75f, 0.7f), 4 + i);
                Label("E" + (i + 1), World(simulation.Exit(i), 1.05f), 0.22f, Color.white);
            }
            markers = new Transform[simulation.AgentCount];
            routes = new LineRenderer[simulation.AgentCount];
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                Primitive("Spawn " + (i + 1), PrimitiveType.Cube, World(simulation.Spawn(i), 0.03f), new Vector3(0.85f, 0.13f, 0.85f), 3);
                Label("S" + (i + 1), World(simulation.Spawn(i), 1.1f), 0.22f, Color.cyan);
                markers[i] = Primitive("Agent " + (i + 1), PrimitiveType.Sphere, World(simulation.Position(i), 0.45f), Vector3.one * 0.5f, 9).transform;
                var line = new GameObject("Route " + (i + 1), typeof(LineRenderer));
                line.transform.SetParent(board, false);
                routes[i] = line.GetComponent<LineRenderer>();
                routes[i].sharedMaterial = routeMaterial;
                routes[i].widthMultiplier = 0.07f;
                routes[i].useWorldSpace = true;
            }
            Label("PATHFINDING SANDBOX", new Vector3(0, 0.4f, 9.5f), 0.32f, Color.white);
            Label("N / E / S / W   |   cyan: spawns   |   colored: exits   |   lines: routes", new Vector3(0, 0.25f, 7.7f), 0.14f, new Color(0.65f, 0.8f, 0.85f));
            RefreshMarkers();
        }

        public void AdvanceTick()
        {
            simulation.AdvanceTick();
            RefreshMarkers();
        }

        private void RefreshMarkers()
        {
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                markers[i].position = World(simulation.Position(i), 0.45f);
                bool found = simulation.Route(i, out _, out int exit, out int distance);
                var line = routes[i];
                line.positionCount = found ? distance + 1 : 0;
                if (!found) continue;
                Color color = materials[4 + exit].color;
                line.startColor = line.endColor = color;
                int node = simulation.Position(i);
                for (int step = 0; step <= distance; step++)
                {
                    line.SetPosition(step, World(node, 0.17f + i * 0.025f));
                    simulation.Fields.TryRoute(node, simulation.AllowedMask(i), out node, out _, out _);
                }
            }
        }

        public Vector3 World(int node, float height) =>
            new Vector3(simulation.Grid.GetX(node) - 8, height, simulation.Grid.GetY(node) - 5);

        private GameObject Primitive(string label, PrimitiveType type, Vector3 position, Vector3 scale, int material)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = label;
            item.transform.SetParent(board, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = materials[material];
            // The sandbox only reads grid data, never physics, for navigation.
            var collider = item.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            return item;
        }

        private void Label(string text, Vector3 position, float size, Color color)
        {
            var item = new GameObject("Label " + text, typeof(TextMesh));
            item.transform.SetParent(board, false);
            item.transform.localPosition = position;
            item.transform.rotation = Camera.main.transform.rotation;
            var mesh = item.GetComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.fontSize = 48;
            mesh.characterSize = size;
            mesh.color = color;
            mesh.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            item.GetComponent<MeshRenderer>().sharedMaterial = mesh.font.material;
        }

        private void OnGUI()
        {
            if (simulation == null) return;
            GUILayout.BeginArea(new Rect(12, 12, Mathf.Min(Screen.width - 24, 1050), 165), GUI.skin.box);
            GUILayout.Label("PATHFINDING  |  " + PathfindingSimulation.ScenarioNames[scenario] + "  |  Tick " + simulation.Tick + "  |  " + simulation.Fields.FieldCount + " shared field(s)");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(running ? "Pause" : "Run")) { running = !running; accumulator = 0; }
            if (GUILayout.Button("Step 1 tick")) { running = false; accumulator = 0; AdvanceTick(); }
            if (GUILayout.Button("Reset")) ResetSandbox();
            if (GUILayout.Button("Next scenario")) { scenario = (scenario + 1) % PathfindingSimulation.ScenarioCount; ResetSandbox(); }
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            foreach (DestinationPolicy option in System.Enum.GetValues(typeof(DestinationPolicy)))
                if (GUILayout.Button((policy == option ? "[selected] " : "") + option)) { policy = option; ResetSandbox(); }
            GUILayout.EndHorizontal();
            GUILayout.Label("One cardinal tile per tick. Numbers show S1's allowed-exit distances; X means unreachable.");
            string status = "";
            for (int i = 0; i < simulation.AgentCount; i++)
            {
                bool found = simulation.Route(i, out _, out int exit, out int distance);
                status += "S" + (i + 1) + ": " + (!found ? "no route" : distance == 0 ? "arrived E" + (exit + 1) : "E" + (exit + 1) + " (" + distance + ")") + "   ";
            }
            GUILayout.Label(status);
            GUILayout.EndArea();
        }
    }
}
