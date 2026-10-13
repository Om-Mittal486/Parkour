using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

namespace Stillworks
{
    public sealed class StillworksSession : MonoBehaviour
    {
        public enum ScreenState { Title, Playing, Paused, Settings, Finished, Failed }
        public StillworksRoute route;
        public PlayerMovement player;
        public PlayerCamera look;
        public bool PersistenceEnabled { get; set; } = true;
        public ScreenState State { get; private set; }
        public float Elapsed { get; private set; }
        public float Best { get; private set; }
        public int Falls { get; private set; }
        public bool Finished { get; private set; }
        public bool ClothesCollected { get; private set; }
        public GameObject laundry;
        public AudioClip backgroundMusic;
        public float soakSeconds = 2700;
        public float Wetness => Mathf.Clamp01(Elapsed / Mathf.Max(60,soakSeconds));
        public bool CanCollect => !ClothesCollected && Vector3.Distance(player.transform.position,route.summit) < 9;
        public bool HasSavedRun => ReadSave() != null;
        public float Sensitivity = .12f, FieldOfView = 85, Volume = .8f;
        public float MusicVolume = .55f, RainVolume = .2f;
        public bool ReducedMotion, InvertY, Fullscreen = true, Economy;
        private const string SaveKey = "Stillworks.SuspendedRun.v1";
        private Camera gameCamera, titleCamera;
        private StillworksUI ui;
        private StillworksAudio sound;
        private PlayerSpeedFX speedFX;
        private StillworksNarration narration;
        private ScreenState settingsReturn;
        private Vector3 safePosition;
        private float safeHeading, saveTimer;
        private bool started;

        [Serializable]
        private sealed class RunSave
        {
            public int version = 1;
            public Vector3 position;
            public float heading, elapsed;
            public int falls;
            public bool clothesCollected;
            public long narrationMask;
        }

        private void Start()
        {
            Best = PlayerPrefs.GetFloat("Stillworks.LaundryBestSeconds", 0);
            Sensitivity = PlayerPrefs.GetFloat("Stillworks.Sensitivity", .12f);
            FieldOfView = PlayerPrefs.GetFloat("Stillworks.FOV", 85);
            Volume = PlayerPrefs.GetFloat("Stillworks.Volume", .8f);
            MusicVolume = PlayerPrefs.GetFloat("Stillworks.MusicVolume", .55f);
            RainVolume = PlayerPrefs.GetFloat("Stillworks.RainVolume", .2f);
            ReducedMotion = PlayerPrefs.GetInt("Stillworks.ReducedMotion", 0) == 1;
            InvertY = PlayerPrefs.GetInt("Stillworks.InvertY", 0) == 1;
            Fullscreen = PlayerPrefs.GetInt("Stillworks.Fullscreen", 1) == 1;
            Economy = PlayerPrefs.GetInt("Stillworks.Economy", 0) == 1;
            safePosition = route.spawn;
            gameCamera = player.GetComponentInChildren<Camera>();
            speedFX = FindFirstObjectByType<PlayerSpeedFX>();
            titleCamera = new GameObject("Title vista camera").AddComponent<Camera>();
            titleCamera.transform.position = new Vector3(-540, 440, -630);
            titleCamera.transform.LookAt(new Vector3(-60, 306, 0));
            titleCamera.fieldOfView = 53; titleCamera.farClipPlane = 2500;
            titleCamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            titleCamera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            sound = gameObject.AddComponent<StillworksAudio>(); sound.player = player;
            sound.StartMusic(backgroundMusic);
            narration = GetComponent<StillworksNarration>();
            ui = gameObject.AddComponent<StillworksUI>(); ui.Create(this);
            ApplySettings(false);
            SetScreen(ScreenState.Title);
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (State == ScreenState.Playing) Pause();
                else if (State == ScreenState.Paused) Resume();
                else if (State == ScreenState.Settings) CloseSettings();
            }
            if (State != ScreenState.Playing) return;
            if (player.CurrentSpeed > .5f) started = true;
            if (started && !Finished) Elapsed += Time.deltaTime;
            if (player.IsGrounded && !player.IsSliding && CanOccupy(player.transform.position))
            { safePosition = player.transform.position; safeHeading = player.transform.eulerAngles.y; }
            if (player.transform.position.y < -35)
            {
                Falls++;
                Teleport(route.spawn, InitialHeading());
                safePosition = route.spawn;
            }
            if (!Finished && started && Wetness >= 1)
            {
                if (PersistenceEnabled) { PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.Save(); }
                narration.Trigger("soaked"); SetScreen(ScreenState.Failed); return;
            }
            if (CanCollect && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) CollectClothes();
            if (!Finished && started && ClothesCollected && Vector3.Distance(player.transform.position,route.spawn) < 7)
            {
                Finished = true;
                if (Best <= 0 || Elapsed < Best)
                {
                    Best = Elapsed;
                    if (PersistenceEnabled) PlayerPrefs.SetFloat("Stillworks.LaundryBestSeconds", Best);
                }
                if (PersistenceEnabled) { PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.Save(); }
                sound.SummitSignal();
                narration.Trigger("delivered");
                SetScreen(ScreenState.Finished);
            }
            saveTimer += Time.deltaTime;
            if (saveTimer >= 15) { saveTimer = 0; SaveRun(); }
        }

        public void CollectClothes()
        {
            if (!CanCollect || State != ScreenState.Playing || Wetness >= 1) return;
            ClothesCollected = true;
            if (laundry != null) laundry.SetActive(false);
            narration.Trigger("collected"); SaveRun();
        }

        private float InitialHeading()
        {
            foreach (RouteSpan span in route.spans)
                if (span.passage == Passage.Run) return Quaternion.LookRotation(span.to - span.from).eulerAngles.y;
            return 0;
        }

        private void Teleport(Vector3 position, float heading)
        {
            CharacterController controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.transform.position = position + Vector3.up * .04f;
            player.ResetMotion();
            look.ResetView(heading);
            controller.enabled = true;
        }

        public void NewRun()
        {
            Elapsed = 0; Falls = 0; started = Finished = false; saveTimer = 0;
            ClothesCollected = false;
            if (laundry != null) laundry.SetActive(true);
            narration.ResetStory();
            safePosition = route.spawn; safeHeading = InitialHeading();
            if (PersistenceEnabled) { PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.Save(); }
            Teleport(route.spawn, safeHeading);
            SetScreen(ScreenState.Playing);
        }

        public void ContinueRun()
        {
            RunSave save = ReadSave();
            if (save == null) { NewRun(); return; }
            Elapsed = save.elapsed; Falls = save.falls; started = true; Finished = false;
            ClothesCollected = save.clothesCollected;
            if (laundry != null) laundry.SetActive(!ClothesCollected);
            narration.ResetStory(save.narrationMask);
            safePosition = save.position; safeHeading = save.heading;
            Teleport(safePosition, safeHeading);
            SetScreen(ScreenState.Playing);
        }

        private RunSave ReadSave()
        {
            if (!PersistenceEnabled || !PlayerPrefs.HasKey(SaveKey)) return null;
            try
            {
                RunSave save = JsonUtility.FromJson<RunSave>(PlayerPrefs.GetString(SaveKey));
                if (save == null || save.version != 1 || !float.IsFinite(save.elapsed) || save.elapsed < 0 || save.elapsed >= Mathf.Max(60,soakSeconds) ||
                    !float.IsFinite(save.heading) || !float.IsFinite(save.position.sqrMagnitude) || save.falls < 0 ||
                    save.position.y < -12 || save.position.y > 590 || Mathf.Abs(save.position.x) > 2100 || Mathf.Abs(save.position.z) > 2100 ||
                    !CanOccupy(save.position)) return null;
                return save;
            }
            catch (ArgumentException) { return null; }
        }

        private static bool CanOccupy(Vector3 p)
        {
            return Physics.Raycast(p + Vector3.up * .25f, Vector3.down, .65f, 1 << 6) &&
                !Physics.CheckCapsule(p + Vector3.up * .5f, p + Vector3.up * 1.5f, .27f, 1 << 6, QueryTriggerInteraction.Ignore);
        }

        public void SaveRun()
        {
            if (!PersistenceEnabled || !started || Finished || Wetness >= 1) return;
            // ponytail: one local suspended run; add multiple slots only if the game needs them.
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(new RunSave { position = safePosition, heading = safeHeading, elapsed = Elapsed,
                falls = Falls, clothesCollected = ClothesCollected, narrationMask = narration.PlayedMask }));
            PlayerPrefs.Save();
        }

        public void Pause() { if (State == ScreenState.Playing) { SaveRun(); SetScreen(ScreenState.Paused); } }
        public void PlayClick() { if (sound != null) sound.Click(); }
        public void Resume() { SetScreen(ScreenState.Playing); }
        public void ReturnToTitle() { SaveRun(); SetScreen(ScreenState.Title); }
        public void OpenSettings() { settingsReturn = State; SetScreen(ScreenState.Settings); }
        public void CloseSettings() { ApplySettings(true); SetScreen(settingsReturn); }
        public void Quit()
        {
            SaveRun();
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        private void SetScreen(ScreenState next)
        {
            State = next;
            bool playing = next == ScreenState.Playing;
            Time.timeScale = playing ? 1 : 0;
            player.enabled = playing; look.enabled = playing;
            if (speedFX != null) speedFX.enabled = playing;
            bool vista = next == ScreenState.Title || (next == ScreenState.Settings && settingsReturn == ScreenState.Title);
            titleCamera.enabled = vista; gameCamera.enabled = !vista;
            var effects = player.GetComponent<StillworksEffects>();
            if (effects != null) effects.RainAnchor = vista ? titleCamera.transform : gameCamera.transform;
            sound.Suspended = !playing;
            Cursor.lockState = playing ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !playing;
            ui.Show(next);
        }

        public void ApplySettings(bool save)
        {
            Sensitivity = Mathf.Clamp(Sensitivity, .04f, .3f); FieldOfView = Mathf.Clamp(FieldOfView, 70, 105); Volume = Mathf.Clamp01(Volume);
            look.Configure(Sensitivity, ReducedMotion, InvertY);
            if (speedFX != null) speedFX.Configure(FieldOfView, ReducedMotion);
            var effects = player.GetComponent<StillworksEffects>();
            if (effects != null) effects.Configure(ReducedMotion, Economy);
            gameCamera.fieldOfView = FieldOfView;
            AudioListener.volume = Volume;
            sound.MusicLevel = Mathf.Clamp01(MusicVolume); sound.RainLevel = Mathf.Clamp01(RainVolume);
            QualitySettings.SetQualityLevel(Economy ? 0 : Mathf.Min(1, QualitySettings.names.Length - 1));
            QualitySettings.vSyncCount = 1; Application.targetFrameRate = 120;
            if (!Application.isEditor && !Application.isBatchMode) Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (!save || !PersistenceEnabled) return;
            PlayerPrefs.SetFloat("Stillworks.MusicVolume", MusicVolume); PlayerPrefs.SetFloat("Stillworks.RainVolume", RainVolume);
            PlayerPrefs.SetFloat("Stillworks.Sensitivity", Sensitivity); PlayerPrefs.SetFloat("Stillworks.FOV", FieldOfView); PlayerPrefs.SetFloat("Stillworks.Volume", Volume);
            PlayerPrefs.SetInt("Stillworks.ReducedMotion", ReducedMotion ? 1 : 0); PlayerPrefs.SetInt("Stillworks.InvertY", InvertY ? 1 : 0);
            PlayerPrefs.SetInt("Stillworks.Fullscreen", Fullscreen ? 1 : 0); PlayerPrefs.SetInt("Stillworks.Economy", Economy ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void OnApplicationFocus(bool focused) { if (!focused && !Application.isBatchMode) Pause(); }
        private void OnApplicationQuit() { SaveRun(); }
        private void OnDestroy() { Time.timeScale = 1; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        public static string Clock(float time) { int s = Mathf.FloorToInt(time); return $"{s / 60:00}:{s % 60:00}"; }
    }
}
