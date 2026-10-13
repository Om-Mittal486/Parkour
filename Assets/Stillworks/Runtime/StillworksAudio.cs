using UnityEngine;

namespace Stillworks
{
    /// <summary>Original synthesized ambience and surface sounds; no external audio dependencies.</summary>
    public sealed class StillworksAudio : MonoBehaviour
    {
        public PlayerMovement player;
        public bool Suspended { get; set; }
        public float MusicLevel = .55f, RainLevel = .2f;
        private AudioSource wind, foley, rain, music, interfaceSound;
        private AudioClip step, land, scrape, signal, click;
        private float nextShelterCheck;
        private bool sheltered;
        public bool MusicPlaying => music != null && music.isPlaying;
        public int ClicksPlayed { get; private set; }
        private float cadence;
        private bool grounded, sliding, wall;

        private void Awake()
        {
            wind = gameObject.AddComponent<AudioSource>();
            wind.loop = true; wind.spatialBlend = 0; wind.volume = .17f;
            wind.clip = Synth("Air through the Stillworks", 8, 0);
            wind.Play();
            rain = gameObject.AddComponent<AudioSource>(); rain.loop = true; rain.spatialBlend = 0; rain.volume = .02f;
            rain.clip = Synth("Distant soft rainfall",24,5); rain.Play();
            music = gameObject.AddComponent<AudioSource>(); music.loop = true; music.spatialBlend = 0;
            interfaceSound = gameObject.AddComponent<AudioSource>(); interfaceSound.spatialBlend = 0;
            foley = gameObject.AddComponent<AudioSource>(); foley.spatialBlend = 0;
            step = Synth("Concrete footfall", .14f, 1);
            land = Synth("Concrete landing", .25f, 2);
            scrape = Synth("Cloth and grit", .75f, 3);
            signal = Synth("Crown carrier tone", 2.6f, 4);
            click = Synth("Soft interface click",.07f,6);
        }

        public void SummitSignal() { foley.PlayOneShot(signal, .5f); }
        public void StartMusic(AudioClip clip) { music.clip = clip; if (clip != null) music.Play(); }
        public void Click() { ClicksPlayed++; interfaceSound.PlayOneShot(click,.65f); }

        private void Update()
        {
            if (player == null) return;
            float altitude = Mathf.Clamp01(player.transform.position.y / 588f);
            if (Time.unscaledTime >= nextShelterCheck)
            { nextShelterCheck = Time.unscaledTime + .5f; sheltered = Physics.Raycast(player.transform.position + Vector3.up * 2,Vector3.up,650,1 << 6); }
            var narration = GetComponent<StillworksNarration>();
            bool speaking = narration != null && !string.IsNullOrEmpty(narration.Subtitle);
            music.volume = Mathf.Lerp(music.volume,MusicLevel * (speaking ? .16f : .42f),Time.unscaledDeltaTime * 2);
            rain.volume = Mathf.Lerp(rain.volume,RainLevel * (sheltered && !Suspended ? .025f : .10f),Time.unscaledDeltaTime * 2);
            wind.volume = Mathf.Lerp(wind.volume, Suspended ? .035f : .04f + altitude * .04f + player.CurrentSpeed * .001f, Time.unscaledDeltaTime * 2);
            if (Suspended) { grounded = player.IsGrounded; return; }
            if (player.IsGrounded && !grounded) foley.PlayOneShot(land, .45f);
            if (player.IsSliding && !sliding) foley.PlayOneShot(scrape, .3f);
            bool againstWall = player.IsWallRunning || player.IsWallSliding;
            if (againstWall && !wall) foley.PlayOneShot(scrape,.18f);
            if (player.IsGrounded && !player.IsSliding && player.CurrentSpeed > 1)
            {
                cadence += Time.deltaTime * Mathf.Lerp(1.7f, 3.3f, player.CurrentSpeed / 14f);
                if (cadence >= 1) { cadence %= 1; foley.pitch = Random.Range(.91f,1.08f); foley.PlayOneShot(step, .20f); }
            }
            else cadence = .7f;
            grounded = player.IsGrounded; sliding = player.IsSliding; wall = againstWall;
        }

        private static AudioClip Synth(string name, float seconds, int kind)
        {
            const int rate = 22050;
            var samples = new float[Mathf.CeilToInt(seconds * rate)];
            var random = new System.Random(103 + kind);
            float filtered = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate, u = t / seconds;
                float noise = (float)random.NextDouble() * 2 - 1;
                filtered = Mathf.Lerp(filtered, noise, kind == 0 ? .035f : kind == 5 ? .055f : .3f);
                if (kind == 0)
                    samples[i] = filtered * (.55f + .16f * Mathf.Sin(t * Mathf.PI / 2)) + .012f * Mathf.Sin(t * 2 * Mathf.PI * 48);
                else if (kind == 5)
                    samples[i] = filtered * .28f + noise * .008f;
                else if (kind == 6)
                    samples[i] = Mathf.Sin(t * Mathf.PI * 2 * 740) * Mathf.Exp(-u * 10) * .28f;
                else if (kind == 4)
                    samples[i] = Mathf.Sin(t * Mathf.PI * 2 * 392) * Mathf.Sin(t * Mathf.PI * 2 * 1.5f) * Mathf.Sin(Mathf.PI * u) * .12f;
                else
                {
                    float decay = Mathf.Exp(-u * (kind == 3 ? 2.5f : 10));
                    float thud = kind == 3 ? 0 : Mathf.Sin(t * Mathf.PI * 2 * (kind == 2 ? 62 : 110)) * .35f;
                    samples[i] = (filtered * .7f + thud) * decay * Mathf.Min(1,t * 900);
                }
            }
            // Blend the loop seam and fade transient tails to avoid clicks.
            int edge = Mathf.Min(220, samples.Length / 8);
            for (int i = 0; i < edge; i++)
            { samples[i] *= (float)i / edge; samples[samples.Length - 1 - i] *= (float)i / edge; }
            AudioClip clip = AudioClip.Create(name, samples.Length, 1, rate, false);
            clip.SetData(samples, 0); return clip;
        }

        private void OnDestroy()
        {
            if (wind != null && wind.clip != null) Destroy(wind.clip);
            if (rain != null && rain.clip != null) Destroy(rain.clip);
            foreach (AudioClip clip in new[] { step, land, scrape, signal, click }) if (clip != null) Destroy(clip);
        }
    }
}
