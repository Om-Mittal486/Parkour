#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Stillworks.Editor
{
    /// <summary>Short real-controller smoke checks driven through the existing Input Actions.</summary>
    [InitializeOnLoad]
    public sealed class StillworksPlaytest : MonoBehaviour
    {
        private const string Pending = "Stillworks.Playtest.Pending";
        private static readonly string PreviewDirectory = "Documentation/Previews/Game-" + System.DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        private readonly List<string> results = new List<string>();
        private PlayerMovement player;
        private CharacterController controller;
        private PlayerCamera look;
        private Keyboard keyboard;
        private InputSettings originalInputSettings;
        private bool failed;
        private bool previousBackground;

        static StillworksPlaytest()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool("Stillworks.Playtest.Complete", false))
                {
                    SessionState.SetBool("Stillworks.Playtest.Complete", false);
                    if (Application.isBatchMode) EditorApplication.Exit(SessionState.GetBool("Stillworks.Playtest.Failed", false) ? 1 : 0);
                    return;
                }
                if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
                SessionState.SetBool(Pending, false);
                new GameObject("Stillworks controller smoke checks").AddComponent<StillworksPlaytest>();
            };
        }

        [MenuItem("Stillworks/Run controller smoke checks")]
        public static void Begin()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene("Assets/Scenes/Stillworks.unity");
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        private void Keys(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); }

        private void Place(Vector3 p, Vector3 direction)
        {
            Keys();
            player.enabled = false;
            look.enabled = false;
            controller.enabled = false;
            player.transform.SetPositionAndRotation(p + Vector3.up * .08f, Quaternion.LookRotation(direction));
            look.transform.localRotation = Quaternion.identity;
            player.ResetMotion();
            controller.enabled = true;
            player.enabled = true;
        }

        private void Check(bool condition, string description)
        {
            failed |= !condition;
            results.Add((condition ? "PASS " : "FAIL ") + description);
        }

        private IEnumerator Start()
        {
            Time.captureDeltaTime = 1f / 60f;
            previousBackground = Application.runInBackground; Application.runInBackground = true;
            // Headless tests have no focused Game view. Use a temporary input configuration.
            originalInputSettings = InputSystem.settings;
            InputSystem.settings = Instantiate(originalInputSettings);
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            player = FindFirstObjectByType<PlayerMovement>();
            look = player.GetComponentInChildren<PlayerCamera>();
            controller = player.GetComponent<CharacterController>();
            keyboard = InputSystem.AddDevice<Keyboard>("Stillworks smoke keyboard");
            keyboard.MakeCurrent();
            StillworksSession session = FindFirstObjectByType<StillworksSession>();
            session.PersistenceEnabled = false;
            StillworksRoute route = session.route;
            yield return new WaitForSecondsRealtime(.3f);
            float[] startHeights = Physics.RaycastAll(route.spawn + Vector3.up * .25f,Vector3.down,1,1 << 6)
                .Select(h => h.point.y).OrderBy(h => h).ToArray();
            Check(startHeights.Zip(startHeights.Skip(1),(a,b) => Mathf.Abs(a - b) > .015f).All(separate => separate),
                "Starting shelter floors have distinct surface heights, removing coplanar ground flicker.");
            var concrete = AssetDatabase.LoadAssetAtPath<Material>("Assets/Stillworks/Generated/Materials/Boardformed concrete.mat");
            Check(concrete.GetTexture("_DetailTex") is Texture2D surface && surface.mipmapCount > 1 && surface.wrapMode == TextureWrapMode.Repeat,
                "Architecture uses imported seamless textures with mipmaps instead of aliasing shader noise.");
            Check(session.State == StillworksSession.ScreenState.Title, "Game opens on the title screen.");
            var rainSystem = FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).First(s => s.name.StartsWith("Rain around"));
            Check(rainSystem.particleCount > 30 && rainSystem.main.maxParticles == 3200,"Dense rain emits around the title camera with a bounded particle pool.");
            var weather = FindFirstObjectByType<StillworksEffects>();
            Check(weather.RainRadius >= 200 && FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Count(s => s.name.Contains("rain /")) == 3,
                "Near, middle, distant and city-wide rain cover the skyline and exposed architecture.");
            var sound = session.GetComponent<StillworksAudio>();
            Check(sound.MusicPlaying && session.backgroundMusic != null,"The original soundtrack is playing on the title screen.");
            Check(FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length == 1, "Exactly one UI EventSystem is active.");
            Capture("05-title-screen");
            VerifyRenderedRain(Camera.allCameras.First(c => c.enabled));
            VerifyRenderedFilter(Camera.allCameras.First(c => c.enabled));
            UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            yield return new WaitForSeconds(.2f);
            Check(sound.ClicksPlayed > 0,"The title button produces audible click feedback.");
            RouteSpan run = route.spans.First(s => s.name.Contains("Upper terrace sprint") && Vector3.Distance(s.from,s.to) > 30);
            Vector3 direction = (run.to - run.from).normalized;
            Place(run.from + direction * 3, direction);
            yield return new WaitForSeconds(.2f);
            Vector3 initial = player.transform.position;
            Keys(Key.W, Key.LeftShift);
            yield return new WaitForSeconds(.2f);
            Keys(Key.W);
            yield return new WaitForSeconds(.8f);
            Check(player.CurrentSpeed > 13, $"Sprint reaches serialized 14 m/s ({player.CurrentSpeed:0.00}).");
            Check(Vector3.Distance(initial, player.transform.position) > 9, "Player actually advances along the intake floor.");
            float pausedTime = session.Elapsed;
            session.Pause();
            yield return new WaitForSecondsRealtime(.15f);
            Check(Mathf.Approximately(session.Elapsed, pausedTime) && !player.enabled, "Pause freezes timer and movement.");
            Capture("06-pause-screen");
            session.OpenSettings();
            yield return new WaitForSecondsRealtime(.1f);
            Capture("07-settings-screen");
            session.CloseSettings();
            Check(session.State == StillworksSession.ScreenState.Paused, "Settings return to the paused run.");
            session.Resume();

            const string saveKey = "Stillworks.SuspendedRun.v1";
            string oldSave = PlayerPrefs.HasKey(saveKey) ? PlayerPrefs.GetString(saveKey) : null;
            try
            {
                session.PersistenceEnabled = true;
                Vector3 savedPosition = player.transform.position;
                session.SaveRun(); session.ReturnToTitle();
                Check(session.HasSavedRun, "A suspended run can be read back.");
                session.ContinueRun();
                Check(Vector3.Distance(player.transform.position,savedPosition) < 1 && Mathf.Approximately(session.Elapsed,pausedTime), "Continue restores location and elapsed time.");
            }
            finally
            {
                if (oldSave == null) PlayerPrefs.DeleteKey(saveKey); else PlayerPrefs.SetString(saveKey,oldSave);
                PlayerPrefs.Save(); session.PersistenceEnabled = false;
            }
            // Continue enables mouse look; return the smoke driver to its fixed test heading.
            Place(run.from + direction * 3,direction);
            yield return new WaitForSeconds(.2f);
            Keys(Key.W);
            yield return new WaitForSeconds(.1f);
            float ground = player.transform.position.y;
            Keys(Key.W, Key.Space);
            yield return new WaitForSeconds(.3f);
            Check(player.transform.position.y > ground + .8f, "Space produces a real jump.");
            Keys(Key.W);
            yield return new WaitForSeconds(1.2f);
            Check(player.IsGrounded, "Controller lands on the architectural floor.");
            Capture("08-in-game-hud");

            RouteSpan slide = route.spans.First(s => s.name.Contains("Main gallery slide"));
            direction = (slide.to - slide.from).normalized;
            Place(slide.from - direction * 8, direction);
            yield return new WaitForSeconds(.2f);
            Keys(Key.W, Key.LeftShift);
            yield return new WaitForSeconds(.15f);
            Keys(Key.W);
            yield return new WaitForSeconds(.55f);
            Keys(Key.W, Key.LeftCtrl);
            yield return new WaitForSeconds(.08f);
            Check(player.IsSliding && controller.height < 1.1f, "Ctrl enters the 1 m slide capsule.");
            Keys(Key.W);
            yield return new WaitForSeconds(1.7f);
            Check(Vector3.Dot(player.transform.position - slide.to, direction) > 0, "Player exits the 1.25 m loading opening.");
            Check(controller.height > 1.7f && !player.IsSliding, "Standing height is restored after clearance.");
            RouteSpan guardedSlide = route.spans.First(s => s.name.Contains("Main gallery slide"));
            direction = (guardedSlide.to - guardedSlide.from).normalized;
            Vector3 side = Vector3.Cross(Vector3.up,direction);
            Vector3 gate = (guardedSlide.from + guardedSlide.to) / 2;
            Check(Physics.Raycast(gate + Vector3.up,side,guardedSlide.width / 2 + .8f,1 << 6) &&
                Physics.Raycast(gate + Vector3.up,-side,guardedSlide.width / 2 + .8f,1 << 6),"Guard fences close both sides of the required slide lane.");

            foreach (RouteSpan precision in route.spans.Where(s => s.passage == Passage.Precision).Take(2))
            {
                direction = (precision.to - precision.from).normalized;
                Place(precision.from + direction,direction); yield return new WaitForSeconds(.2f); Keys(Key.W);
                float deadline = Time.time + 8;
                while (Vector3.Dot(precision.to - player.transform.position,direction) > .7f && Time.time < deadline) yield return null;
                Keys(); yield return new WaitForSeconds(.1f);
                Check(player.transform.position.y >= precision.to.y - .3f && player.IsGrounded,
                    "Real controller crosses the 65 cm transfer girder: " + precision.name);
                Capture("14-precision-transfer");
            }
            RouteSpan narrowJump = route.spans.First(s => s.passage == Passage.Jump && s.width < 1 && s.name.Contains("Shortcut gap"));
            direction = (narrowJump.to - narrowJump.from).normalized;
            Place(narrowJump.from - direction * 4,direction); yield return new WaitForSeconds(.2f);
            Keys(Key.W,Key.LeftShift); yield return new WaitForSeconds(.15f); Keys(Key.W);
            float narrowDeadline = Time.time + 3;
            while (Vector3.Dot(narrowJump.from - player.transform.position,direction) > .6f && Time.time < narrowDeadline) yield return null;
            // Keep takeoff momentum, then lower the air target speed to fit the short landing.
            Keys(Key.W,Key.Space,Key.LeftShift); yield return new WaitForSeconds(.1f); Keys(Key.W);
            yield return new WaitForSeconds(.9f); Keys(); yield return new WaitForSeconds(.2f);
            Check(Vector3.Dot(player.transform.position - narrowJump.to,direction) > 0 && player.IsGrounded && player.transform.position.y >= narrowJump.to.y - .3f,
                "A controlled sprint jump clears 6.4 m onto the 85 cm fire-escape landing.");

            foreach (Passage kind in new[] { Passage.Ramp, Passage.Recovery })
            {
                RouteSpan climb = route.spans.First(s => s.passage == kind && s.to.y > s.from.y + 4);
                direction = climb.to - climb.from; direction.y = 0; direction.Normalize();
                Place(climb.from, direction);
                yield return new WaitForSeconds(.2f);
                Keys(Key.W);
                float deadline = Time.time + 12;
                while (Vector3.Dot(climb.to - player.transform.position, direction) > 1 && Time.time < deadline) yield return null;
                Keys();
                yield return new WaitForSeconds(.3f);
                Check(player.transform.position.y > climb.to.y - .5f && player.IsGrounded, kind + " ascent reaches its upper landing with the real controller.");
            }

            // Test a representative real gap in each district that contains broken galleries.
            for (int d = 2; d <= 7; d++)
            {
                RouteSpan jump = route.spans.First(s => s.passage == Passage.Jump && s.name.StartsWith("D" + d + "_L") &&
                    s.name.Contains("Broken expansion joint") && !route.spans.Any(w => w.name.StartsWith(s.name.Split('/')[0]) && w.name.Contains("Weave")));
                direction = (jump.to - jump.from).normalized;
                Place(jump.from - direction * 10, direction);
                yield return new WaitForSeconds(.2f);
                Keys(Key.W, Key.LeftShift);
                yield return new WaitForSeconds(.15f);
                Keys(Key.W);
                float timeout = Time.time + 3;
                while (Vector3.Dot(jump.from - player.transform.position, direction) > 1.3f && Time.time < timeout) yield return null;
                Keys(Key.W, Key.Space);
                yield return new WaitForSeconds(.1f);
                Keys(Key.W);
                yield return new WaitForSeconds(1.0f);
                Check(Vector3.Dot(player.transform.position - jump.to, direction) > 0 && player.transform.position.y > jump.to.y - .3f && player.IsGrounded,
                    $"District {d} broken gallery: sprint jump clears {Vector3.Distance(jump.from,jump.to):0.0} m and lands. Position {player.transform.position}; landing {jump.to}; grounded {player.IsGrounded}.");
            }
            for (int d = 1; d <= 7; d++)
            {
                RouteSpan divider = route.spans.First(s => s.name.StartsWith("D" + d + "_L") && s.name.Contains("Jump over divider"));
                direction = (divider.to - divider.from).normalized;
                Place(divider.from - direction * 10, direction);
                yield return new WaitForSeconds(.2f);
                Keys(Key.W,Key.LeftShift); yield return new WaitForSeconds(.15f); Keys(Key.W);
                float deadline = Time.time + 3;
                while (Vector3.Dot(divider.from - player.transform.position,direction) > 1.3f && Time.time < deadline) yield return null;
                Keys(Key.W,Key.Space); yield return new WaitForSeconds(.1f); Keys(Key.W);
                yield return new WaitForSeconds(.85f);
                Check(Vector3.Dot(player.transform.position - divider.to,direction) > 0 && player.transform.position.y > divider.to.y - .3f,
                    $"District {d}: real sprint jump clears the concrete/girder obstacle.");
            }
            foreach (string subtype in new[] { "pipe bundle", "toppled cabinets", "abandoned shuttering" })
            {
                RouteSpan obstacle = route.spans.First(s => s.passage == Passage.Jump && s.name.Contains(subtype));
                direction = (obstacle.to - obstacle.from).normalized;
                Place(obstacle.from - direction * 7,direction); yield return new WaitForSeconds(.2f);
                Keys(Key.W,Key.LeftShift); yield return new WaitForSeconds(.15f); Keys(Key.W);
                float deadline = Time.time + 3;
                while (Vector3.Dot(obstacle.from - player.transform.position,direction) > 1.3f && Time.time < deadline) yield return null;
                Keys(Key.W,Key.Space); yield return new WaitForSeconds(.1f); Keys(Key.W); yield return new WaitForSeconds(.9f);
                Check(Vector3.Dot(player.transform.position - obstacle.to,direction) > 0 && player.IsGrounded && player.transform.position.y > obstacle.to.y - .3f,
                    "Real controller jumps the new " + subtype + " obstacle.");
            }
            RouteSpan inspection = route.spans.First(s => s.name.Contains("Jump onto inspection bed"));
            direction = inspection.to - inspection.from; direction.y = 0; direction.Normalize();
            Place(inspection.from - direction * 2,direction); yield return new WaitForSeconds(.2f);
            Keys(Key.W); yield return new WaitForSeconds(.3f); Keys(Key.W,Key.Space); yield return new WaitForSeconds(.1f);
            Keys(Key.W); yield return new WaitForSeconds(.42f); Keys(); yield return new WaitForSeconds(.6f);
            Check(player.IsGrounded && Mathf.Abs(player.transform.position.y - inspection.to.y) < .25f,"A controlled jump lands on the raised cable inspection bed.");
            Capture("15-raised-inspection-bed");
            RouteSpan weave = route.spans.First(s => s.name.Contains("Weave through infrastructure"));
            string wingPrefix = weave.name.Split(new[] { " / " },System.StringSplitOptions.None)[0];
            RouteSpan[] bends = route.spans.Where(s => s.name.StartsWith(wingPrefix + " / Weave")).ToArray();
            Place(bends[0].from,(bends[0].to - bends[0].from).normalized);
            yield return new WaitForSeconds(.2f);
            Keys(Key.W);
            foreach (RouteSpan bend in bends)
            {
                float deadline = Time.time + 10;
                while (Vector3.Distance(player.transform.position,bend.to) > 1 && Time.time < deadline)
                {
                    Vector3 toward = bend.to - player.transform.position; toward.y = 0;
                    player.transform.rotation = Quaternion.LookRotation(toward);
                    yield return null;
                }
                Check(Vector3.Distance(player.transform.position,bend.to) < 1.5f,"Real controller traverses " + bend.name + ".");
            }
            RouteSpan door = route.spans.First(s => s.name.Contains("Weave through offset loading door"));
            wingPrefix = door.name.Split(new[] { " / " },System.StringSplitOptions.None)[0];
            bends = route.spans.Where(s => s.name.StartsWith(wingPrefix + " / Weave")).ToArray();
            Place(bends[0].from,(bends[0].to - bends[0].from).normalized); yield return new WaitForSeconds(.2f); Keys(Key.W);
            foreach (RouteSpan bend in bends)
            {
                float deadline = Time.time + 10;
                while (Vector3.Distance(player.transform.position,bend.to) > 1 && Time.time < deadline)
                {
                    Vector3 toward = bend.to - player.transform.position; toward.y = 0;
                    player.transform.rotation = Quaternion.LookRotation(toward); yield return null;
                }
                Check(Vector3.Distance(player.transform.position,bend.to) < 1.5f,"Real controller threads " + bend.name + ".");
            }
            Capture("16-offset-loading-shutters");
            // An isolated vertical wall exercises traversal without a floor or another route hiding a failure.
            var testWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            testWall.name = "Wall traversal smoke surface"; testWall.layer = 6;
            testWall.transform.position = new Vector3(900.8f,95,920);
            testWall.transform.localScale = new Vector3(.5f,30,60);
            foreach (float heading in new[] { 1f,-1f })
            {
                Place(new Vector3(900,100,920),Vector3.forward * heading);
                yield return new WaitForSeconds(.1f);
                Keys(Key.W,Key.LeftShift); yield return new WaitForSeconds(.15f); Keys(Key.W);
                yield return new WaitForSeconds(.8f);
                Check(!player.IsWallRunning,"Wall-running requires holding Space.");
                Keys(Key.W,Key.Space); yield return new WaitForSeconds(.2f);
                Check(player.IsWallRunning && player.CurrentSpeed >= 8, "Holding Space engages " + (heading > 0 ? "right" : "left") + " wall-running.");
                look.Configure(.12f,false,false); look.ResetView(player.transform.eulerAngles.y); look.enabled = true;
                float wallHeight = player.transform.position.y;
                yield return new WaitForSeconds(.4f);
                Check(wallHeight - player.transform.position.y < 1, "Wall-running slows the fall while preserving forward travel.");
                Check(Mathf.DeltaAngle(0,look.transform.localEulerAngles.z) * player.WallSide > 1,"Wall camera roll uses the corrected side on " + (heading > 0 ? "right" : "left") + " walls.");
                look.enabled = false;
                Keys(Key.W); yield return new WaitForSeconds(.15f);
                Check(!player.IsWallRunning && player.transform.position.x < 899.4f && player.transform.position.y > wallHeight,
                    "Releasing Space kicks away from the wall and gains height.");
            }
            Place(new Vector3(900,100,920),Vector3.forward);
            yield return new WaitForSeconds(.1f); Keys(Key.W,Key.LeftShift);
            yield return new WaitForSeconds(.15f); Keys(Key.W,Key.Space); yield return new WaitForSeconds(3.1f);
            Check(!player.IsWallRunning, "Wall-run duration expires; the same wall cannot grant unlimited hovering.");
            Place(new Vector3(900,100,920),Vector3.right);
            yield return new WaitForSeconds(.1f); Keys(Key.LeftCtrl); yield return new WaitForSeconds(.3f);
            Check(player.IsWallSliding, "Holding Ctrl while falling against a front wall engages wall-sliding.");
            float slideHeight = player.transform.position.y;
            yield return new WaitForSeconds(.8f);
            Check(slideHeight - player.transform.position.y < 3.2f, "Wall-slide caps descent at 3.5 m/s.");
            Keys(Key.LeftCtrl,Key.Space); yield return new WaitForSeconds(.2f); Keys();
            Check(player.transform.position.x < 899 && !player.IsWallSliding, "Space exits a wall-slide with an outward jump.");
            Destroy(testWall);
            RouteSpan facade = route.spans.First(s => s.passage == Passage.WallRun && s.name.StartsWith("D5_"));
            direction = (facade.to - facade.from).normalized;
            Place(facade.from - direction * 8,direction);
            yield return new WaitForSeconds(.2f);
            Keys(Key.W,Key.LeftShift); yield return new WaitForSeconds(.15f); Keys(Key.W);
            float facadeDeadline = Time.time + 3;
            while (Vector3.Dot(facade.from - player.transform.position,direction) > .8f && Time.time < facadeDeadline) yield return null;
            Keys(Key.W,Key.Space); yield return new WaitForSeconds(.12f);
            yield return new WaitForSeconds(.25f);
            Check(player.IsWallRunning,$"The Ministry facade engages wall-running across an actual broken gallery. Grounded {player.IsGrounded}, speed {player.CurrentSpeed:0.0}, position {player.transform.position}.");
            yield return new WaitForSeconds(.45f); Keys();
            Check(Vector3.Dot(player.transform.position - facade.to,direction) > 0 && player.transform.position.y > facade.to.y,
                "Real facade wall-run crosses the broken gallery and reaches the upper landing.");
            var volume = FindFirstObjectByType<UnityEngine.Rendering.Volume>();
            Check(volume != null && volume.isGlobal && volume.sharedProfile.Has<UnityEngine.Rendering.Universal.ColorAdjustments>() &&
                player.GetComponentInChildren<Camera>().GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing,
                "Global atmosphere profile and gameplay camera post-processing are active.");
            var fallNarrator = session.GetComponent<StillworksNarration>();
            fallNarrator.ResetStory(1);
            var secondLevel = FindObjectsByType<StillworksDialogueTrigger>(FindObjectsSortMode.None).First(t => t.cueId == "21-level-02");
            direction = secondLevel.transform.forward;
            Vector3 sensorFloor = secondLevel.transform.position - Vector3.up * 3;
            Place(sensorFloor - direction * 5,direction); yield return new WaitForSeconds(.2f); Keys(Key.W); yield return new WaitForSeconds(.9f); Keys();
            yield return new WaitForSecondsRealtime(.3f);
            Check(fallNarrator.LocationTriggers == 1 && fallNarrator.Subtitle.Contains("Second level"),"Entering the second-level zone triggers its specific voiced dialogue.");
            Place(sensorFloor - direction * 5,direction); yield return new WaitForSeconds(.2f); Keys(Key.W); yield return new WaitForSeconds(.9f); Keys();
            Check(fallNarrator.LocationTriggers == 1,"Re-entering a visited level does not repeat its story line.");
            fallNarrator.ResetStory(1);
            var finalGallery = FindObjectsByType<StillworksDialogueTrigger>(FindObjectsSortMode.None).First(t => t.cueId == "55-gallery-14-05");
            direction = finalGallery.transform.forward; sensorFloor = finalGallery.transform.position - Vector3.up * 3;
            Place(sensorFloor - direction * 5,direction); yield return new WaitForSeconds(.2f); Keys(Key.W); yield return new WaitForSeconds(.9f); Keys();
            yield return new WaitForSecondsRealtime(.3f);
            Check((fallNarrator.PlayedMask & (1L << 55)) != 0,"A high-index gallery cue triggers independently without overflowing the story save mask.");
            fallNarrator.ResetStory();
            direction = (run.to - run.from).normalized;
            Place(run.from + direction * 3 + Vector3.up * 8,direction);
            yield return new WaitForSeconds(1.4f);
            Check(fallNarrator.FallTauntsPlayed == 1,"An actual eight-metre fall triggers a mocking narrator line.");
            string firstTaunt = fallNarrator.Subtitle;
            yield return new WaitForSecondsRealtime(6.2f);
            Place(run.from + direction * 3 + Vector3.up * 8,direction); yield return new WaitForSeconds(1.4f);
            Check(fallNarrator.FallTauntsPlayed == 2 && fallNarrator.Subtitle != firstTaunt,$"A subsequent fall plays a different taunt rather than exhausting a one-time cue (count {fallNarrator.FallTauntsPlayed}).");
            Keys();
            Place(route.summit, Vector3.forward);
            yield return new WaitForSecondsRealtime(.3f);
            Check(!session.Finished && session.CanCollect,"The Crown offers laundry collection rather than ending the errand.");
            Capture("09-laundry-at-crown");
            Keys(Key.E); yield return new WaitForSeconds(.15f); Keys();
            Check(session.ClothesCollected && !session.laundry.activeSelf,"E collects the clothes and removes them from the line.");
            var narration = session.GetComponent<StillworksNarration>();
            Check(narration.voiceClips.Length == 56 && narration.voiceClips.All(c => c != null),"All 56 narrator clips, including location cues and six fall taunts, are connected.");
            long played = narration.PlayedMask; narration.Trigger("collected");
            Check(played == narration.PlayedMask && narration.Subtitle.Contains("Laundry secured"),"The collection dialogue fires once with subtitles.");
            session.Pause();
            float wetness = session.Wetness; yield return new WaitForSecondsRealtime(.2f);
            Check(Mathf.Approximately(wetness,session.Wetness),"Pausing stops the wetness clock."); session.Resume();
            Place(route.spawn,Vector3.forward); yield return new WaitForSecondsRealtime(.3f);
            Check(session.Finished && session.State == StillworksSession.ScreenState.Finished,"Returning the clothes to the street shelter completes the errand.");
            Capture("10-laundry-delivered");
            session.NewRun();
            Check(!session.Finished && !session.ClothesCollected && session.laundry.activeSelf && session.Elapsed == 0 &&
                Vector3.Distance(player.transform.position, route.spawn) < .2f, "Restart resets laundry, narration and the street start.");
            session.soakSeconds = 60;
            Place(run.from + (run.to - run.from).normalized * 3,(run.to - run.from).normalized);
            yield return new WaitForSeconds(.2f); Keys(Key.W); yield return new WaitForSeconds(.3f); Keys();
            // A late suspended run checks expiration without requiring a real 45-minute wait.
            oldSave = PlayerPrefs.HasKey(saveKey) ? PlayerPrefs.GetString(saveKey) : null;
            try
            {
                session.PersistenceEnabled = true;
                long persistedMask = (1L << 55) | 256;
                PlayerPrefs.SetString(saveKey,"{\"version\":1,\"position\":{\"x\":" + player.transform.position.x.ToString(System.Globalization.CultureInfo.InvariantCulture) +
                    ",\"y\":" + player.transform.position.y.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"z\":" +
                    player.transform.position.z.ToString(System.Globalization.CultureInfo.InvariantCulture) + "},\"heading\":0,\"elapsed\":59.9,\"falls\":0,\"clothesCollected\":true,\"narrationMask\":" + persistedMask + "}");
                session.ContinueRun();
                Check(session.ClothesCollected && narration.PlayedMask == persistedMask,"Continue preserves collected laundry and the expanded 64-bit dialogue history.");
                session.PersistenceEnabled = false;
                yield return new WaitForSecondsRealtime(.4f);
                Check(session.State == StillworksSession.ScreenState.Failed && session.Wetness >= 1,"The deadline produces the soaked-laundry failure screen.");
                Capture("11-soaked-laundry");
            }
            finally
            {
                if (oldSave == null) PlayerPrefs.DeleteKey(saveKey); else PlayerPrefs.SetString(saveKey,oldSave);
                PlayerPrefs.Save(); session.PersistenceEnabled = false;
            }
            File.WriteAllLines("Documentation/Stillworks-controller-checks.txt", results);
            if (failed) Debug.LogError(string.Join("\n", results)); else Debug.Log(string.Join("\n", results));
            SessionState.SetBool("Stillworks.Playtest.Complete", true);
            SessionState.SetBool("Stillworks.Playtest.Failed", failed);
            EditorApplication.isPlaying = false;
        }

        private void OnDestroy()
        {
            Time.captureDeltaTime = 0;
            Application.runInBackground = previousBackground;
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (originalInputSettings != null)
            {
                InputSettings temporary = InputSystem.settings;
                InputSystem.settings = originalInputSettings;
                Destroy(temporary);
            }
        }

        private void VerifyRenderedRain(Camera camera)
        {
            var data = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            int mask = camera.cullingMask; CameraClearFlags clear = camera.clearFlags; Color background = camera.backgroundColor;
            var target = new RenderTexture(640,360,24); var texture = new Texture2D(640,360,TextureFormat.RGB24,false);
            camera.targetTexture = target; camera.cullingMask = 0; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.1f,.1f,.1f); data.renderPostProcessing = false;
            camera.Render(); RenderTexture.active = target; texture.ReadPixels(new Rect(0,0,640,360),0,0); texture.Apply();
            Color32[] blank = texture.GetPixels32();
            camera.cullingMask = 1 << 2; camera.Render(); texture.ReadPixels(new Rect(0,0,640,360),0,0); texture.Apply();
            Color32[] weather = texture.GetPixels32(); int changed = 0;
            for (int i = 0; i < blank.Length; i++)
                if (Mathf.Abs(blank[i].r - weather[i].r) + Mathf.Abs(blank[i].g - weather[i].g) + Mathf.Abs(blank[i].b - weather[i].b) > 20) changed++;
            Check(changed > 80,$"Rain actually draws visible streaks in the camera image ({changed} pixels).");
            camera.targetTexture = null; camera.cullingMask = mask; camera.clearFlags = clear; camera.backgroundColor = background;
            data.renderPostProcessing = true; RenderTexture.active = null; Destroy(texture); Destroy(target);
        }

        private void VerifyRenderedFilter(Camera camera)
        {
            var data = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var target = new RenderTexture(640,360,24);
            var texture = new Texture2D(640,360,TextureFormat.RGB24,false);
            camera.targetTexture = target;
            data.renderPostProcessing = false; camera.Render(); RenderTexture.active = target;
            texture.ReadPixels(new Rect(0,0,640,360),0,0); texture.Apply(); Color32[] plain = texture.GetPixels32();
            data.renderPostProcessing = true; camera.Render();
            texture.ReadPixels(new Rect(0,0,640,360),0,0); texture.Apply(); Color32[] graded = texture.GetPixels32();
            long delta = 0;
            for (int i = 0; i < plain.Length; i++) delta += Mathf.Abs(plain[i].r - graded[i].r) + Mathf.Abs(plain[i].g - graded[i].g) + Mathf.Abs(plain[i].b - graded[i].b);
            float average = (float)delta / (plain.Length * 3);
            Check(average > 3,$"The atmospheric filter changes the rendered image (average channel difference {average:0.0}/255).");
            camera.targetTexture = null; RenderTexture.active = null; Destroy(texture); Destroy(target);
        }

        private static void Capture(string name)
        {
            Camera camera = Camera.allCameras.First(c => c.enabled);
            Canvas canvas = FindFirstObjectByType<Canvas>();
            var target = new RenderTexture(1600,900,24);
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            var image = new Texture2D(1600,900,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1600,900),0,0); image.Apply();
            Directory.CreateDirectory(PreviewDirectory);
            File.WriteAllBytes(PreviewDirectory + "/" + name + ".png",image.EncodeToPNG());
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.worldCamera = null;
            camera.targetTexture = null; RenderTexture.active = null;
            Destroy(image); Destroy(target);
        }
    }
}
#endif
