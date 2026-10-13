using UnityEngine;

namespace Stillworks
{
    public sealed class StillworksDialogueTrigger : MonoBehaviour
    {
        public StillworksNarration narrator;
        public string cueId;

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<PlayerMovement>() != null) narrator.QueueCue(cueId);
        }
    }
}
