using UnityEngine;
using UnityEngine.InputSystem;

namespace Stillworks
{
    /// <summary>Local run timing and unobtrusive orientation. No checkpoint teleportation.</summary>
    public sealed class StillworksSession : MonoBehaviour
    {
        public StillworksRoute route;
        public PlayerMovement player;
        public PlayerCamera look;
        private float elapsed;
        private bool started, finished, paused;
        private int falls;
        private GUIStyle small, title, number, body, button;
        private Texture2D pixel;
        private float best;

        private void Start()
        {
            best = PlayerPrefs.GetFloat("Stillworks.BestSeconds", 0f);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetPaused(!paused);
            if (paused) return;
            if (player.CurrentSpeed > 0.5f) started = true;
            if (started && !finished) elapsed += Time.deltaTime;
            if (player.transform.position.y < -35f)
            {
                falls++;
                Respawn();
            }
            if (!finished && Vector3.Distance(player.transform.position, route.summit) < 9f)
            {
                finished = true;
                if (best <= 0f || elapsed < best)
                {
                    best = elapsed;
                    PlayerPrefs.SetFloat("Stillworks.BestSeconds", best);
                    PlayerPrefs.Save();
                }
            }
        }

        private void Respawn()
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = route.spawn;
            player.ResetMotion();
            controller.enabled = true;
        }

        private void Restart()
        {
            elapsed = 0f;
            falls = 0;
            started = finished = false;
            Respawn();
            SetPaused(false);
        }

        private void SetPaused(bool value)
        {
            paused = value;
            player.enabled = !value;
            look.enabled = !value;
            Cursor.lockState = value ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = value;
        }

        private static string Clock(float seconds)
        {
            int whole = Mathf.FloorToInt(seconds);
            return $"{whole / 60:00}:{whole % 60:00}";
        }

        private void Styles()
        {
            if (small != null) return;
            pixel = Texture2D.whiteTexture;
            small = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold };
            small.normal.textColor = new Color(0.67f, 0.72f, 0.70f);
            title = new GUIStyle(small) { fontSize = 26 };
            title.normal.textColor = new Color(0.91f, 0.90f, 0.83f);
            number = new GUIStyle(title) { fontSize = 23, alignment = TextAnchor.UpperRight };
            body = new GUIStyle(small) { fontSize = 14, fontStyle = FontStyle.Normal, wordWrap = true };
            body.normal.textColor = new Color(0.82f, 0.83f, 0.79f);
            button = new GUIStyle(GUI.skin.button) { fontSize = 14, fixedHeight = 42 };
        }

        private void Panel(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = Color.white;
        }

        private void OnGUI()
        {
            if (route == null || player == null) return;
            Styles();
            float scale = Mathf.Clamp(Screen.height / 900f, 0.65f, 2f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float w = Screen.width / scale, h = Screen.height / scale;
            float altitude = Mathf.Max(0, player.transform.position.y);
            int district = Mathf.Clamp((int)(altitude / 84f), 0, 6);
            Panel(new Rect(30, 30, 3, 46), new Color(0.69f, 0.39f, 0.23f));
            GUI.Label(new Rect(46, 28, 320, 22), "THE STILLWORKS / " + (district + 1).ToString("00"), small);
            GUI.Label(new Rect(45, 47, 370, 35), route.districts[district].ToUpperInvariant(), title);
            GUI.Label(new Rect(w - 230, 29, 195, 32), Clock(elapsed), number);
            GUI.Label(new Rect(w - 230, 61, 195, 24), $"{altitude:000} M  /  {route.summit.y:000} M", new GUIStyle(small) { alignment = TextAnchor.UpperRight });
            Panel(new Rect(w - 38, 106, 2, 190), new Color(0.33f, 0.38f, 0.37f, 0.5f));
            float progress = Mathf.Clamp01(altitude / route.summit.y);
            Panel(new Rect(w - 39, 296 - 190 * progress, 4, 190 * progress), new Color(0.76f, 0.65f, 0.44f));
            GUI.Label(new Rect(32, h - 38, 400, 24), "ESC  PAUSE    /    NO SIGNAL • LOCAL TIMER", small);
            Panel(new Rect(w / 2 - 1, h / 2 - 1, 2, 2), new Color(0.88f, 0.87f, 0.81f, 0.6f));
            if (elapsed < 15f && !finished && !paused)
            {
                Panel(new Rect(32, h - 161, 445, 106), new Color(0.055f, 0.075f, 0.077f, 0.88f));
                GUI.Label(new Rect(49, h - 148, 410, 26), "THE CROWN IS STILL TRANSMITTING.", small);
                GUI.Label(new Rect(49, h - 120, 405, 59), "Find a way up.\nWASD move · Shift toggle sprint · Space jump\nCtrl slide · Lower roofs can catch your fall.", body);
            }
            if (finished && !paused)
            {
                Panel(new Rect(w / 2 - 240, h - 170, 480, 112), new Color(0.05f, 0.07f, 0.075f, 0.92f));
                GUI.Label(new Rect(w / 2 - 220, h - 157, 440, 32), "END OF TRANSMISSION", title);
                GUI.Label(new Rect(w / 2 - 220, h - 116, 440, 50), $"Summit reached / {Clock(elapsed)}    Best / {Clock(best)}\nThe entire Stillworks is beneath you. ESC for a new run.", body);
            }
            if (paused)
            {
                Panel(new Rect(0, 0, w, h), new Color(0.025f, 0.037f, 0.04f, 0.87f));
                float x = w / 2 - 220, y = h / 2 - 190;
                GUI.Label(new Rect(x, y, 440, 45), "THE STILLWORKS", title);
                GUI.Label(new Rect(x, y + 49, 440, 82), $"{Clock(elapsed)} elapsed / {altitude:0} m climbed\n{falls} returns to ground / Best { (best > 0 ? Clock(best) : "—") }\nWASD · Shift sprint · Space jump · Ctrl slide", body);
                if (GUI.Button(new Rect(x, y + 151, 440, 42), "CONTINUE ASCENT", button)) SetPaused(false);
                if (GUI.Button(new Rect(x, y + 207, 440, 42), "RESTART FROM THE STREET", button)) Restart();
                GUI.Label(new Rect(x, y + 268, 440, 80), "Look for maintenance ramps, broken spans and service galleries. Rusted beams offer faster, narrower connections. Falls usually lead back to lower architecture.", body);
            }
            GUI.matrix = previous;
        }
    }
}
