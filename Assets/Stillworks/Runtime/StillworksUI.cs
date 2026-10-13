using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Stillworks
{
    /// <summary>Native Canvas controls, built once and shared by the title, pause and finish screens.</summary>
    public sealed class StillworksUI : MonoBehaviour
    {
        private StillworksSession session;
        private RectTransform canvas, menu, settings, hud;
        private UnityEngine.UI.Image shade;
        private TMP_Text heading, description, stats, district, telemetry, hint;
        private TMP_Text subtitle;
        private RectTransform subtitlePanel;
        private StillworksNarration narration;
        private readonly List<UnityEngine.UI.Button> actions = new List<UnityEngine.UI.Button>();
        private readonly Color ink = new Color(.90f,.89f,.83f);
        private readonly Color muted = new Color(.64f,.71f,.70f);
        private readonly Color accent = new Color(.72f,.43f,.25f);
        private UnityEngine.UI.Slider sensitivity, fov, volume, music, rainfall;
        private UnityEngine.UI.Toggle motion, invert, fullscreen, economy;
        private float nextHud;

        public void Create(StillworksSession owner)
        {
            session = owner;
            narration = owner.GetComponent<StillworksNarration>();
            canvas = Rect("Stillworks interface", transform);
            canvas.gameObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600,900); scaler.matchWidthOrHeight = .5f;
            canvas.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("Interface input", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            shade = Image("Menu shade", canvas, new Color(.02f,.035f,.04f,.40f)); Stretch(shade.rectTransform);
            menu = Card("Menu", canvas, new Vector2(520,720));
            Text(menu, "FIELD RECORDING   /   09", 15, 24, accent);
            heading = Text(menu, "THE\nSTILLWORKS", 58, 134, ink);
            heading.fontStyle = FontStyles.Bold;
            description = Text(menu, "The washing is on the highest roof.\nThe rain is already here.", 19, 67, muted);
            stats = Text(menu, "588 METRES  /  7 DISTRICTS", 14, 38, accent);
            for (int i = 0; i < 5; i++) actions.Add(Button(menu, "Action " + i, () => { }));
            Text(menu, "WASD Move / SHIFT Sprint / SPACE Jump\nCTRL Slide / E Take laundry / ESC Pause", 15, 57, muted);

            settings = Card("Settings", canvas, new Vector2(560,840));
            settings.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().spacing = 7;
            settings.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().padding = new RectOffset(32,32,22,22);
            Text(settings, "TUNE THE EXPERIENCE", 14, 25, accent);
            Text(settings, "SETTINGS", 42, 62, ink).fontStyle = FontStyles.Bold;
            sensitivity = Slider(settings, "Mouse sensitivity", .04f,.3f, session.Sensitivity, v => session.Sensitivity = v, "0.00");
            fov = Slider(settings, "Field of view", 70,105,session.FieldOfView, v => session.FieldOfView = v, "0");
            volume = Slider(settings, "Master volume", 0,1,session.Volume, v => { session.Volume = v; AudioListener.volume = v; }, "P0");
            music = Slider(settings,"Background music",0,1,session.MusicVolume,v => { session.MusicVolume = v; session.ApplySettings(false); },"P0");
            rainfall = Slider(settings,"Rain sound",0,1,session.RainVolume,v => { session.RainVolume = v; session.ApplySettings(false); },"P0");
            motion = Toggle(settings, "Reduce camera motion and speed effects", session.ReducedMotion, v => session.ReducedMotion = v);
            invert = Toggle(settings, "Invert vertical mouse look", session.InvertY, v => session.InvertY = v);
            fullscreen = Toggle(settings, "Borderless fullscreen", session.Fullscreen, v => session.Fullscreen = v);
            economy = Toggle(settings, "Economy graphics", session.Economy, v => session.Economy = v);
            Text(settings, "Settings are stored on this computer.\nMouse and keyboard are required to play.", 15, 48, muted);
            Button(settings, "APPLY & BACK", session.CloseSettings);

            hud = Rect("Run HUD", canvas); Stretch(hud);
            var header = Rect("District", hud); Anchor(header, new Vector2(0,1), new Vector2(32,-28), new Vector2(500,85));
            district = Text(header, "", 23, 80, ink); Stretch(district.rectTransform);
            var clock = Rect("Telemetry", hud); Anchor(clock, Vector2.one, new Vector2(-32,-28), new Vector2(330,85));
            telemetry = Text(clock, "", 22, 80, ink); telemetry.alignment = TextAlignmentOptions.TopRight; Stretch(telemetry.rectTransform);
            var lower = Rect("Instructions", hud); Anchor(lower, Vector2.zero, new Vector2(32,32), new Vector2(660,85));
            hint = Text(lower, "", 16,85,muted); hint.alignment = TextAlignmentOptions.BottomLeft; Stretch(hint.rectTransform);
            var dot = Image("Reticle", hud, new Color(.9f,.9f,.84f,.7f)); Anchor(dot.rectTransform, Vector2.one * .5f, Vector2.zero, Vector2.one * 3);
            subtitlePanel = Image("Narrator subtitles",canvas,new Color(.02f,.035f,.04f,.82f)).rectTransform;
            Anchor(subtitlePanel,new Vector2(.5f,0),new Vector2(0,140),new Vector2(900,76));
            subtitle = Text(subtitlePanel,"",19,76,ink); Stretch(subtitle.rectTransform);
            subtitle.alignment = TextAlignmentOptions.Center; subtitle.margin = new Vector4(20,8,20,8);
            subtitlePanel.gameObject.SetActive(false);
        }

        public void Show(StillworksSession.ScreenState state)
        {
            bool playing = state == StillworksSession.ScreenState.Playing;
            hud.gameObject.SetActive(playing); shade.gameObject.SetActive(!playing);
            settings.gameObject.SetActive(state == StillworksSession.ScreenState.Settings);
            menu.gameObject.SetActive(!playing && state != StillworksSession.ScreenState.Settings);
            if (playing) { EventSystem.current.SetSelectedGameObject(null); return; }
            if (state == StillworksSession.ScreenState.Settings)
            {
                sensitivity.SetValueWithoutNotify(session.Sensitivity); fov.SetValueWithoutNotify(session.FieldOfView); volume.SetValueWithoutNotify(session.Volume);
                music.SetValueWithoutNotify(session.MusicVolume); rainfall.SetValueWithoutNotify(session.RainVolume);
                motion.SetIsOnWithoutNotify(session.ReducedMotion); invert.SetIsOnWithoutNotify(session.InvertY);
                fullscreen.SetIsOnWithoutNotify(session.Fullscreen); economy.SetIsOnWithoutNotify(session.Economy);
                EventSystem.current.SetSelectedGameObject(sensitivity.gameObject); return;
            }
            foreach (var action in actions) action.gameObject.SetActive(false);
            bool title = state == StillworksSession.ScreenState.Title;
            bool failed = state == StillworksSession.ScreenState.Failed;
            Anchor(menu, title ? new Vector2(0,.5f) : Vector2.one * .5f, title ? new Vector2(60,0) : Vector2.zero, new Vector2(520,720));
            heading.text = title ? "THE\nSTILLWORKS" : failed ? "TOO\nSOAKED" : state == StillworksSession.ScreenState.Finished ? "DRY\nENOUGH" : "HOLD YOUR\nGROUND";
            heading.fontSize = state == StillworksSession.ScreenState.Finished ? 45 : 58;
            description.text = title ? "Retrieve the laundry from the Crown.\nBring it back here before the rain wins." : failed ?
                "The washing is soaked. Find a faster route\nand try the errand again." : session.Finished ?
                "The clothes are back under the shelter.\nNext time, use the balcony." : "The rain waits while you pause.\nLook for lower roofs and service routes.";
            stats.text = title ? "588 M UP  /  BACK TO THE STREET  /  45 MINUTES" : $"{StillworksSession.Clock(session.Elapsed)}  /  {session.Wetness:P0} WET  /  BEST {(session.Best > 0 ? StillworksSession.Clock(session.Best) : "--:--")}";
            if (title)
            {
                Action(0, "BEGIN ASCENT", session.NewRun);
                if (session.HasSavedRun) Action(1, "CONTINUE SAVED RUN", session.ContinueRun);
                Action(2, "SETTINGS", session.OpenSettings); Action(3, "QUIT", session.Quit);
            }
            else if (failed)
            {
                Action(0,"TRY THE ERRAND AGAIN",session.NewRun);
                Action(1,"SETTINGS",session.OpenSettings); Action(2,"RETURN TO TITLE",session.ReturnToTitle);
            }
            else
            {
                Action(0, session.Finished ? "STAY A WHILE" : "CONTINUE THE ERRAND", session.Resume);
                Action(1, "RESTART FROM THE STREET", session.NewRun);
                Action(2, "SETTINGS", session.OpenSettings);
                Action(3, session.Finished ? "RETURN TO TITLE" : "SAVE & RETURN TO TITLE", session.ReturnToTitle);
                Action(4, "QUIT", session.Quit);
            }
            EventSystem.current.SetSelectedGameObject(actions[0].gameObject);
        }

        private void Action(int index, string label, UnityAction callback)
        {
            var button = actions[index]; button.gameObject.SetActive(true);
            button.GetComponentInChildren<TMP_Text>().text = label;
            button.onClick.RemoveAllListeners(); button.onClick.AddListener(session.PlayClick); button.onClick.AddListener(callback);
        }

        private void Update()
        {
            if (session == null) return;
            bool showSubtitle = narration != null && !string.IsNullOrEmpty(narration.Subtitle) && session.State != StillworksSession.ScreenState.Title;
            subtitlePanel.gameObject.SetActive(showSubtitle);
            if (showSubtitle)
            {
                subtitle.text = narration.Subtitle;
                subtitlePanel.anchoredPosition = new Vector2(0,session.State == StillworksSession.ScreenState.Playing ? 140 : 16);
            }
            if (session == null || !hud.gameObject.activeSelf || Time.unscaledTime < nextHud) return;
            nextHud = Time.unscaledTime + .1f;
            float altitude = Mathf.Max(0,session.player.transform.position.y);
            int area = Mathf.Clamp((int)(altitude / 84),0,6);
            district.text = $"<size=13>THE STILLWORKS  /  {area + 1:00}</size>\n{session.route.districts[area].ToUpperInvariant()}";
            telemetry.text = $"{StillworksSession.Clock(session.Elapsed)}\n<size=14>{altitude:000} M / 588 M   •   {session.player.CurrentSpeed:0.0} M/S</size>\n<size=16>{session.Wetness:P0} WET / {(session.ClothesCollected ? "RETURN TO SHELTER" : "FIND THE LAUNDRY")}</size>";
            hint.text = session.player.IsWallRunning ? "WALL RUN   /   RELEASE SPACE TO KICK AWAY" : session.player.IsWallSliding ?
                "WALL SLIDE   /   SPACE TO KICK AWAY" : session.Elapsed < 30 ?
                "WASD move · Shift sprint · Space jump · Ctrl slide\nHold Space beside a wall to run; release to kick away. Ctrl slows a wall descent.\nESC pause / Save and resume from the menu." : "ESC  PAUSE     /     LOCAL TIMER";
            if (session.CanCollect) hint.text = "E  /  TAKE THE LAUNDRY\nBring it back to the shelter at the street.";
            else if (session.ClothesCollected && !session.Finished) hint.text = "BRING THE LAUNDRY BACK TO THE STREET SHELTER.\nUse lower roofs and wall-slides to descend.";
        }

        private RectTransform Card(string name, Transform parent, Vector2 size)
        {
            var image = Image(name, parent, new Color(.035f,.06f,.065f,.95f));
            Anchor(image.rectTransform, Vector2.one * .5f, Vector2.zero, size);
            var layout = image.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
            layout.padding = new RectOffset(32,32,28,28); layout.spacing = 10;
            layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            return image.rectTransform;
        }

        private TMP_Text Text(Transform parent, string value, int size, float height, Color color)
        {
            var rect = Rect("Label", parent);
            var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
            text.text = value; text.fontSize = size; text.color = color; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.Normal; text.overflowMode = TextOverflowModes.Overflow;
            rect.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = height;
            return text;
        }

        private UnityEngine.UI.Button Button(Transform parent, string value, UnityAction callback)
        {
            var image = Image(value, parent, new Color(.13f,.19f,.20f)); image.raycastTarget = true;
            image.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 48;
            var button = image.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(1.8f,1.55f,1.3f);
            colors.selectedColor = colors.highlightedColor; colors.pressedColor = new Color(.8f,.6f,.4f); button.colors = colors;
            var label = Text(image.transform,value,18,48,ink); label.alignment = TextAlignmentOptions.MidlineLeft;
            Stretch(label.rectTransform); label.margin = new Vector4(18,0,12,0);
            button.onClick.AddListener(session.PlayClick); button.onClick.AddListener(callback); return button;
        }

        private UnityEngine.UI.Slider Slider(Transform parent, string label, float min, float max, float initial, Action<float> changed, string format)
        {
            TMP_Text caption = Text(parent,label + "   " + initial.ToString(format),17,25,ink);
            var track = Image(label, parent, new Color(.15f,.22f,.23f)); track.raycastTarget = true;
            track.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 25;
            var handle = Image("Handle", track.transform, accent); handle.rectTransform.sizeDelta = new Vector2(14,25);
            var slider = track.gameObject.AddComponent<UnityEngine.UI.Slider>(); slider.targetGraphic = handle;
            slider.handleRect = handle.rectTransform; slider.minValue = min; slider.maxValue = max; slider.value = initial;
            slider.onValueChanged.AddListener(v => { changed(v); caption.text = label + "   " + v.ToString(format); });
            return slider;
        }

        private UnityEngine.UI.Toggle Toggle(Transform parent, string label, bool initial, Action<bool> changed)
        {
            var row = Rect(label,parent); row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().preferredHeight = 33;
            var hitArea = row.gameObject.AddComponent<UnityEngine.UI.Image>(); hitArea.color = Color.clear; hitArea.raycastTarget = true;
            var box = Image("Box",row,new Color(.2f,.28f,.29f)); box.raycastTarget = true;
            Anchor(box.rectTransform,new Vector2(0,.5f),Vector2.zero,new Vector2(25,25));
            var mark = Image("Check",box.transform,accent); Stretch(mark.rectTransform); mark.rectTransform.offsetMin = Vector2.one * 5; mark.rectTransform.offsetMax = -Vector2.one * 5;
            var caption = Text(row,label,16,33,ink); Stretch(caption.rectTransform); caption.margin = new Vector4(39,0,0,0); caption.alignment = TextAlignmentOptions.MidlineLeft;
            var toggle = row.gameObject.AddComponent<UnityEngine.UI.Toggle>(); toggle.targetGraphic = box; toggle.graphic = mark;
            toggle.isOn = initial; toggle.onValueChanged.AddListener(v => { session.PlayClick(); changed(v); }); return toggle;
        }

        private static RectTransform Rect(string name, Transform parent)
        { var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false); return rect; }
        private static UnityEngine.UI.Image Image(string name, Transform parent, Color color)
        { var image = Rect(name,parent).gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return image; }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = rect.pivot = anchor; rect.anchoredPosition = position; rect.sizeDelta = size; }
    }
}
