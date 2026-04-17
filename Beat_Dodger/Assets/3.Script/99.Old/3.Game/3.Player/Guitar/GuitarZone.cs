using UnityEngine;

namespace BeatDodger.Game
{
    
    
    
    
    public class GuitarZone : MonoBehaviour
    {
        [Header("Auto Config")]
        [SerializeField] private GuitarString[] stringsInZone;

        private void Awake()
        {
            
            
            stringsInZone = GetComponentsInChildren<GuitarString>();
        }

        [ContextMenu("Vibrate All")] 
        public void VibrateAll()
        {
            if (stringsInZone == null || stringsInZone.Length == 0)
            {
                Debug.LogWarning($"<color=red>[GuitarZone]</color> {gameObject.name}: No strings found in children! Check hierarchy.");
                return;
            }
            
            for (int i = 0; i < stringsInZone.Length; i++)
            {
                if (stringsInZone[i] != null)
                {
                    stringsInZone[i].Vibrate();
                }
            }
        }
    }
}
