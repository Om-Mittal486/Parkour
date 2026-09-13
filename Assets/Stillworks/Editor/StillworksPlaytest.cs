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
        private readonly List<string> results = new List<string>();
        private PlayerMovement player;
        private CharacterController controller;
        private PlayerCamera look;
        private Keyboard keyboard;
        private bool failed;

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
            player = FindFirstObjectByType<PlayerMovement>();
            look = player.GetComponentInChildren<PlayerCamera>();
            controller = player.GetComponent<CharacterController>();
            keyboard = Keyboard.current ?? InputSystem.AddDevice<Keyboard>();
            StillworksRoute route = FindFirstObjectByType<StillworksSession>().route;
            yield return new WaitForSeconds(.3f);
            RouteSpan run = route.spans.First(s => s.passage == Passage.Run);
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
            float ground = player.transform.position.y;
            Keys(Key.W, Key.Space);
            yield return new WaitForSeconds(.3f);
            Check(player.transform.position.y > ground + .8f, "Space produces a real jump.");
            Keys(Key.W);
            yield return new WaitForSeconds(1.2f);
            Check(player.IsGrounded, "Controller lands on the architectural floor.");

            RouteSpan slide = route.spans.First(s => s.passage == Passage.Slide);
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

            // Test a representative real gap in each district that contains broken galleries.
            for (int d = 2; d <= 7; d++)
            {
                RouteSpan jump = route.spans.First(s => s.passage == Passage.Jump && s.name.StartsWith("D" + d + "_L"));
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
                    $"District {d} broken gallery: sprint jump clears {Vector3.Distance(jump.from,jump.to):0.0} m and lands.");
            }
            Keys();
            File.WriteAllLines("Documentation/Stillworks-controller-checks.txt", results);
            if (failed) Debug.LogError(string.Join("\n", results)); else Debug.Log(string.Join("\n", results));
            SessionState.SetBool("Stillworks.Playtest.Complete", true);
            SessionState.SetBool("Stillworks.Playtest.Failed", failed);
            EditorApplication.isPlaying = false;
        }
    }
}
