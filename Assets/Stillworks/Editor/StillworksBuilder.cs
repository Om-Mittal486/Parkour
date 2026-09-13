using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Stillworks.Editor
{
    /// <summary>Deterministic architectural kit; baked meshes and colliders, never runtime generation.</summary>
    public static class StillworksBuilder
    {
        private const string Output = "Assets/Stillworks/Generated";
        private const string ScenePath = "Assets/Scenes/Stillworks.unity";
        private static readonly string[] Districts = { "Intake Quarter", "Heat Exchange", "Transit Stack", "Unfinished Works", "The Ministry", "Relay Needles", "The Crown" };
        private static readonly float[] Scale = { 1.13f, 1.08f, 1.22f, 0.98f, 1.08f, 0.87f, 0.78f, 0.78f };
        private static readonly float[] Width = { 18, 16, 25, 9, 19, 7, 12 };
        private static readonly Vector3[] Plan = {
            new Vector3(-120, 0, -92), new Vector3(42, 0, -145),
            new Vector3(150, 0, -58), new Vector3(110, 0, 118),
            new Vector3(-32, 0, 156), new Vector3(-150, 0, 48)
        };
        private static readonly List<RouteSpan> Spans = new List<RouteSpan>();
        private static readonly List<Chunk> Chunks = new List<Chunk>();
        private static Material[] palette;
        private static Transform root;
        private static Chunk chunk;
        private static float metres;
        private static int colliderCount;
        private static readonly int[] Faces = { 0,3,2,1, 4,5,6,7, 0,4,7,3, 1,2,6,5, 0,1,5,4, 3,7,6,2 };

        [MenuItem("Stillworks/Build playable map")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Output + "/Meshes");
            Directory.CreateDirectory(Output + "/Materials");
            Spans.Clear(); Chunks.Clear(); metres = 0; colliderCount = 0;
            // Save-as preserves every original scene object on disk, including the input references.
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity", OpenSceneMode.Single);
            PlayerMovement player = UnityEngine.Object.FindFirstObjectByType<PlayerMovement>();
            if (player == null) throw new InvalidOperationException("The source scene must contain PlayerMovement.");
            foreach (GameObject go in scene.GetRootGameObjects())
                if (go != player.gameObject) UnityEngine.Object.DestroyImmediate(go);
            root = new GameObject("THE STILLWORKS • abandoned vertical utility city").transform;
            CreatePalette();
            Foundation();
            for (int district = 0; district < 7; district++)
            {
                Transform area = new GameObject($"{district + 1:00} • {Districts[district]} • {district * 84}–{(district + 1) * 84}m").transform;
                area.SetParent(root);
                for (int storey = 0; storey < 2; storey++)
                {
                    int floor = district * 2 + storey;
                    for (int wing = 0; wing < 6; wing++)
                    {
                        chunk = new Chunk($"D{district + 1}_L{storey + 1}_W{wing + 1}", area);
                        Chunks.Add(chunk);
                        Vector3 a = Node(floor, wing), b = Node(floor, wing + 1);
                        Wing(district, storey, wing, a, b);
                        Vector3 radial = new Vector3(a.x, 0, a.z).normalized;
                        Vector3 upperPier = a - radial * 28 - Vector3.up * 2;
                        Vector3 lowerNode = floor > 0 ? Node(floor - 1, wing) : new Vector3(a.x, -2, a.z);
                        Vector3 lowerRadial = new Vector3(lowerNode.x, 0, lowerNode.z).normalized;
                        Vector3 lowerPier = lowerNode - lowerRadial * 28 - Vector3.up * 2;
                        Beam("Continuous raking megaframe pier", lowerPier, upperPier, 5, 5, 0);
                        Beam("Megaframe transfer beam", upperPier, a - Vector3.up * 2, 3, 2, 0);
                    }
                }
                chunk = new Chunk($"D{district + 1}_service_spine", area); Chunks.Add(chunk);
                MaintenanceSpine(district);
                DistrictLandmark(district);
            }
            chunk = new Chunk("Crown_observation", root); Chunks.Add(chunk);
            Vector3 end = Node(14, 0);
            Vector3 summit = new Vector3(0, 588, 0);
            Deck("Crown approach", end, summit, 12, 1.7f, 0);
            AddSpan("Final communications bridge", Passage.Run, end, summit, 12);
            Box("Observation roof", summit - Vector3.up * 2, new Vector3(92, 4, 78), 1);
            // A hollow, offset communications arch frames the vista instead of enclosing it.
            Box("Crown west fin", new Vector3(-36, 603, 18), new Vector3(8, 30, 18), 0);
            Box("Crown east fin", new Vector3(36, 603, 18), new Vector3(8, 30, 18), 0);
            Box("Crown lintel", new Vector3(0, 620, 18), new Vector3(86, 6, 18), 1);
            Cylinder("Transmission mast", new Vector3(30, 648, 18), 0.8f, 54, 3);
            for (int i = 0; i < 5; i++) Box("Antenna array", new Vector3(30, 635 + i * 7, 18), new Vector3(14 - i * 1.5f, 0.28f, 0.5f), 3);
            Label("THE CROWN\nEND OF TRANSMISSION", new Vector3(0, 590.5f, 24), Quaternion.identity, 1.1f);
            Box("Summit survey table", new Vector3(0, 588.5f, 5), new Vector3(4, 1, 2), 3);
            PerimeterRoofRails(summit, 90, 76);
            foreach (Chunk c in Chunks) c.Bake();

            StillworksRoute route = AssetDatabase.LoadAssetAtPath<StillworksRoute>(Output + "/StillworksRoute.asset");
            if (route == null) { route = ScriptableObject.CreateInstance<StillworksRoute>(); AssetDatabase.CreateAsset(route, Output + "/StillworksRoute.asset"); }
            route.spawn = Node(0, 0) + Vector3.up * 0.12f;
            route.summit = summit;
            route.mainRouteMetres = metres;
            route.districts = Districts;
            route.spans = Spans.ToArray();
            EditorUtility.SetDirty(route);
            ConfigurePlayer(player, route);
            Atmosphere();
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Physics.SyncTransforms();
            Validate();
            Debug.Log($"STILLWORKS BUILT: {metres:0}m main route / 588m ascent / {Chunks.Count} chunks / {colliderCount} colliders.");
        }

        private static Vector3 Node(int floor, int vertex)
        {
            if (vertex == 6) return Node(floor + 1, 0);
            int d = Mathf.Min(floor / 2, 7);
            return Plan[vertex] * Scale[d] + Vector3.up * (floor * 42 + vertex * 7);
        }

        private static void CreatePalette()
        {
            Shader shader = Shader.Find("Stillworks/Board concrete");
            if (shader == null) throw new InvalidOperationException("Stillworks concrete shader not imported.");
            Color[] colors = { new Color(.47f,.48f,.45f), new Color(.63f,.62f,.56f), new Color(.22f,.25f,.25f), new Color(.13f,.17f,.18f), new Color(.43f,.23f,.14f), new Color(.65f,.51f,.25f), new Color(.74f,.73f,.64f), new Color(.17f,.23f,.16f) };
            string[] names = { "Boardformed concrete", "Chalk concrete", "Asphalt", "Oxidised steel", "Iron oxide", "Faded ochre", "Old enamel", "Moss" };
            palette = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                string path = Output + "/Materials/" + names[i] + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.shader = shader;
                material.SetColor("_BaseColor", colors[i]);
                material.SetFloat("_Weather", i == 3 || i == 4 ? .85f : .45f);
                material.SetFloat("_Board", i < 2 ? 1 : 0);
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                palette[i] = material;
            }
        }

        private static void Foundation()
        {
            chunk = new Chunk("00_raft_and_city", root); Chunks.Add(chunk);
            Box("Abandoned concrete floodplain", new Vector3(0, -12, 0), new Vector3(4200, 4, 4200), 2);
            Box("City foundation raft", new Vector3(0, -5, 0), new Vector3(650, 10, 650), 2);
            // The inhabited circulation wraps around a hollow structural core.
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Box("Continuous core buttress", new Vector3(x * 29, 291, z * 25), new Vector3(16, 582, 18), 0);
            for (int level = 0; level < 14; level++)
            {
                float y = level * 42 + 12;
                Box("Core north spandrel", new Vector3(0, y, 31), new Vector3(70, 10, 8), 1);
                Box("Core south spandrel", new Vector3(0, y, -31), new Vector3(70, 10, 8), 0);
                Box("Core east spandrel", new Vector3(33, y, 0), new Vector3(8, 10, 60), 0);
                Box("Core west spandrel", new Vector3(-33, y, 0), new Vector3(8, 10, 60), 1);
                for (int f = -1; f <= 1; f += 2)
                    Box("Recessed core slit", new Vector3(f * 34, y + 17, 0), new Vector3(3, 24, 8), 3);
            }
            Box("Atrium floor", new Vector3(0, 1, 0), new Vector3(46, 2, 40), 1);
            // Grounded, simplified city blocks create an urban base and legible scale.
            var random = new System.Random(7331);
            for (int i = 0; i < 42; i++)
            {
                float angle = i * Mathf.PI * 2 / 42;
                float radius = 260 + (float)random.NextDouble() * 42;
                float height = 18 + (float)random.NextDouble() * 69;
                Vector3 p = new Vector3(Mathf.Cos(angle) * radius, height / 2, Mathf.Sin(angle) * radius);
                float w = 17 + (float)random.NextDouble() * 16;
                Box("Abandoned perimeter block", p, new Vector3(w, height, 22), i % 3 == 0 ? 1 : 0);
                Box("Inset roof machinery", p + Vector3.up * (height / 2 + 1.5f), new Vector3(w * .6f, 3, 10), 3);
                for (int j = 1; j < height / 7; j++)
                    Box("Recessed facade ribbon", new Vector3(p.x, j * 7, p.z - 11.04f), new Vector3(w - 4, 1.3f, .08f), 3, false);
            }
            for (int i = 0; i < 12; i++)
            {
                Vector3 p = new Vector3(-180 + i * 31, .04f, -207);
                Box("Lost carriageway centre line", p, new Vector3(12, .035f, .23f), 6, false);
                Car(p + new Vector3(7, 0, -8), Quaternion.Euler(0, i * 17, 0), i % 2 == 0 ? 3 : 4);
            }
        }

        private static void Wing(int d, int storey, int wing, Vector3 a, Vector3 b)
        {
            float width = Width[d] + ((wing % 3) - 1) * 2;
            Vector3 flatEnd = Vector3.Lerp(a, b, .56f); flatEnd.y = a.y;
            Vector3 rampEnd = Vector3.Lerp(a, b, .84f); rampEnd.y = b.y;
            Vector3 dir = (b - a); dir.y = 0; dir.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 inside = Vector3.Dot(side, -a) > 0 ? side : -side;
            Quaternion rotation = Quaternion.LookRotation(dir);
            Box("Structural junction court", a - Vector3.up * .8f, new Vector3(width + 13, 1.6f, width + 13), d == 2 ? 2 : 0);
            // Separate foundation legs stay below their own decks; they never intrude on a lower route.
            for (int k = 0; k < 3; k++)
            {
                Vector3 p = Vector3.Lerp(a, flatEnd, .12f + k * .35f);
                Box("Massive inhabited plinth", p + inside * (width * .5f + 5) - Vector3.up * 15,
                    new Vector3(9, 30, 22), 0, true, rotation);
                Beam("Cantilever knee", p + inside * (width * .5f + 5) - Vector3.up * 14,
                    p - inside * (width * .42f) - Vector3.up * 1.3f, 1.2f, 1.5f, d == 3 ? 4 : 0);
            }
            bool broken = d > 0 && (wing + storey * 2) % 3 == 1;
            if (broken)
            {
                Vector3 mid = Vector3.Lerp(a, flatEnd, .59f);
                float gap = d >= 5 ? 4.2f : 3.2f;
                Vector3 lip = mid - dir * gap / 2, land = mid + dir * gap / 2;
                Deck("Fractured gallery / approach", a, lip, width, 1, d == 2 ? 2 : 0);
                Deck("Fractured gallery / landing", land, flatEnd, width, 1, d == 2 ? 2 : 0);
                AddSpan("Gallery approach", Passage.Run, a, lip, width);
                AddSpan("Broken expansion joint", Passage.Jump, lip, land, width);
                AddSpan("Gallery landing", Passage.Run, land, flatEnd, width);
                Box("Exposed fracture reinforcement", mid - Vector3.up * .4f + side * (width / 2 - .6f), new Vector3(.14f, .14f, gap + .8f), 4, false, rotation);
                Recovery(mid, dir, side, width, gap);
            }
            else
            {
                Deck("Occupied floor slab", a, flatEnd, width, 1.1f, d == 2 ? 2 : 0);
                AddSpan("Open circulation floor", Passage.Run, a, flatEnd, width);
            }
            if ((d == 0 || d == 3 || d == 4 || d == 6) && wing % 2 == 0)
                Stair("Processional staircase", flatEnd, rampEnd, width * .7f, 1);
            else Deck("Graded service ramp", flatEnd, rampEnd, width * .78f, 1, d == 2 ? 2 : 0);
            AddSpan("Ascending circulation", Passage.Ramp, flatEnd, rampEnd, width * .7f);
            Deck("Upper transfer terrace", rampEnd, b, width, 1.2f, 1);
            AddSpan("Upper terrace sprint", Passage.Run, rampEnd, b, width);

            // Broad side rooms, walls and roofs turn the circulation into occupied architecture.
            Vector3 galleryA = Vector3.Lerp(a, flatEnd, .10f);
            Vector3 galleryB = Vector3.Lerp(a, flatEnd, .89f);
            float wallHeight = d == 4 ? 23 : d == 5 ? 19 : d == 0 ? 12 : 8;
            Vector3 wallA = galleryA + inside * (width / 2 + 1.3f);
            Vector3 wallB = galleryB + inside * (width / 2 + 1.3f);
            Deck("Continuous wall-run facade", wallA + Vector3.up * wallHeight, wallB + Vector3.up * wallHeight, 2.4f, wallHeight, d == 4 ? 1 : 0);
            if (d == 2 || d == 4 || (d == 1 && wing % 2 == 0))
            {
                float clearance = d == 4 ? 16 : 5.5f;
                Deck("Sheltered industrial ceiling", galleryA + Vector3.up * clearance, galleryB + Vector3.up * clearance, width + 10, 1, 0);
                for (float t = .17f; t < .86f; t += .15f)
                {
                    Vector3 p = Vector3.Lerp(a, flatEnd, t) - inside * (width / 2 + 4);
                    Box("Column arcade", p + Vector3.up * ((clearance - 1) / 2), new Vector3(1.2f, clearance - 1, 1.2f), 1);
                }
            }
            // Perimeter rails are deliberately incomplete; lower architecture remains reachable.
            Rail(Vector3.Lerp(a, flatEnd, .17f) - inside * width / 2, Vector3.Lerp(a, flatEnd, .25f) - inside * width / 2, d >= 3);
            Rail(rampEnd - inside * width / 2, b - inside * width / 2, d >= 3);
            if (wing == 0 || wing == 3) Exploration(galleryA, galleryB, dir, inside, width, d);
            if (wing == 2 || wing == 5) SlideBypass(a, flatEnd, side, width, d);
            DressWing(d, storey, wing, a, flatEnd, dir, inside, width);
            if (wing == 0)
            {
                Label($"{d + 1:00}  /  {Districts[d].ToUpperInvariant()}\n{a.y:000} M     SERVICE ACCESS", a + inside * (width / 2 + .06f) + dir * 18 + Vector3.up * 4,
                    Quaternion.LookRotation(inside), .7f);
            }
        }

        private static void Recovery(Vector3 mid, Vector3 dir, Vector3 side, float width, float gap)
        {
            Vector3 catchA = mid - dir * 13 - Vector3.up * 5;
            Vector3 catchB = mid + dir * 14 - Vector3.up * 5;
            Deck("Lower maintenance catch floor", catchA, catchB, width + 12, .8f, 2);
            Vector3 rampA = mid - dir * 6 - Vector3.up * 5 + side * (width / 2 + 3);
            Vector3 rampB = mid + dir * 17 + side * (width / 2 + 3);
            Deck("Catch floor return ramp", rampA, rampB, 4, .55f, 4);
            Vector3 bend = rampB + dir * 2;
            Deck("Level recovery elbow", rampB, bend, 4, .55f, 4);
            Vector3 rejoin = bend - side * (width / 2 + 3);
            Deck("Recovery rejoins gallery", bend, rejoin, 4, .55f, 4);
            AddSpan("Lower roof recovery ramp", Passage.Recovery, rampA, rampB, 4);
            AddSpan("Recovery elbow", Passage.Recovery, rampB, bend, 4);
            AddSpan("Recovery rejoin", Passage.Recovery, bend, rejoin, 4);
            // Supports expose a believable bridge joint, not an isolated jump platform.
            Beam("Expansion joint support", catchA - Vector3.up * .8f, mid - dir * (gap + 2) - Vector3.up * 1.2f, 1, 1, 3);
        }

        private static void Exploration(Vector3 a, Vector3 b, Vector3 dir, Vector3 inside, float width, int d)
        {
            // The outside gallery avoids the continuous inner facade and rejoins at the same height.
            Vector3 offset = -inside * (width / 2 + 14);
            Vector3 p = a + offset, q = b + offset;
            Deck("Side court entrance", a, p, 7, .9f, 1);
            Deck("Unmarked service courtyard", p, q, d == 1 ? 23 : 15, 1.4f, 0);
            Deck("Side court return", q, b, 7, .9f, 1);
            AddSpan("Exploration entrance", Passage.Exploration, a, p, 7);
            AddSpan("Exploration gallery", Passage.Exploration, p, q, 15);
            AddSpan("Exploration rejoin", Passage.Exploration, q, b, 7);
            Vector3 centre = (p + q) / 2 - inside * (d == 1 ? 6.5f : 4);
            if (d == 1) Tank(centre, 4, 10);
            else
            {
                Box("Silent ventilation house", centre + Vector3.up * 3, new Vector3(5, 6, 12), d == 3 ? 4 : 0, true, Quaternion.LookRotation(dir));
                for (int i = 0; i < 5; i++)
                    Box("Ventilation louvre", centre + inside * 2.55f + dir * (-4 + i * 2) + Vector3.up * 3, new Vector3(.1f, 3.5f, .3f), 3, false, Quaternion.LookRotation(dir));
            }
            Rail(p - inside * (d == 1 ? 11 : 7), q - inside * (d == 1 ? 11 : 7), true);
            Beam("Gallery tension strut", p - Vector3.up * 1, a - Vector3.up * 15, 1.4f, 1.4f, 0);
            Beam("Gallery tension strut", q - Vector3.up * 1, b - Vector3.up * 15, 1.4f, 1.4f, 0);
        }

        private static void SlideBypass(Vector3 a, Vector3 b, Vector3 side, float width, int d)
        {
            Vector3 dir = (b - a).normalized;
            // Optional loading-bay cut-through: long approach, only 2.4m under the lintel.
            Vector3 p = Vector3.Lerp(a, b, .30f) + side * (width / 2 - .9f);
            Vector3 q = p + dir * 2.4f;
            Deck("Loading bay apron", p - dir * 8, q + dir * 6, 4.8f, 1.1f, d == 2 ? 2 : 0);
            Deck("Low service lintel • 1.25m clear", p + Vector3.up * 2.25f, q + Vector3.up * 2.25f, 3.5f, 1, 0);
            foreach (float sign in new[] { -1f, 1f })
                Box("Service opening jamb", (p + q) / 2 + side * sign * 2.05f + Vector3.up * 1.125f, new Vector3(.5f, 2.25f, 3), 0, true, Quaternion.LookRotation(dir));
            AddSpan("Loading lintel slide", Passage.Slide, p - dir * 2, q + dir * 2, 2.8f);
            Box("Faded clearance stripe", p + Vector3.up * 1.85f - dir * .06f, new Vector3(3.4f, .16f, .08f), 5, false, Quaternion.LookRotation(dir));
        }

        private static void DressWing(int d, int storey, int wing, Vector3 a, Vector3 b, Vector3 dir, Vector3 inside, float width)
        {
            Quaternion rot = Quaternion.LookRotation(dir);
            for (int i = 0; i < 5; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, .20f + i * .14f);
                Vector3 edge = p + inside * (width / 2 - 1.4f);
                if (d == 0)
                {
                    Box("Sealed service door", edge + inside * 1.3f + Vector3.up * 2, new Vector3(.08f, 4, 3), 3, false, rot);
                    if (i % 2 == 0) Car(p - inside * (width / 2 - 2), rot, i == 0 ? 4 : 3);
                    Lamp(p - inside * (width / 2 + .7f));
                }
                else if (d == 1)
                {
                    CylinderBetween("Heat transfer main", edge + Vector3.up * 3, edge + dir * 11 + Vector3.up * 3, .65f, 4);
                    Box("Pipe saddle", edge + Vector3.up * 1.2f, new Vector3(1.4f, 2.4f, 1.4f), 0);
                    if (i == 1 || i == 4) Tank(edge + inside * 11, 4, 14);
                }
                else if (d == 2)
                {
                    Box("Parking bay stripe", p - inside * 7 + Vector3.up * .02f, new Vector3(6, .03f, .13f), 6, false, rot);
                    if (i == 1 || i == 4) Car(p - inside * 8, Quaternion.LookRotation(inside), 3);
                    Box("Low concrete divider / future vault", edge + Vector3.up * .45f, new Vector3(.5f, .9f, 5), 0, true, rot);
                }
                else if (d == 3)
                {
                    Box("Unfinished floor column", edge + Vector3.up * 8, new Vector3(.8f, 16, .8f), 0);
                    Beam("Exposed steel roof girder", edge + Vector3.up * 15, edge - inside * (width + 1) + Vector3.up * 15, .45f, .7f, 4);
                    Box("Rebar bundle", edge + Vector3.up * 17, new Vector3(.2f, 4, .2f), 4, false);
                    if (i == 3) Box("Abandoned hoist motor", edge + Vector3.up * .8f, new Vector3(1.6f, 1.6f, 2.8f), 3);
                }
                else if (d == 4)
                {
                    Box("Ministry facade fin", edge + inside * 2.5f + Vector3.up * 12, new Vector3(3, 24, 1.6f), 1, true, rot);
                    Box("Old enamel information panel", edge + inside * 1.4f + Vector3.up * 4, new Vector3(.1f, 2, 4), 3, false, rot);
                }
                else if (d == 5)
                {
                    Box("Relay cable riser", edge + inside * 2 + Vector3.up * 18, new Vector3(1, 36, 1.3f), 3);
                    Beam("Relay riser cross arm", edge + inside * 2 + Vector3.up * 25, edge - inside * 5 + Vector3.up * 25, .25f, .4f, 4);
                }
                else
                {
                    Box("Crown concrete blade", edge + inside * 2 + Vector3.up * 12, new Vector3(2, 24, 3), 1, true, rot);
                    if (i == 3) Label("LISTENING STATION\nNO PERSONNEL REMAIN", edge - inside * .2f + Vector3.up * 3, Quaternion.LookRotation(inside), .45f);
                }
                // Collison-free secondary dressing is combined separately and distance-culled.
                Box("Water runoff scar", p + inside * (width / 2 + .06f) + Vector3.up * 1.6f, new Vector3(.08f, 3.2f, .7f), 2, false, rot);
                if (i == 2 && d < 3)
                    Box("Windblown moss", p - inside * (width / 2 - .4f) + Vector3.up * .025f, new Vector3(.65f, .035f, 5), 7, false, rot);
            }
        }

        private static void MaintenanceSpine(int d)
        {
            Vector3 entry = Node(d * 2, 0), exit = Node(d * 2 + 1, 0);
            Vector3 outward = new Vector3(entry.x, 0, entry.z).normalized;
            Vector3 tangent = Vector3.Cross(Vector3.up, outward);
            Vector3 centre = entry + outward * 40;
            Vector3 start = centre - tangent * 13 - outward * 8;
            Deck("Unmarked maintenance access", entry, start, 2.2f, .5f, 4);
            AddSpan("Maintenance access", Passage.Shortcut, entry, start, 2.2f);
            Vector3 current = start;
            for (int flight = 0; flight < 6; flight++)
            {
                Vector3 next = centre + tangent * (flight % 2 == 0 ? 13 : -13) + outward * (flight % 2 == 0 ? -8 : 8);
                next.y = entry.y + (flight + 1) * 7;
                Stair("Narrow exposed fire stair", current, next, 1.5f, 4);
                AddSpan("Exposed service stair", Passage.Shortcut, current, next, 1.5f);
                if (flight < 5)
                {
                    Vector3 across = next + outward * (flight % 2 == 0 ? 16 : -16);
                    Vector3 direction = (across - next).normalized;
                    Vector3 lip = Vector3.Lerp(next, across, .5f) - direction * 1.9f;
                    Vector3 landing = lip + direction * 3.8f;
                    Deck("Missing fire-escape grating / takeoff", next, lip, 2.3f, .3f, 3);
                    Deck("Missing fire-escape grating / landing", landing, across, 2.3f, .3f, 3);
                    AddSpan("Service landing takeoff", Passage.Shortcut, next, lip, 2.3f);
                    AddSpan("Shortcut gap (3.8m)", Passage.Jump, lip, landing, 2.3f, false);
                    AddSpan("Service landing exit", Passage.Shortcut, landing, across, 2.3f);
                    current = across;
                }
                else current = next;
            }
            Deck("Maintenance returns to upper district", current, exit, 2.2f, .5f, 4);
            AddSpan("Maintenance upper rejoin", Passage.Shortcut, current, exit, 2.2f);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 foot = centre + tangent * (x * 15) + outward * (z * 10) - Vector3.up * 10;
                    Box("Fire stair structural stanchion", foot + Vector3.up * 28, new Vector3(.7f, 56, .7f), 3);
                    Beam("Fire stair facade bracing", foot, foot + tangent * (-x * 30) + Vector3.up * 45, .25f, .25f, 4);
                }
            Label("MAINTENANCE ONLY\nGRATING INCOMPLETE", start - outward * 2 + Vector3.up * 2.3f, Quaternion.LookRotation(outward), .35f);
        }

        private static void DistrictLandmark(int d)
        {
            Vector3 p = Node(d * 2, 2);
            Vector3 outward = new Vector3(p.x, 0, p.z).normalized;
            p += outward * 42;
            if (d == 1)
            {
                Tank(p, 14, 43);
                Tank(p + new Vector3(5, 0, 38), 10, 32);
                CylinderBetween("District transfer artery", p + Vector3.up * 32, p - outward * 33 + Vector3.up * 32, 2, 4);
            }
            else if (d == 3 || d == 5) Crane(p, 69, d == 3 ? 62 : 48);
            else if (d == 4)
            {
                Box("Ministry monumental foundation", p - Vector3.up * 25, new Vector3(38, 50, 42), 0);
                Box("Ministry blind tower", p + Vector3.up * 34, new Vector3(32, 68, 34), 1);
                Box("Ministry dark vertical slit", p + new Vector3(0, 36, -17.05f), new Vector3(3, 54, .1f), 3, false);
                Label("THE MINISTRY\nOF CONTINUITY", p + new Vector3(0, 17, -17.12f), Quaternion.identity, .95f);
            }
            else if (d == 2)
            {
                for (int i = 0; i < 5; i++)
                {
                    Box("Disused parking annex floor", p + Vector3.up * (i * 6), new Vector3(49, 1.3f, 64), 0);
                    for (int x = -1; x <= 1; x += 2)
                        for (int z = -1; z <= 1; z += 2)
                            Box("Parking annex column", p + new Vector3(x * 20, i * 6 + 3, z * 27), new Vector3(1.7f, 6, 1.7f), 0);
                }
            }
            else if (d == 0)
            {
                Box("Intake clockhouse", p + Vector3.up * 24, new Vector3(24, 48, 24), 0);
                Box("Clockhouse recessed crown", p + Vector3.up * 50, new Vector3(29, 5, 29), 3);
            }
        }

        private static void Tank(Vector3 p, float radius, float height)
        {
            Cylinder("Industrial reservoir", p + Vector3.up * height / 2, radius, height, 0);
            Cylinder("Reservoir lid", p + Vector3.up * (height + .3f), radius + .4f, .6f, 3);
            for (int i = 1; i <= 3; i++) Cylinder("Reservoir steel binding", p + Vector3.up * height * i / 4, radius + .12f, .3f, 4);
        }

        private static void Crane(Vector3 p, float height, float reach)
        {
            Box("Crane ballast foundation", p - Vector3.up * 4, new Vector3(14, 8, 14), 0);
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Box("Lattice crane chord", p + new Vector3(x * 2, height / 2, z * 2), new Vector3(.5f, height, .5f), 4);
            for (int j = 0; j < height - 5; j += 7)
            {
                Beam("Crane lattice diagonal", p + new Vector3(-2, j, -2), p + new Vector3(2, j + 7, -2), .25f, .25f, 4);
                Beam("Crane lattice diagonal", p + new Vector3(2, j, 2), p + new Vector3(-2, j + 7, 2), .25f, .25f, 4);
                Box("Crane ladder cage", p + new Vector3(0, j, 0), new Vector3(4.2f, .25f, 4.2f), 3);
            }
            Vector3 top = p + Vector3.up * height;
            Deck("Crane maintenance jib", top - Vector3.right * 18, top + Vector3.right * reach, 2, .5f, 4);
            Beam("Crane jib top chord", top + new Vector3(-18, 3, 0), top + new Vector3(reach, 3, 0), .3f, .3f, 4);
            for (int i = -18; i < reach - 5; i += 6)
                Beam("Jib lattice", top + Vector3.right * i, top + new Vector3(i + 6, 3, 0), .2f, .2f, 4);
            Box("Crane counterweight", top + new Vector3(-15, -2, 0), new Vector3(7, 4, 6), 0);
            Box("Abandoned operator cabin", top + new Vector3(4, -2, 2), new Vector3(3, 3, 3), 3);
            CylinderBetween("Hanging crane cable", top + Vector3.right * (reach - 4), top + new Vector3(reach - 4, -31, 0), .07f, 3, false);
        }

        private static void Car(Vector3 p, Quaternion rotation, int material)
        {
            Box("Abandoned vehicle body", p + Vector3.up * .65f, new Vector3(1.9f, .85f, 4.5f), material, true, rotation);
            Box("Dust-covered vehicle cabin", p + Vector3.up * 1.3f, new Vector3(1.65f, .7f, 2.3f), 3, true, rotation);
            foreach (int side in new[] { -1, 1 })
                foreach (int axle in new[] { -1, 1 })
                    Box("Vehicle wheel", p + rotation * new Vector3(side * .95f, .35f, axle * 1.35f), new Vector3(.24f, .62f, .65f), 2, false, rotation);
        }

        private static void Lamp(Vector3 p)
        {
            Box("Dead streetlamp", p + Vector3.up * 3.8f, new Vector3(.15f, 7.6f, .15f), 3);
            Box("Dead streetlamp head", p + new Vector3(.6f, 7.6f, 0), new Vector3(1.4f, .16f, .4f), 6, false);
        }

        private static void Rail(Vector3 a, Vector3 b, bool steel)
        {
            if (!steel) { Deck("Weathered parapet / vault-ready", a + Vector3.up * .8f, b + Vector3.up * .8f, .38f, .8f, 0); return; }
            Beam("Incomplete steel handrail", a + Vector3.up * 1.05f, b + Vector3.up * 1.05f, .07f, .07f, 3);
            int n = Mathf.CeilToInt(Vector3.Distance(a, b) / 4);
            for (int i = 0; i <= n; i++) Box("Handrail stanchion", Vector3.Lerp(a, b, (float)i / n) + Vector3.up * .5f, new Vector3(.08f, 1, .08f), 3);
        }

        private static void PerimeterRoofRails(Vector3 p, float w, float l)
        {
            Rail(p + new Vector3(-w/2,0,-l/2), p + new Vector3(w/2,0,-l/2), false);
            Rail(p + new Vector3(-w/2,0,l/2), p + new Vector3(w/2,0,l/2), false);
            Rail(p + new Vector3(w/2,0,-l/2), p + new Vector3(w/2,0,l/2), false);
        }

        private static void Label(string text, Vector3 p, Quaternion rotation, float size)
        {
            var go = new GameObject("Faded signage • " + text.Replace('\n', ' '));
            go.transform.SetParent(chunk.parent); go.transform.SetPositionAndRotation(p, rotation);
            var label = go.AddComponent<TextMesh>();
            label.text = text; label.fontSize = 64; label.characterSize = size * .1f;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.color = new Color(.74f, .71f, .61f);
            go.isStatic = true;
        }

        private static void ConfigurePlayer(PlayerMovement player, StillworksRoute route)
        {
            player.transform.localScale = Vector3.one;
            player.transform.position = route.spawn;
            Vector3 forward = Node(0, 1) - Node(0, 0); forward.y = 0;
            player.transform.rotation = Quaternion.LookRotation(forward);
            foreach (CapsuleCollider c in player.GetComponents<CapsuleCollider>()) UnityEngine.Object.DestroyImmediate(c);
            foreach (MeshRenderer r in player.GetComponents<MeshRenderer>()) UnityEngine.Object.DestroyImmediate(r);
            foreach (MeshFilter f in player.GetComponents<MeshFilter>()) UnityEngine.Object.DestroyImmediate(f);
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.height = 1.8f; controller.center = Vector3.up * .9f; controller.radius = .35f; controller.stepOffset = .3f;
            var so = new SerializedObject(player);
            so.FindProperty("groundMask").intValue = 1 << 6;
            so.ApplyModifiedPropertiesWithoutUndo();
            PlayerCamera look = player.GetComponentInChildren<PlayerCamera>();
            look.transform.localPosition = Vector3.up * 1.62f;
            var cameraSettings = new SerializedObject(look);
            cameraSettings.FindProperty("slideCameraDrop").floatValue = .84f;
            cameraSettings.ApplyModifiedPropertiesWithoutUndo();
            Camera camera = player.GetComponentInChildren<Camera>();
            camera.nearClipPlane = .06f; camera.farClipPlane = 2400; camera.fieldOfView = 85;
            var session = new GameObject("Run / altitude / local timing").AddComponent<StillworksSession>();
            session.route = route; session.player = player; session.look = look;
        }

        private static void Atmosphere()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.42f, .49f, .52f);
            RenderSettings.ambientEquatorColor = new Color(.30f, .34f, .34f);
            RenderSettings.ambientGroundColor = new Color(.19f, .18f, .16f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.53f, .59f, .61f);
            RenderSettings.fogStartDistance = 700; RenderSettings.fogEndDistance = 2300;
            Light sun = new GameObject("Late afternoon / pale industrial sun").AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = new Color(1f, .87f, .68f); sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft; sun.shadowBias = .045f; sun.shadowNormalBias = .2f;
            sun.transform.rotation = Quaternion.Euler(34, -128, 0); RenderSettings.sun = sun;
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(Output + "/Materials/Stillworks sky.mat");
            if (sky == null) { sky = new Material(Shader.Find("Stillworks/Overcast sky")); AssetDatabase.CreateAsset(sky, Output + "/Materials/Stillworks sky.mat"); }
            sky.shader = Shader.Find("Stillworks/Overcast sky");
            RenderSettings.skybox = sky; EditorUtility.SetDirty(sky);
        }

        private static void AddSpan(string label, Passage kind, Vector3 a, Vector3 b, float width, bool main = true)
        {
            Spans.Add(new RouteSpan(chunk.name + " / " + label, kind, a, b, width));
            if (main && (kind == Passage.Run || kind == Passage.Ramp || kind == Passage.Jump)) metres += Vector3.Distance(a, b);
        }

        private static void Box(string name, Vector3 centre, Vector3 size, int material, bool collision = true, Quaternion? rotation = null)
        {
            Quaternion q = rotation ?? Quaternion.identity;
            Vector3[] v = {
                new Vector3(-.5f,-.5f,-.5f),new Vector3(.5f,-.5f,-.5f),new Vector3(.5f,.5f,-.5f),new Vector3(-.5f,.5f,-.5f),
                new Vector3(-.5f,-.5f,.5f),new Vector3(.5f,-.5f,.5f),new Vector3(.5f,.5f,.5f),new Vector3(-.5f,.5f,.5f)
            };
            for (int i = 0; i < v.Length; i++) v[i] = centre + q * Vector3.Scale(v[i], size);
            chunk.Geometry(material, !collision).Box(v);
            if (!collision) return;
            GameObject go = new GameObject(name); go.layer = 6; go.isStatic = true;
            go.transform.SetParent(chunk.parent); go.transform.SetPositionAndRotation(centre, q);
            go.AddComponent<BoxCollider>().size = size; colliderCount++;
        }

        private static void Beam(string name, Vector3 a, Vector3 b, float width, float depth, int material)
        { Box(name, (a + b) / 2, new Vector3(width, depth, Vector3.Distance(a, b)), material, true, Quaternion.LookRotation(b - a)); }

        // Wedge top surfaces exactly match the route endpoints, including ramp/deck junctions.
        private static void Deck(string name, Vector3 a, Vector3 b, float width, float thickness, int material)
        {
            Vector3 dir = b - a; dir.y = 0;
            float distance = dir.magnitude;
            if (distance < .01f) return;
            Quaternion rotation = Quaternion.LookRotation(dir);
            if (Mathf.Abs(a.y - b.y) < .001f)
            { Box(name, (a + b) / 2 - Vector3.up * thickness / 2, new Vector3(width, thickness, distance + .04f), material, true, rotation); return; }
            Vector3 side = Vector3.Cross(Vector3.up, dir.normalized) * width / 2;
            Vector3[] v = { a-side-Vector3.up*thickness, a+side-Vector3.up*thickness, a+side, a-side,
                b-side-Vector3.up*thickness, b+side-Vector3.up*thickness, b+side, b-side };
            chunk.Geometry(material, false).Box(v);
            var mesh = new Geometry(); mesh.Box(v);
            Mesh collision = mesh.Mesh(name);
            string path = Output + "/Meshes/" + chunk.name + "_ramp_" + chunk.ramps++ + ".asset";
            collision = SaveMesh(collision, path);
            GameObject go = new GameObject(name); go.layer = 6; go.isStatic = true; go.transform.SetParent(chunk.parent);
            go.AddComponent<MeshCollider>().sharedMesh = collision; colliderCount++;
        }

        private static void Stair(string name, Vector3 a, Vector3 b, float width, int material)
        {
            // Smooth wedge collision prevents CharacterController snagging on dozens of risers.
            Deck(name + " / smooth collision", a, b, width, .8f, material);
            int steps = Mathf.CeilToInt(Mathf.Abs(b.y - a.y) / .175f);
            Vector3 dir = b - a; dir.y = 0;
            float tread = dir.magnitude / steps;
            Quaternion rotation = Quaternion.LookRotation(dir);
            for (int i = 0; i < steps; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, (i + .5f) / steps);
                // Thin nosings follow the collision plane with less than 9cm discrepancy.
                Box("Concrete stair nosing", p, new Vector3(width, .065f, Mathf.Max(.1f, tread * .18f)), material, false, rotation);
            }
        }

        private static void Cylinder(string name, Vector3 p, float radius, float height, int material)
        { CylinderBetween(name, p - Vector3.up * height / 2, p + Vector3.up * height / 2, radius, material); }

        private static void CylinderBetween(string name, Vector3 a, Vector3 b, float radius, int material, bool collision = true)
        {
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
            Vector3 centre = (a + b) / 2; float height = Vector3.Distance(a, b);
            Geometry geometry = chunk.Geometry(material, !collision);
            const int sides = 16;
            for (int i = 0; i < sides; i++)
            {
                float t = i * Mathf.PI * 2 / sides, u = (i + 1) * Mathf.PI * 2 / sides;
                Vector3 p = rotation * new Vector3(Mathf.Cos(t) * radius, 0, Mathf.Sin(t) * radius);
                Vector3 q = rotation * new Vector3(Mathf.Cos(u) * radius, 0, Mathf.Sin(u) * radius);
                geometry.Quad(a + p, b + p, b + q, a + q);
                geometry.Triangle(a, a + p, a + q);
                geometry.Triangle(b, b + q, b + p);
            }
            if (collision)
            {
                var go = new GameObject(name); go.layer = 6; go.isStatic = true;
                go.transform.SetParent(chunk.parent); go.transform.SetPositionAndRotation(centre, rotation);
                // Conservative box colliders provide inexpensive, stable pipe/tank landings.
                go.AddComponent<BoxCollider>().size = new Vector3(radius * 1.8f, height, radius * 1.8f); colliderCount++;
            }
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing;
        }

        private sealed class Geometry
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<int> triangles = new List<int>();
            public void Triangle(Vector3 a, Vector3 b, Vector3 c)
            { int n = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); triangles.Add(n); triangles.Add(n+1); triangles.Add(n+2); }
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            { int n = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d); triangles.AddRange(new[] {n,n+1,n+2,n,n+2,n+3}); }
            public void Box(Vector3[] v)
            { for (int i = 0; i < Faces.Length; i += 4) Quad(v[Faces[i]],v[Faces[i+1]],v[Faces[i+2]],v[Faces[i+3]]); }
            public Mesh Mesh(string name)
            {
                var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
            }
        }

        private sealed class Chunk
        {
            public readonly string name;
            public readonly Transform parent;
            public int ramps;
            private readonly Dictionary<int, Geometry> solids = new Dictionary<int, Geometry>();
            private readonly Dictionary<int, Geometry> details = new Dictionary<int, Geometry>();
            public Chunk(string label, Transform owner)
            { name = label; parent = new GameObject(label).transform; parent.SetParent(owner); }
            public Geometry Geometry(int material, bool detail)
            {
                var set = detail ? details : solids;
                if (!set.TryGetValue(material, out Geometry g)) { g = new Geometry(); set.Add(material, g); }
                return g;
            }
            public void Bake()
            {
                BakeSet(solids, false); BakeSet(details, true);
            }
            private void BakeSet(Dictionary<int, Geometry> source, bool detail)
            {
                var renderers = new List<Renderer>();
                Transform group = new GameObject(detail ? "Secondary details / distance LOD" : "Combined structural meshes").transform;
                group.SetParent(parent);
                foreach (var pair in source)
                {
                    string label = name + (detail ? "_detail_" : "_structure_") + pair.Key;
                    Mesh mesh = SaveMesh(pair.Value.Mesh(label), Output + "/Meshes/" + label + ".asset");
                    var go = new GameObject(palette[pair.Key].name); go.transform.SetParent(group); go.isStatic = true;
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = palette[pair.Key];
                    renderer.shadowCastingMode = detail ? ShadowCastingMode.Off : ShadowCastingMode.On;
                    renderers.Add(renderer);
                }
                if (detail && renderers.Count > 0)
                {
                    var lod = group.gameObject.AddComponent<LODGroup>();
                    lod.SetLODs(new[] { new LOD(.045f, renderers.ToArray()) }); lod.RecalculateBounds();
                }
            }
        }

        [MenuItem("Stillworks/Validate current map")]
        public static void Validate()
        {
            StillworksRoute route = AssetDatabase.LoadAssetAtPath<StillworksRoute>(Output + "/StillworksRoute.asset");
            if (route == null) throw new InvalidOperationException("Build the map first.");
            Physics.SyncTransforms();
            var failures = new List<string>();
            int samples = 0, jumps = 0, slides = 0;
            foreach (RouteSpan span in route.spans)
            {
                if (span.passage == Passage.Jump)
                {
                    jumps++;
                    float flightTime = 2 * Mathf.Sqrt(2 * 2.2f / 25);
                    if (Vector3.Distance(span.from, span.to) > 14 * flightTime * .7f)
                        failures.Add(span.name + ": outside conservative sprint jump envelope");
                    continue;
                }
                if (span.passage == Passage.Slide) slides++;
                int n = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(span.from, span.to) / 2));
                for (int i = 1; i < n; i++)
                {
                    samples++;
                    Vector3 p = Vector3.Lerp(span.from, span.to, (float)i / n);
                    if (!Physics.Raycast(p + Vector3.up * .2f, Vector3.down, out RaycastHit hit, .65f, 1 << 6))
                    { failures.Add(span.name + $": unsupported floor near {p}"); break; }
                    float height = span.passage == Passage.Slide ? 1.0f : 1.8f;
                    Collider[] blocked = Physics.OverlapCapsule(p + Vector3.up * .43f, p + Vector3.up * (height - .3f), .27f, 1 << 6, QueryTriggerInteraction.Ignore);
                    // Shallow overlap with the ramp underfoot is excluded via actual top height.
                    foreach (Collider c in blocked)
                    {
                        if (c == hit.collider) continue;
                        failures.Add(span.name + $": blocked by {c.name} near {p}"); break;
                    }
                    if (failures.Count > 0 && failures[failures.Count - 1].StartsWith(span.name + ": blocked")) break;
                }
            }
            var report = new System.Text.StringBuilder();
            report.AppendLine("THE STILLWORKS — generated geometry validation");
            report.AppendLine($"Main route: {route.mainRouteMetres:0} m; summit: {route.summit.y:0} m; districts: 7");
            report.AppendLine($"14 m/s uninterrupted sprint floor: {route.mainRouteMetres / 14 / 60:0.0} minutes. 15–30 minute target requires human playtesting.");
            report.AppendLine($"Ground/headroom samples: {samples}; jump spans: {jumps}; slide openings: {slides}");
            report.AppendLine($"Issues: {failures.Count}");
            foreach (string failure in failures) report.AppendLine(failure);
            Directory.CreateDirectory("Documentation"); File.WriteAllText("Documentation/Stillworks-validation.txt", report.ToString());
            if (failures.Count > 0) Debug.LogError(report.ToString()); else Debug.Log(report.ToString());
        }

        // Reproducible renders are useful in CI and do not require entering play mode.
        public static void Capture()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            camera.transform.SetParent(null);
            Directory.CreateDirectory("Documentation/Previews");
            Vector3[] positions = { new Vector3(-650, 540, -760), Node(0,0) + Vector3.up * 1.62f, new Vector3(-340, 420, 280), new Vector3(0, 596, -22) };
            Vector3[] targets = { new Vector3(0,300,0), Node(0,1) + Vector3.up * 13, new Vector3(0,340,0), new Vector3(-105,240,-130) };
            string[] names = { "01-city-overview", "02-first-person-intake", "03-ministry-and-construction", "04-summit-view" };
            for (int i = 0; i < positions.Length; i++)
            {
                camera.transform.position = positions[i]; camera.transform.LookAt(targets[i]); camera.fieldOfView = i == 0 ? 55 : 78;
                var target = new RenderTexture(1600, 1000, 24); camera.targetTexture = target; camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(1600,1000,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply();
                File.WriteAllBytes("Documentation/Previews/" + names[i] + ".png", image.EncodeToPNG());
                camera.targetTexture = null; RenderTexture.active = null; UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
            }
            Debug.Log("STILLWORKS: four previews captured.");
        }

        public static void BuildAndCapture()
        {
            Build();
            Capture();
        }
    }
}
