using System;
using System.Collections.Generic;
using UnityEngine;

namespace Stillworks
{
    public sealed class StillworksNarration : MonoBehaviour
    {
        [Serializable] public sealed class Cue { public string id, text, @event; public float height; }
        [Serializable] private sealed class Script { public Cue[] cues; }
        public TextAsset dialogue;
        public AudioClip[] voiceClips;
        public StillworksSession session;
        public long PlayedMask { get; set; }
        public string Subtitle { get; private set; }
        private Cue[] cues;
        private AudioSource voice;
        private float remaining, peak, nextTaunt;
        private bool paused, tauntedFall;
        private readonly Queue<int> pending = new Queue<int>();
        private readonly HashSet<int> queued = new HashSet<int>();
        public int FallTauntsPlayed { get; private set; }
        public int LocationTriggers { get; private set; }

        private void Awake()
        {
            cues = JsonUtility.FromJson<Script>(dialogue.text).cues;
            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 0; voice.volume = .9f;
        }

        public void ResetStory(long mask = 0)
        {
            PlayedMask = mask; Subtitle = ""; remaining = peak = 0;
            FallTauntsPlayed = 0; nextTaunt = 0; tauntedFall = false;
            pending.Clear(); queued.Clear(); LocationTriggers = 0;
            voice.Stop(); paused = false;
        }

        public void Trigger(string eventName)
        {
            for (int i = 0; i < cues.Length; i++)
                if (cues[i].@event == eventName && (PlayedMask & (1L << i)) == 0)
                { pending.Clear(); queued.Clear(); Speak(i); return; }
        }

        public void QueueCue(string id)
        {
            if (session.State != StillworksSession.ScreenState.Playing || session.ClothesCollected) return;
            for (int i = 0; i < cues.Length; i++)
                if (cues[i].id == id && (PlayedMask & (1L << i)) == 0 && queued.Add(i))
                { pending.Enqueue(i); LocationTriggers++; return; }
        }

        public void TauntFall()
        {
            if (Time.unscaledTime < nextTaunt) return;
            int total = 0;
            foreach (Cue cue in cues) if (cue.@event == "fall-taunt") total++;
            if (total == 0) return;
            int selected = FallTauntsPlayed++ % total;
            for (int i = 0; i < cues.Length; i++)
                if (cues[i].@event == "fall-taunt" && selected-- == 0) { Speak(i,true); break; }
            nextTaunt = Time.unscaledTime + 6;
            tauntedFall = true;
        }

        private void Speak(int i, bool repeat = false)
        {
            if (!repeat) PlayedMask |= 1L << i;
            Subtitle = cues[i].text;
            voice.Stop();
            AudioClip clip = i < voiceClips.Length ? voiceClips[i] : null;
            remaining = clip == null ? Mathf.Max(5,cues[i].text.Length / 14f) : clip.length + .6f;
            if (clip != null) { voice.clip = clip; voice.Play(); }
        }

        private void Update()
        {
            bool menu = session.State == StillworksSession.ScreenState.Title || session.State == StillworksSession.ScreenState.Paused ||
                session.State == StillworksSession.ScreenState.Settings;
            if (menu && !paused) { voice.Pause(); paused = true; }
            if (!menu && paused) { voice.UnPause(); paused = false; }
            if (menu) return;
            remaining -= Time.unscaledDeltaTime;
            if (remaining <= 0) Subtitle = "";
            if (session.State != StillworksSession.ScreenState.Playing || session.Elapsed < .1f) return;
            float altitude = session.player.transform.position.y;
            peak = Mathf.Max(peak,altitude);
            if (!tauntedFall && peak - altitude > 12) TauntFall();
            if (session.player.IsGrounded)
            {
                if (!tauntedFall && peak - altitude >= 4) TauntFall();
                peak = altitude; tauntedFall = false;
            }
            if (remaining > 0) return;
            if (session.Wetness >= .75f) { Trigger("wet"); if (remaining > 0) return; }
            if (pending.Count > 0)
            {
                int i = pending.Dequeue(); queued.Remove(i);
                if ((PlayedMask & (1L << i)) == 0) { Speak(i); return; }
            }
            for (int i = 0; i < cues.Length; i++)
            {
                if ((PlayedMask & (1L << i)) != 0) continue;
                bool eligible = cues[i].@event == "start" ||
                    (cues[i].@event == "altitude" && !session.ClothesCollected && altitude >= cues[i].height) ||
                    (cues[i].@event == "descent" && session.ClothesCollected && altitude <= cues[i].height);
                if (eligible) { Speak(i); return; }
            }
        }
    }
}
