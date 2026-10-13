using UnityEngine;

namespace Stillworks
{
    /// <summary>Small pooled dust systems; no effects cover the centre of the view.</summary>
    public sealed class StillworksEffects : MonoBehaviour
    {
        public PlayerMovement player;
        public Material particleMaterial;
        public Material rainMaterial;
        public Material splashMaterial;
        private ParticleSystem dust, contact, rain, middleRain, distantRain, cityRain, splashes;
        public Transform RainAnchor;
        private float rainBudget, middleBudget, distantBudget, cityBudget;
        private Vector3 lastRainAnchor;
        public int RainParticles => rain == null ? 0 : rain.particleCount + middleRain.particleCount + distantRain.particleCount + cityRain.particleCount;
        public float RainRadius => 220;
        private bool grounded, reduced, economy;
        private float nextContact;

        public void Configure(bool reducedMotion, bool economyGraphics)
        { reduced = reducedMotion; economy = economyGraphics; }

        private ParticleSystem Create(string label, int maximum)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maximum; main.startLifetime = 2;
            main.startSpeed = .15f; main.startSize = .05f;
            main.startColor = new Color(.75f,.73f,.65f,.12f);
            var emission = system.emission; emission.enabled = false;
            var shape = system.shape; shape.enabled = false;
            var fade = system.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(Color.white,1) },
                new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(1,.15f), new GradientAlphaKey(0,1) });
            fade.color = gradient;
            system.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMaterial;
            system.Play();
            return system;
        }

        private void Start()
        {
            dust = Create("Airborne concrete dust",64);
            contact = Create("Traversal contact dust",96);
            rain = RainLayer("Rain around the view / roof occlusion",3200,.028f,.58f);
            middleRain = RainLayer("Middle-distance rain / all directions",4000,.045f,.42f);
            distantRain = RainLayer("Distant rain / across courtyards",5000,.07f,.3f);
            cityRain = RainLayer("City-wide rain / entire vertical skyline",10000,.10f,.23f);
            splashes = Create("Rain splashes on exposed architecture",384);
            splashes.gameObject.layer = 2;
            var main = splashes.main; main.useUnscaledTime = true;
            var splashRenderer = splashes.GetComponent<ParticleSystemRenderer>();
            splashRenderer.sharedMaterial = splashMaterial; splashRenderer.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            var size = splashes.sizeOverLifetime; size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.15f,1,1));
        }

        private ParticleSystem RainLayer(string name, int maximum, float size, float alpha)
        {
            ParticleSystem system = Create(name,maximum); system.gameObject.layer = 2;
            var main = system.main; main.useUnscaledTime = true;
            main.startColor = new Color(.78f,.87f,.95f,alpha); main.startSize = size;
            var fade = system.colorOverLifetime; fade.enabled = false;
            var renderer = system.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = rainMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch; renderer.lengthScale = 8; renderer.velocityScale = .016f;
            return system;
        }

        private void EmitRain(ParticleSystem system, ref float budget, float inner, float outer, float height, float seconds, int density)
        {
            budget = Mathf.Min(90,budget + Time.unscaledDeltaTime * density);
            int count = (int)budget; budget -= count;
            Vector3 velocity = new Vector3(1.1f,-22,.6f);
            for (int i = 0; i < count; i++)
            {
                float radius = Mathf.Sqrt(Random.Range(inner * inner,outer * outer)), angle = Random.value * Mathf.PI * 2;
                Vector3 p = RainAnchor.position + new Vector3(Mathf.Cos(angle) * radius,Random.Range(5,height),Mathf.Sin(angle) * radius);
                if (Physics.Raycast(p,Vector3.up,650,1 << 6,QueryTriggerInteraction.Ignore)) continue;
                float lifetime = seconds;
                if (Physics.Raycast(p,velocity.normalized,out RaycastHit roof,seconds * velocity.magnitude,1 << 6,QueryTriggerInteraction.Ignore))
                {
                    lifetime = Mathf.Max(.02f,roof.distance / velocity.magnitude);
                    if (inner == 0 && i % 7 == 0 && roof.normal.y > .7f)
                        splashes.Emit(new ParticleSystem.EmitParams { position = roof.point + Vector3.up * .025f,velocity = Vector3.zero,
                            startSize = Random.Range(.14f,.32f),startLifetime = .35f,startColor = new Color(.7f,.83f,.88f,.3f) },1);
                }
                system.Emit(new ParticleSystem.EmitParams { position = p,velocity = velocity,startLifetime = lifetime },1);
            }
        }

        private void EmitCityRain()
        {
            cityBudget = Mathf.Min(45,cityBudget + Time.unscaledDeltaTime * (economy ? 400 : 1200));
            int count = (int)cityBudget; cityBudget -= count;
            Vector3 velocity = new Vector3(1.1f,-22,.6f);
            for (int i = 0; i < count; i++)
            {
                Vector2 offset = Random.insideUnitCircle * 360;
                Vector3 p = new Vector3(offset.x,Random.Range(-8f,700f),offset.y);
                if (Physics.Raycast(p,Vector3.up,800,1 << 6,QueryTriggerInteraction.Ignore)) continue;
                float lifetime = 6;
                if (Physics.Raycast(p,velocity.normalized,out RaycastHit roof,133,1 << 6,QueryTriggerInteraction.Ignore))
                    lifetime = Mathf.Max(.02f,roof.distance / velocity.magnitude);
                cityRain.Emit(new ParticleSystem.EmitParams { position = p,velocity = velocity,startLifetime = lifetime },1);
            }
        }

        private void Update()
        {
            if (RainAnchor != null)
            {
                if (Vector3.Distance(lastRainAnchor,RainAnchor.position) > 80)
                {
                    rain.Clear(); middleRain.Clear(); distantRain.Clear(); splashes.Clear();
                }
                lastRainAnchor = RainAnchor.position;
                EmitRain(rain,ref rainBudget,0,18,14,1.6f,economy ? 600 : 1600);
                EmitRain(middleRain,ref middleBudget,18,75,32,2.6f,economy ? 400 : 1500);
                EmitRain(distantRain,ref distantBudget,75,RainRadius,55,3.5f,economy ? 550 : 1800);
                EmitCityRain();
            }
            if (!player.enabled || Time.timeScale == 0) return;
            if (!economy && !reduced && Random.value < Time.deltaTime * 7)
            {
                var puff = new ParticleSystem.EmitParams {
                    position = player.transform.position + new Vector3(Random.Range(-6f,6f),Random.Range(1f,6f),Random.Range(-6f,6f)),
                    velocity = new Vector3(.12f,.04f,-.06f), startLifetime = 4, startSize = Random.Range(.025f,.06f)
                };
                dust.Emit(puff,1);
            }
            bool wall = player.IsWallRunning || player.IsWallSliding;
            bool landing = !grounded && player.IsGrounded;
            if (!reduced && (landing || ((wall || player.IsSliding) && Time.time > nextContact)))
            {
                nextContact = Time.time + .12f;
                Vector3 p = player.transform.position + (wall ? Vector3.up * .8f - player.WallNormal * .4f : Vector3.up * .08f);
                for (int i = 0; i < (landing ? 12 : 3); i++)
                {
                    var puff = new ParticleSystem.EmitParams {
                        position = p + Random.insideUnitSphere * .12f,
                        velocity = Random.insideUnitSphere * .6f + Vector3.up * .3f,
                        startLifetime = Random.Range(.3f,.65f), startSize = Random.Range(.05f,.13f),
                        startColor = new Color(.6f,.55f,.43f,.25f)
                    };
                    contact.Emit(puff,1);
                }
            }
            grounded = player.IsGrounded;
        }
    }
}
