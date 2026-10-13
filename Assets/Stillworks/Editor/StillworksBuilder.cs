using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
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
        private static readonly int[] Accent = { 9, 4, 5, 10, 11, 9, 10 };
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

        public static void PrepareGameResources()
        {
            if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            { EditorApplication.Exit(0); return; }
            AssetDatabase.importPackageCompleted += package => {
                AssetDatabase.SaveAssets();
                Debug.Log("Stillworks UI resources imported: " + package);
                EditorApplication.Exit(0);
            };
            AssetDatabase.importPackageFailed += (package, error) => {
                Debug.LogError(error); EditorApplication.Exit(1);
            };
            TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
        }

        [MenuItem("Stillworks/Build playable map")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                TMPro.TMP_PackageResourceImporter.ImportResources(true, false, false);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
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
            Label("THE CROWN\nROOFTOP DRYING YARD", new Vector3(0, 590.5f, 24), Quaternion.identity, 1.1f);
            Box("Summit survey table", new Vector3(0, 588.5f, 5), new Vector3(4, 1, 2), 3);
            LaundryLine(summit);
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
            Color[] colors = { new Color(.47f,.48f,.45f), new Color(.63f,.62f,.56f), new Color(.22f,.25f,.25f), new Color(.13f,.17f,.18f), new Color(.43f,.23f,.14f), new Color(.65f,.51f,.25f), new Color(.74f,.73f,.64f), new Color(.17f,.23f,.16f), new Color(.12f,.20f,.21f), new Color(.22f,.47f,.65f), new Color(.65f,.25f,.18f), new Color(.15f,.48f,.40f) };
            string[] names = { "Boardformed concrete", "Chalk concrete", "Asphalt", "Oxidised steel", "Iron oxide", "Faded ochre", "Old enamel", "Moss", "Reservoir water", "Faded service blue", "Signal oxide red", "Patina teal" };
            Texture2D[] surfaces = StillworksSurfaceTextures.Bake();
            palette = new Material[colors.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                string path = Output + "/Materials/" + names[i] + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
                material.shader = i == 8 ? Shader.Find("Stillworks/Reservoir water") : shader;
                material.SetColor("_BaseColor", colors[i]);
                material.SetFloat("_Weather", i == 3 || i == 4 ? .85f : .45f);
                material.SetFloat("_Board", i < 2 ? 1 : 0);
                if (i != 8)
                {
                    bool painted = i == 5 || i == 6 || i >= 9;
                    material.SetTexture("_DetailTex",surfaces[painted ? 3 : i == 2 ? 1 : i == 3 || i == 4 ? 2 : 0]);
                    material.SetFloat("_SurfaceScale",i == 2 ? 4 : 3);
                    material.SetFloat("_Bump",i == 2 ? .012f : .008f);
                    material.SetFloat("_Paint",painted ? 1 : 0);
                }
                material.enableInstancing = true;
                EditorUtility.SetDirty(material);
                palette[i] = material;
            }
        }

        private static void Foundation()
        {
            chunk = new Chunk("00_raft_and_city", root); Chunks.Add(chunk);
            Box("Abandoned concrete floodplain", new Vector3(0, -12, 0), new Vector3(18000, 4, 18000), 2);
            Box("City foundation raft", new Vector3(0, -5.18f, 0), new Vector3(650, 10, 650), 2);
            foreach (Vector3 direction in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                Vector3 from = direction * 375 - Vector3.up * 10, to = direction * 325;
                Deck("Floodplain access ramp", from, to, 24, 1.4f, 0);
                AddSpan("Floodplain return to city", Passage.Recovery, from, to, 24);
            }
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
            Surroundings();
            Vector3 shelter = Node(0,0);
            Vector3 forward = Node(0,1) - shelter; forward.y = 0; forward.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up,forward);
            Box("Laundry receiving shelter roof",shelter + Vector3.up * 3.8f,new Vector3(12,.45f,10),1,true,Quaternion.LookRotation(forward));
            foreach (float sign in new[] { -1f,1f })
                Box("Shelter corner pier",shelter + side * sign * 5.8f + forward * 4 + Vector3.up * 1.9f,new Vector3(.35f,3.8f,.35f),3);
            Label("BRING THE WASHING IN\nSTREET SHELTER",shelter - forward * 3 + Vector3.up * 3.1f,Quaternion.LookRotation(-forward),.35f);
        }

        private static void LaundryLine(Vector3 summit)
        {
            Beam("Rooftop clothesline",summit + new Vector3(-4,3.5f,5),summit + new Vector3(4,3.5f,5),.025f,.025f,3);
            foreach (float sign in new[] { -1f,1f })
                Cylinder("Clothesline pole",summit + new Vector3(sign * 4,1.75f,5),.075f,3.5f,3);
            var laundry = new GameObject("Laundry to bring home"); laundry.transform.SetParent(root);
            string path = Output + "/Materials/Weathered linen.mat";
            Material fabric = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (fabric == null) { fabric = new Material(Shader.Find("Stillworks/Board concrete")); AssetDatabase.CreateAsset(fabric,path); }
            fabric.shader = Shader.Find("Stillworks/Board concrete");
            fabric.SetColor("_BaseColor",new Color(.86f,.83f,.74f)); fabric.SetFloat("_Board",0); fabric.SetFloat("_Weather",.12f); EditorUtility.SetDirty(fabric);
            for (int i = 0; i < 6; i++)
            {
                var cloth = GameObject.CreatePrimitive(PrimitiveType.Cube); cloth.name = "Hanging laundry " + (i + 1);
                UnityEngine.Object.DestroyImmediate(cloth.GetComponent<Collider>());
                cloth.transform.SetParent(laundry.transform);
                cloth.transform.position = summit + new Vector3(-3 + i * 1.2f,2.68f,5);
                cloth.transform.localScale = new Vector3(.9f,1.5f - i % 2 * .3f,.025f);
                cloth.transform.rotation = Quaternion.Euler(0,0,(i % 3 - 1) * 4);
                Material garment = i == 1 ? palette[9] : i == 3 ? palette[10] : i == 4 ? palette[5] : fabric;
                cloth.GetComponent<Renderer>().sharedMaterial = garment;
                if (i == 1 || i == 3)
                    foreach (float sign in new[] { -1f,1f })
                    {
                        var sleeve = GameObject.CreatePrimitive(PrimitiveType.Cube); sleeve.name = "Shirt sleeve";
                        UnityEngine.Object.DestroyImmediate(sleeve.GetComponent<Collider>());
                        sleeve.transform.SetParent(cloth.transform,false);
                        sleeve.transform.localPosition = new Vector3(sign * .56f,.24f,0);
                        sleeve.transform.localScale = new Vector3(.4f,.26f,1);
                        sleeve.transform.localRotation = Quaternion.Euler(0,0,-sign * 20);
                        sleeve.GetComponent<Renderer>().sharedMaterial = garment;
                    }
                Box("Wooden clothes peg",summit + new Vector3(-3 + i * 1.2f,3.42f,5),new Vector3(.08f,.20f,.08f),5,false);
            }
        }

        private static void Surroundings()
        {
            // Reservoirs sit on the floodplain; the existing dry return ramps remain unobstructed.
            foreach (float sign in new[] { -1f,1f })
            {
                Box("Flood retention basin",new Vector3(sign * 510,-9.82f,0),new Vector3(240,.03f,900),8,false);
                Box("Reservoir outer retaining wall",new Vector3(sign * 640,-8.8f,0),new Vector3(6,2.4f,940),0);
                Box("Settling basin",new Vector3(0,-9.82f,sign * 510),new Vector3(780,.03f,240),8,false);
                Box("Northern / southern flood embankment",new Vector3(0,-8.8f,sign * 640),new Vector3(1290,2.4f,6),0);
                for (int i = 0; i < 8; i++)
                    Box("Water-stained reservoir divider",new Vector3(sign * 510,-9.3f,-390 + i * 110),new Vector3(230,1.4f,2),0);
            }
            for (int i = 0; i < 18; i++)
            {
                float angle = (i + .35f) * Mathf.PI * 2 / 18;
                float radius = 920 + i % 4 * 105;
                Vector3 p = new Vector3(Mathf.Cos(angle) * radius,-10,Mathf.Sin(angle) * radius);
                float height = 45 + i % 5 * 19;
                Box("Distant utility hall",p + Vector3.up * height / 2,new Vector3(55,height,40),i % 2);
                Box("Industrial roof duct",p + Vector3.up * (height + 3),new Vector3(43,6,10),3);
                if (i % 3 == 0)
                {
                    Vector3 basePoint = p + new Vector3(65,0,0);
                    CylinderBetween("Distant exhaust stack",basePoint,basePoint + Vector3.up * (height + 70),7,0,false);
                    CylinderBetween("Rust-stained exhaust crown",basePoint + Vector3.up * (height + 63),basePoint + Vector3.up * (height + 73),7.3f,4,false);
                    Box("Stack foundation",basePoint + Vector3.up * 6,new Vector3(24,12,24),0);
                }
            }
            for (int i = 0; i < 36; i++)
            {
                float angle = (i + .5f) * Mathf.PI * 2 / 36;
                Vector3 p = new Vector3(Mathf.Cos(angle) * 337,-9.97f,Mathf.Sin(angle) * 337);
                Box("Floodplain moss bed",p,new Vector3(10,.04f,7),7,false);
                for (int reed = 0; reed < 8; reed++)
                {
                    Vector3 foot = p + new Vector3((reed % 4 - 1.5f) * 1.3f,0,(reed / 4 - .5f) * 2);
                    Vector3 tip = foot + Vector3.up * (.7f + reed % 3 * .3f) + Vector3.right * .25f;
                    chunk.Geometry(7,true).Triangle(foot - Vector3.right * .08f,tip,foot + Vector3.right * .08f);
                    chunk.Geometry(7,true).Triangle(foot - Vector3.forward * .08f,tip,foot + Vector3.forward * .08f);
                }
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
            Box("Structural junction court", a - Vector3.up * .85f, new Vector3(width + 13, 1.6f, width + 13), d == 2 ? 2 : 0);
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
                AddSpan("Broken expansion joint", Passage.Jump, lip, land, width);
                Box("Exposed fracture reinforcement", mid - Vector3.up * .4f + side * (width / 2 - .6f), new Vector3(.14f, .14f, gap + .8f), 4, false, rotation);
                Recovery(mid, dir, side, width, gap);
            }
            else
            {
                Deck("Occupied floor slab", a, flatEnd, width, 1.1f, d == 2 ? 2 : 0);
            }
            TraversalObstacles(d, storey, wing, a, flatEnd, dir, inside, width, broken);
            if ((d == 0 || d == 3 || d == 4 || d == 6) && wing % 2 == 0)
                Stair("Processional staircase", flatEnd, rampEnd, width * .7f, 1);
            else Deck("Graded service ramp", flatEnd, rampEnd, width * .78f, 1, d == 2 ? 2 : 0);
            AddSpan("Ascending circulation", Passage.Ramp, flatEnd, rampEnd, width * .7f);
            bool precision = (d == 3 || d == 5) && wing % 2 == 1;
            if (precision)
            {
                Deck("Narrow load-bearing transfer girder",rampEnd,b,.65f,.7f,Accent[d]);
                AddSpan("Precision transfer girder (65cm)",Passage.Precision,rampEnd,b,.65f);
                PrecisionRecovery(rampEnd,b,-inside,width);
            }
            else
            {
                Deck("Upper transfer terrace", rampEnd, b, width, 1.2f, 1);
                AddSpan("Upper terrace sprint", Passage.Run, rampEnd, b, width);
            }

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
                foreach (float t in new[] { .17f, .39f, .61f, .84f })
                {
                    Vector3 p = Vector3.Lerp(a, flatEnd, t) - inside * (width / 2 + 4);
                    Box("Column arcade", p + Vector3.up * ((clearance - 1) / 2), new Vector3(1.2f, clearance - 1, 1.2f), 1, true, rotation);
                }
            }
            // Perimeter rails are deliberately incomplete; lower architecture remains reachable.
            Rail(Vector3.Lerp(a, flatEnd, .17f) - inside * width / 2, Vector3.Lerp(a, flatEnd, .25f) - inside * width / 2, d >= 3);
            Rail(rampEnd - inside * width / 2, b - inside * width / 2, d >= 3);
            if (wing == 0 || wing == 3) Exploration(galleryA, Vector3.Lerp(a, flatEnd, .98f), dir, inside, width, d);
            DressWing(d, storey, wing, a, flatEnd, dir, inside, width);
            var paintRandom = new System.Random(4409 + d * 101 + storey * 37 + wing * 7);
            float facadeLength = Vector3.Distance(galleryA,galleryB);
            for (int panel = 0; panel < 3; panel++)
            {
                float t = .14f + panel * .31f;
                float paintHeight = wallHeight * (.40f + (float)paintRandom.NextDouble() * .35f);
                Box("Flaking district facade paint",Vector3.Lerp(galleryA,galleryB,t) + inside * (width / 2 + .035f) + Vector3.up * (paintHeight / 2 + .6f),
                    new Vector3(.09f,paintHeight,facadeLength * (.11f + (float)paintRandom.NextDouble() * .08f)),Accent[d],false,rotation);
            }
            Label($"{d + 1:00} / {storey * 6 + wing + 1:00}\nKEEP SERVICES CLEAR",galleryA + dir * 10 + inside * (width / 2 - .015f) + Vector3.up * 2.7f,
                Quaternion.LookRotation(inside),.42f);
            Box("Faded floor service marking",a + dir * 10 + Vector3.up * .018f,new Vector3(width * .6f,.025f,.3f),Accent[d],false,rotation);
            if (wing == 0)
            {
                Label($"{d + 1:00}  /  {Districts[d].ToUpperInvariant()}\n{a.y:000} M     SERVICE ACCESS", a + inside * (width / 2 + .06f) + dir * 18 + Vector3.up * 4,
                    Quaternion.LookRotation(inside), .7f);
            }
        }

        private static void TraversalObstacles(int d, int storey, int wing, Vector3 a, Vector3 b,
            Vector3 dir, Vector3 side, float width, bool broken)
        {
            var random = new System.Random(9147 + d * 997 + storey * 113 + wing * 31);
            int variant = wing == 0 && storey == 0 ? 0 : random.Next(6);
            Vector3 cursor = a;
            Quaternion rotation = Quaternion.LookRotation(dir);
            if ((variant == 1 || variant == 4) && width >= 12)
            {
                float first = .22f + (float)random.NextDouble() * .05f;
                float firstSign = random.Next(2) == 0 ? 1 : -1;
                for (int i = 0; i < 2; i++)
                {
                    Vector3 p = Vector3.Lerp(a, b, first + i * .17f);
                    float sign = i == 0 ? firstSign : -firstSign;
                    float lane = sign * width * .28f;
                    if (variant == 1)
                    {
                        float height = 1.8f + (float)random.NextDouble() * 1.2f;
                        Box("Staggered abandoned compressor bank",p - side * sign * width * .22f + Vector3.up * height / 2,
                            new Vector3(width * .56f,height,2.4f),Accent[d],true,rotation);
                        for (int fan = 0; fan < 3; fan++)
                            Cylinder("Compressor exhaust housing",p - side * sign * width * .22f + side * (fan - 1) * width * .12f + Vector3.up * (height + .25f),.45f,.5f,3);
                    }
                    else
                    {
                        // Offset openings through shutter banks force an actual direction change.
                        const float opening = 3.4f;
                        float left = width / 2 + lane - opening / 2, right = width / 2 - lane - opening / 2;
                        Box("Jammed loading shutter / left leaf",p + side * (-width / 2 + left / 2) + Vector3.up * 2.2f,new Vector3(left,4.4f,.55f),3,true,rotation);
                        Box("Jammed loading shutter / right leaf",p + side * (width / 2 - right / 2) + Vector3.up * 2.2f,new Vector3(right,4.4f,.55f),Accent[d],true,rotation);
                        Box("Loading door transom",p + Vector3.up * 4.6f,new Vector3(width,.4f,.9f),4,true,rotation);
                    }
                    Vector3 before = p - dir * 4 + side * lane;
                    Vector3 after = p + dir * 4 + side * lane;
                    AddSpan("Weave approach", Passage.Run, cursor, before, 2.2f);
                    AddSpan(variant == 1 ? "Weave through infrastructure" : "Weave through offset loading door",Passage.Run,before,after,2.2f);
                    cursor = after;
                }
                if (variant == 4) GuardRails(Vector3.Lerp(a,b,first) - dir * 6,cursor + dir * 2,width,Accent[d]);
            }
            else if (variant == 3)
            {
                Vector3 p = Vector3.Lerp(a,b,.28f + (float)random.NextDouble() * .05f);
                float height = .85f + (float)random.NextDouble() * .3f;
                Box("Raised cable inspection bed",p + Vector3.up * height / 2,new Vector3(width,height,5),1,true,rotation);
                for (int slot = 0; slot < 5; slot++)
                    Box("Inspection bed steel cover",p + dir * (slot - 2) + Vector3.up * (height + .015f),new Vector3(width * .8f,.025f,.75f),3,false,rotation);
                Vector3 before = p - dir * 5, upperA = p - dir * 2 + Vector3.up * height;
                Vector3 upperB = p + dir * 2 + Vector3.up * height, after = p + dir * 5;
                AddSpan("Inspection bed approach",Passage.Run,cursor,before,width);
                AddSpan("Jump onto inspection bed",Passage.Jump,before,upperA,width);
                AddSpan("Inspection bed crossing",Passage.Run,upperA,upperB,width);
                AddSpan("Drop from inspection bed",Passage.Jump,upperB,after,width);
                GuardRails(before - dir * 2,after + dir * 2,width,Accent[d]); cursor = after;
            }
            else
            {
                Vector3 p = Vector3.Lerp(a,b,.24f + (float)random.NextDouble() * .12f);
                float height = .65f + (float)random.NextDouble() * .45f;
                if (variant == 2)
                {
                    for (int pipe = 0; pipe < 2; pipe++)
                        CylinderBetween("Fallen transverse pipe bundle",p + dir * (pipe == 0 ? -.7f : .7f) - side * width / 2 + Vector3.up * .48f,
                            p + dir * (pipe == 0 ? -.7f : .7f) + side * width / 2 + Vector3.up * .48f,.45f,4);
                }
                else if (variant == 5)
                {
                    Box("Toppled electrical cabinets",p + Vector3.up * .6f,new Vector3(width,1.2f,1.7f),Accent[d],true,rotation);
                    for (int door = 0; door < Mathf.CeilToInt(width / 2); door++)
                        Box("Cabinet access panel",p + side * (-width / 2 + 1 + door * 2) + Vector3.up * 1.215f,new Vector3(1.65f,.025f,1.3f),3,false,rotation);
                }
                else
                {
                    Quaternion skew = rotation * Quaternion.Euler(0,random.Next(-5,6),0);
                    Box(d == 3 ? "Fallen shuttering girder" : "Cross-gallery concrete divider",p + Vector3.up * height / 2,new Vector3(width + 1,height,.7f),d == 3 ? 4 : 0,true,skew);
                    Box("Weathered divider top stripe",p + Vector3.up * (height + .015f),new Vector3(width * .85f,.025f,.15f),5,false,skew);
                }
                Vector3 before = p - dir * 3, after = p + dir * 3;
                AddSpan("Divider run-up", Passage.Run, cursor, before, width);
                AddSpan("Jump over divider / " + (variant == 2 ? "pipe bundle" : variant == 5 ? "toppled cabinets" : "skewed girder"),Passage.Jump,before,after,width);
                GuardRails(before - dir * 3,after + dir * 3,width,Accent[d]);
                cursor = after;
            }
            if (broken)
            {
                Vector3 mid = Vector3.Lerp(a, b, .59f);
                float gap = d >= 5 ? 4.2f : 3.2f;
                AddSpan("Gallery approach", Passage.Run, cursor, mid - dir * gap / 2, width);
                cursor = mid + dir * gap / 2;
                Vector3 wallLane = side * (width / 2 - .45f);
                AddSpan("Facade wall-run crossing", Passage.WallRun,
                    mid - dir * (gap / 2 + 2) + wallLane, mid + dir * (gap / 2 + 2) + wallLane, 1, false);
            }
            if (!broken && random.Next(3) == 0)
            {
                Vector3 p = Vector3.Lerp(a,b,.62f);
                Box("Abandoned pallet of shuttering",p + Vector3.up * .45f,new Vector3(width,.9f,1.6f),4,true,rotation);
                AddSpan("Shuttering approach",Passage.Run,cursor,p - dir * 3,width);
                AddSpan("Jump over abandoned shuttering",Passage.Jump,p - dir * 3,p + dir * 3,width);
                GuardRails(p - dir * 5,p + dir * 5,width,Accent[d]); cursor = p + dir * 3;
            }
            if (wing != 0 && random.Next(4) == 0)
            { AddSpan("Open sprint gallery",Passage.Run,cursor,b,width); return; }
            Vector3 gate = Vector3.Lerp(a,b,.85f + (float)random.NextDouble() * .05f);
            Vector3 gateA = gate - dir * 3, gateB = gate + dir * 3;
            AddSpan("Underpass run-up", Passage.Run, cursor, gateA, width);
            int gateKind = random.Next(4);
            if (gateKind == 1)
            {
                CylinderBetween("Sagging insulated service main / slide",gate - side * (width / 2 + .6f) + Vector3.up * 1.8f,
                    gate + side * (width / 2 + .6f) + Vector3.up * 1.8f,.55f,4);
                foreach (float x in new[] { -.3f,.3f })
                    Box("Pipe joint flange",gate + side * width * x + Vector3.up * 1.8f,new Vector3(.18f,1.1f,1.1f),3,true,rotation);
            }
            else if (gateKind == 2)
            {
                Box("Half-closed roller shutter / slide",gate + Vector3.up * 2.075f,new Vector3(width + 1.2f,1.65f,.35f),Accent[d],true,rotation);
                for (int slat = 0; slat < 6; slat++)
                    Box("Shutter corrugation",gate - dir * .19f + Vector3.up * (1.35f + slat * .27f),new Vector3(width,.055f,.04f),3,false,rotation);
            }
            else if (gateKind == 3)
            {
                Box("Broken ventilation hood / slide",gate + Vector3.up * 1.7f,new Vector3(width + 1.2f,.9f,2.2f),11,true,rotation);
                for (int grille = 0; grille < 8; grille++)
                    Box("Ventilation hood grille",gate - dir * 1.12f + side * (grille - 3.5f) * width / 8 + Vector3.up * 1.7f,new Vector3(.1f,.65f,.045f),3,false,rotation);
            }
            else Box("Collapsed service lintel / slide clearance 1.25m",gate + Vector3.up * 1.75f,new Vector3(width + 1.2f,1,2.8f),0,true,rotation);
            foreach (float sign in new[] { -1f, 1f })
                Box("Underpass bearing pier", gate + side * sign * (width / 2 + .85f) + Vector3.up * .9f,
                    new Vector3(.6f, 1.8f, 2.8f), 0, true, rotation);
            Box("Underpass clearance marking", gate - dir * 1.43f + Vector3.up * 1.55f,
                new Vector3(width * .9f, .15f, .04f), 5, false, rotation);
            AddSpan("Main gallery slide", Passage.Slide, gateA, gateB, width);
            GuardRails(gateA - dir * 5,gateB + dir,width,Accent[d]);
            Deck("Service tunnel roof prevents jumping over lintel",gateA - dir * 5 + Vector3.up * 3.7f,
                gateB + dir * 5 + Vector3.up * 3.7f,width + 1.4f,.7f,0);
            AddSpan("Gallery exit", Passage.Run, gateB, b, width);
        }

        private static void GuardRails(Vector3 a, Vector3 b, float width, int colour)
        {
            Vector3 dir = (b - a).normalized, side = Vector3.Cross(Vector3.up,dir);
            Quaternion rot = Quaternion.LookRotation(dir);
            foreach (float sign in new[] { -1f,1f })
            {
                Vector3 p = a + side * sign * (width / 2 + .18f), q = b + side * sign * (width / 2 + .18f);
                var fence = new GameObject("Obstacle guard fence / prevents lane bypass"); fence.layer = 6; fence.isStatic = true;
                fence.transform.SetParent(chunk.parent); fence.transform.SetPositionAndRotation((p + q) / 2 + Vector3.up * 1.8f,rot);
                fence.AddComponent<BoxCollider>().size = new Vector3(.16f,3.6f,Vector3.Distance(a,b)); colliderCount++;
                for (int row = 0; row <= 6; row++)
                    Box("Painted guard rail",(p + q) / 2 + Vector3.up * (.08f + row * .58f),new Vector3(.075f,.075f,Vector3.Distance(a,b)),colour,false,rot);
                int posts = Mathf.CeilToInt(Vector3.Distance(a,b) / 2);
                for (int i = 0; i <= posts; i++)
                    Box("Guard fence upright",Vector3.Lerp(p,q,(float)i / posts) + Vector3.up * 1.8f,new Vector3(.075f,3.6f,.075f),3,false,rot);
            }
        }

        private static void PrecisionRecovery(Vector3 a, Vector3 b, Vector3 outside, float width)
        {
            Vector3 mid = (a + b) / 2, dir = (b - a).normalized;
            Deck("Lower transfer maintenance roof",a - dir * 8 - Vector3.up * 6,b + dir * 4 - Vector3.up * 6,width + 40,.7f,2);
            Vector3 from = mid - dir * 16 + outside * (width / 2 + 18) - Vector3.up * 6;
            Vector3 to = mid + outside * (width / 2 + 18);
            Deck("Transfer roof recovery ramp",from,to,3,.5f,Accent[3]);
            Deck("Transfer recovery rejoin",to,mid,3,.5f,3);
            AddSpan("Transfer roof recovery ramp",Passage.Recovery,from,to,3);
            AddSpan("Transfer roof recovery rejoin",Passage.Recovery,to,mid,3);
        }

        private static void Recovery(Vector3 mid, Vector3 dir, Vector3 side, float width, float gap)
        {
            Vector3 catchA = mid - dir * 13 - Vector3.up * 5;
            Vector3 catchB = mid + dir * 14 - Vector3.up * 5;
            Deck("Lower maintenance catch floor", catchA, catchB, width + 12, .8f, 2);
            Vector3 rampA = mid - dir * 6 - Vector3.up * 5 + side * (width / 2 + 3);
            Vector3 rampB = mid + dir * 6 + side * (width / 2 + 3);
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
                    if (i % 2 == 0) Car(p - inside * (width / 2 - 2), rot, i == 0 ? 10 : 9);
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
                    if (i == 1 || i == 3) Car(p - inside * 8, Quaternion.LookRotation(inside), 9);
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
                    Box("Ministry facade fin", edge + inside * 3.2f + Vector3.up * 12, new Vector3(3, 24, 1.6f), 1, true, rot);
                    Box("Old enamel information panel", edge + inside * 1.4f + Vector3.up * 4, new Vector3(.1f, 2, 4), 3, false, rot);
                }
                else if (d == 5)
                {
                    Box("Relay cable riser", edge + inside * 2 + Vector3.up * 18, new Vector3(1, 36, 1.3f), 3);
                    Beam("Relay riser cross arm", edge + inside * 2 + Vector3.up * 25, edge - inside * 5 + Vector3.up * 25, .25f, .4f, 4);
                }
                else
                {
                    Box("Crown concrete blade", edge + inside * 2.7f + Vector3.up * 12, new Vector3(2, 24, 3), 1, true, rot);
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
            Deck("Unmarked maintenance access", entry + Vector3.up * .035f, start, 2.2f, .5f, 4);
            AddSpan("Maintenance access", Passage.Shortcut, entry, start, 2.2f);
            Vector3 current = start;
            for (int flight = 0; flight < 6; flight++)
            {
                Vector3 next = centre + tangent * (flight % 2 == 0 ? 13 : -13) + outward * (flight % 2 == 0 ? -8 : 8);
                next.y = entry.y + (flight + 1) * 7;
                float stairWidth = d >= 3 ? .72f : 1.5f;
                Stair("Narrow exposed fire stair", current, next, stairWidth, Accent[d]);
                AddSpan("Exposed service stair", Passage.Shortcut, current, next, stairWidth);
                if (flight < 5)
                {
                    Vector3 across = next + outward * (flight % 2 == 0 ? 16 : -16);
                    Vector3 direction = (across - next).normalized;
                    float gap = d >= 3 ? 6.4f : 3.8f, landingWidth = d >= 3 ? .85f : 2.3f;
                    Vector3 lip = Vector3.Lerp(next, across, .5f) - direction * gap / 2;
                    Vector3 landing = lip + direction * gap;
                    Deck("Missing fire-escape grating / takeoff", next, lip, landingWidth, .3f, 3);
                    Deck("Missing fire-escape grating / landing", landing, across, landingWidth, .3f, 3);
                    AddSpan("Service landing takeoff", Passage.Shortcut, next, lip, landingWidth);
                    AddSpan("Shortcut gap (" + gap.ToString("0.0") + "m)", Passage.Jump, lip, landing, landingWidth, false);
                    AddSpan("Service landing exit", Passage.Shortcut, landing, across, landingWidth);
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
            var effects = player.gameObject.AddComponent<StillworksEffects>(); effects.player = player;
            string particlePath = Output + "/Materials/Concrete dust.mat";
            Material dust = AssetDatabase.LoadAssetAtPath<Material>(particlePath);
            if (dust == null) { dust = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(dust, particlePath); }
            dust.SetFloat("_Surface",1); dust.SetFloat("_Blend",0);
            dust.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha); dust.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            dust.SetFloat("_ZWrite",0); dust.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); dust.renderQueue = 3000;
            string texturePath = Output + "/Materials/Dust.png";
            if (!File.Exists(texturePath))
            {
                var texture = new Texture2D(32,32,TextureFormat.RGBA32,false);
                for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
                {
                    float alpha = Mathf.Clamp01(1 - Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f)) / 15.5f);
                    texture.SetPixel(x,y,new Color(1,1,1,alpha * alpha));
                }
                texture.Apply(); File.WriteAllBytes(texturePath,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
            }
            dust.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            EditorUtility.SetDirty(dust); effects.particleMaterial = dust;
            string rainPath = Output + "/Materials/Rain streaks.mat";
            Material rain = AssetDatabase.LoadAssetAtPath<Material>(rainPath);
            if (rain == null) { rain = new Material(Shader.Find("Stillworks/Rain streaks")); AssetDatabase.CreateAsset(rain,rainPath); }
            rain.shader = Shader.Find("Stillworks/Rain streaks"); effects.rainMaterial = rain; EditorUtility.SetDirty(rain);
            string splashPath = Output + "/Materials/Rain splashes.mat";
            Material splashes = AssetDatabase.LoadAssetAtPath<Material>(splashPath);
            if (splashes == null) { splashes = new Material(Shader.Find("Stillworks/Rain splashes")); AssetDatabase.CreateAsset(splashes,splashPath); }
            splashes.shader = Shader.Find("Stillworks/Rain splashes"); effects.splashMaterial = splashes; EditorUtility.SetDirty(splashes);
            var session = new GameObject("Run / altitude / local timing").AddComponent<StillworksSession>();
            session.route = route; session.player = player; session.look = look;
            session.backgroundMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Stillworks/Audio/Last Wash.wav");
            session.laundry = GameObject.Find("Laundry to bring home");
            var narration = session.gameObject.AddComponent<StillworksNarration>(); narration.session = session;
            narration.dialogue = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Stillworks/Dialogue.json");
            narration.voiceClips = Directory.GetFiles("Assets/Stillworks/Audio/Narration","*.wav").OrderBy(p => p,StringComparer.Ordinal)
                .Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p.Replace('\\','/'))).ToArray();
            CreateDialogueTriggers(narration);
        }

        private static void CreateDialogueTriggers(StillworksNarration narration)
        {
            void Zone(string id, Vector3 position, Vector3 direction, float width)
            {
                var go = new GameObject("Narration location / " + id); go.layer = 2; go.transform.SetParent(root);
                go.transform.SetPositionAndRotation(position + Vector3.up * 3,Quaternion.LookRotation(direction));
                var sensor = go.AddComponent<BoxCollider>(); sensor.isTrigger = true; sensor.size = new Vector3(width + 16,6,5);
                var body = go.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
                var trigger = go.AddComponent<StillworksDialogueTrigger>(); trigger.narrator = narration; trigger.cueId = id;
            }
            string[] districts = { "01-heat", "02-parking", "03-works", "04-ministry", "05-relays", "06-crown" };
            for (int floor = 1; floor < 14; floor++)
            {
                int d = floor / 2;
                string id = floor % 2 == 0 ? districts[d - 1] : $"{21 + floor / 2:00}-level-{floor + 1:00}";
                Vector3 p = Node(floor,0), direction = Node(floor,1) - p; direction.y = 0;
                Zone(id,p,direction,Width[d]);
            }
            for (int floor = 0; floor < 14; floor++) for (int waypoint = 0; waypoint < 2; waypoint++)
            {
                int wing = waypoint == 0 ? 1 : 4, d = floor / 2;
                Vector3 a = Node(floor,wing), b = Node(floor,wing + 1); Vector3 direction = b - a; direction.y = 0;
                Vector3 p = Vector3.Lerp(a,b,.04f); p.y = a.y;
                Zone($"{28 + floor * 2 + waypoint:00}-gallery-{floor + 1:00}-{wing + 1:00}",p,direction,Width[d]);
            }
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
            CreateFilter();
        }

        private static void CreateFilter()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) throw new InvalidOperationException("Stillworks requires URP.");
            pipeline.supportsHDR = true; EditorUtility.SetDirty(pipeline);
            string path = Output + "/Stillworks atmosphere.asset";
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile,path); }
            T Effect<T>() where T : VolumeComponent
            {
                if (profile.TryGet<T>(out var effect)) return effect;
                effect = profile.Add<T>(true); AssetDatabase.AddObjectToAsset(effect,profile); return effect;
            }
            Effect<Tonemapping>().mode.Override(TonemappingMode.ACES);
            var color = Effect<ColorAdjustments>();
            color.postExposure.Override(.4f); color.contrast.Override(24); color.saturation.Override(4);
            color.colorFilter.Override(new Color(.88f,.97f,1f));
            var white = Effect<WhiteBalance>(); white.temperature.Override(-12); white.tint.Override(3);
            var tone = Effect<SplitToning>();
            tone.shadows.Override(new Color(.35f,.48f,.58f)); tone.highlights.Override(new Color(.62f,.56f,.43f)); tone.balance.Override(12);
            var bloom = Effect<Bloom>(); bloom.threshold.Override(1.1f); bloom.intensity.Override(.16f); bloom.scatter.Override(.55f);
            var grain = Effect<FilmGrain>(); grain.type.Override(FilmGrainLookup.Thin1); grain.intensity.Override(.11f); grain.response.Override(.8f);
            var vignette = Effect<Vignette>(); vignette.intensity.Override(.19f); vignette.smoothness.Override(.6f);
            Effect<ChromaticAberration>().intensity.Override(0);
            Effect<LensDistortion>().intensity.Override(0);
            EditorUtility.SetDirty(profile);
            Volume volume = new GameObject("Stillworks global atmosphere / cool concrete and warm sun").AddComponent<Volume>();
            volume.isGlobal = true; volume.priority = 10; volume.sharedProfile = profile;
            Camera camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            camera.allowHDR = true;
            var data = camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true; data.volumeLayerMask = 1;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;
            var speed = camera.GetComponent<PlayerSpeedFX>();
            if (speed != null)
            {
                var settings = new SerializedObject(speed);
                settings.FindProperty("speedVolume").objectReferenceValue = volume;
                settings.FindProperty("speedForMaximumFX").floatValue = 14;
                settings.FindProperty("maxVignette").floatValue = .06f;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void AddSpan(string label, Passage kind, Vector3 a, Vector3 b, float width, bool main = true)
        {
            Spans.Add(new RouteSpan(chunk.name + " / " + label, kind, a, b, width));
            if (main && (kind == Passage.Run || kind == Passage.Ramp || kind == Passage.Jump || kind == Passage.Slide || kind == Passage.Precision)) metres += Vector3.Distance(a, b);
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
            int samples = 0, jumps = 0, slides = 0, walls = 0;
            foreach (RouteSpan span in route.spans)
            {
                if (span.passage == Passage.WallRun)
                {
                    walls++;
                    Vector3 side = Vector3.Cross(Vector3.up,(span.to - span.from).normalized);
                    Vector3 centre = (span.from + span.to) / 2 + Vector3.up;
                    if (!Physics.Raycast(centre,side,.9f,1 << 6) && !Physics.Raycast(centre,-side,.9f,1 << 6))
                        failures.Add(span.name + ": no reachable wall beside crossing");
                    continue;
                }
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
            report.AppendLine($"Ground/headroom samples: {samples}; jump spans: {jumps}; slide openings: {slides}; wall crossings: {walls}");
            report.AppendLine($"Issues: {failures.Count}");
            foreach (string failure in failures) report.AppendLine(failure);
            Directory.CreateDirectory("Documentation"); File.WriteAllText("Documentation/Stillworks-validation.txt", report.ToString());
            if (failures.Count > 0) throw new InvalidOperationException(report.ToString());
            Debug.Log(report.ToString());
        }

        // Reproducible renders are useful in CI and do not require entering play mode.
        public static void Capture()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Camera camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            camera.transform.SetParent(null);
            string previewDirectory = "Documentation/Previews/Map-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(previewDirectory);
            var route = AssetDatabase.LoadAssetAtPath<StillworksRoute>(Output + "/StillworksRoute.asset");
            RouteSpan divider = route.spans.First(s => s.name.Contains("Jump over divider"));
            RouteSpan slide = route.spans.First(s => s.name.StartsWith("D2_") && s.name.Contains("Main gallery slide"));
            Vector3[] positions = { new Vector3(-650, 540, -760), Node(0,0) + Vector3.up * 1.62f, new Vector3(-340, 420, 280), new Vector3(46.5f,592,-38.5f),
                divider.from - (divider.to - divider.from).normalized * 5 + Vector3.up * 1.62f,
                slide.from - (slide.to - slide.from).normalized * 6 + Vector3.up * 1.62f };
            Vector3[] targets = { new Vector3(0,300,0), Node(0,1) + Vector3.up * 13, new Vector3(0,340,0), new Vector3(140,310,-80),
                divider.to + Vector3.up, slide.to + Vector3.up };
            string[] names = { "01-city-overview", "02-first-person-intake", "03-ministry-and-construction", "04-summit-view", "05-divider-obstacle", "06-industrial-slide" };
            for (int i = 0; i < positions.Length; i++)
            {
                camera.transform.position = positions[i]; camera.transform.LookAt(targets[i]); camera.fieldOfView = i == 0 ? 55 : 78;
                var target = new RenderTexture(1600, 1000, 24); camera.targetTexture = target; camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(1600,1000,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,1600,1000),0,0); image.Apply();
                File.WriteAllBytes(Path.Combine(previewDirectory, names[i] + ".png"), image.EncodeToPNG());
                camera.targetTexture = null; RenderTexture.active = null; UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(image);
            }
            Debug.Log("STILLWORKS: six previews captured.");
        }

        public static void BuildAndCapture()
        {
            Build();
            Capture();
        }

        public static void BakeAndBuildWindows()
        {
            Build();
            BuildWindows();
        }

        [MenuItem("Stillworks/Build Windows game")]
        public static void BuildWindows()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Validate();
            string destination = "Builds/Stillworks/Stillworks.exe";
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-buildOutput") destination = args[i + 1];
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            PlayerSettings.companyName = "Stillworks";
            PlayerSettings.productName = "The Stillworks";
            PlayerSettings.bundleVersion = "0.5.0";
            PlayerSettings.defaultScreenWidth = 1600; PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = destination, target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CompressWithLz4HC
            });
            File.WriteAllText("Documentation/Stillworks-build.txt", $"{result.summary.result}\nWindows x64\nSize: {result.summary.totalSize / 1048576f:0.0} MiB\nErrors: {result.summary.totalErrors}\n");
            if (result.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
                throw new InvalidOperationException("Windows player build failed. Inspect the Unity build log.");
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(destination), "READ ME.txt"),
                "THE STILLWORKS / THE LAST WASH — v0.5\nLaunch Stillworks.exe. Keep the executable beside its Data folder and UnityPlayer.dll.\n\nWASD move | Shift toggle sprint | Space jump | Ctrl slide | E take laundry | Esc pause\nHold Space while airborne beside a wall at speed to wall-run; release Space to wall-jump. Hold Ctrl while falling against a wall to wall-slide; press Space to kick away.\nClimb to the clothesline at 588 metres, collect the clothes, and bring them back to the starting shelter before 45 minutes of rain soak them.\nVaried pipes, raised inspection beds, offset loading shutters and toppled machinery join the fenced slide/jump lanes and precision fire escapes. Catch floors provide recovery.\nSettings offer master, music and rain volumes. Reduced motion disables camera lean and movement particles.\nUse Save & Return to Title to suspend a run. Pausing stops wetness. Saves and settings are local to this computer.\n56 original voiced narrator cues include physical level/gallery triggers and six rotating fall taunts. Weathered surface textures, flaking facade paint and layered city-wide rain complete the environment. The original ambient soundtrack requires no network.\n\nOriginal game environment. Built with Unity 6. Not affiliated with Only Up! or Vholume.\n");
        }
    }
}
