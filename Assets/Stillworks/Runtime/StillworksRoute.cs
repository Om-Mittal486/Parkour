using System;
using UnityEngine;

namespace Stillworks
{
    public enum Passage { Run, Ramp, Jump, Slide, Exploration, Shortcut, Recovery }

    [Serializable]
    public struct RouteSpan
    {
        public string name;
        public Passage passage;
        public Vector3 from;
        public Vector3 to;
        public float width;
        public RouteSpan(string label, Passage type, Vector3 a, Vector3 b, float w)
        { name = label; passage = type; from = a; to = b; width = w; }
    }

    [CreateAssetMenu(menuName = "Stillworks/Route manifest")]
    public sealed class StillworksRoute : ScriptableObject
    {
        public Vector3 spawn;
        public Vector3 summit;
        public float mainRouteMetres;
        public string[] districts;
        public RouteSpan[] spans;
    }
}
