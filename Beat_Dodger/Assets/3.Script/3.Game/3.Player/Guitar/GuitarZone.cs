using UnityEngine;

namespace BeatDodger.Game
{
    /// <summary>
    /// Manages a group of strings in a specific lane/zone.
    /// Automatically detects child GuitarString components for easy setup.
    /// </summary>
    public class GuitarZone : MonoBehaviour
    {
        [Header("Auto Config")]
        [SerializeField] private GuitarString[] stringsInZone;

        private void Awake()
        {
            // Automatically find strings in children to save manual labor.
            // Just drop 6 GuitarString objects under this zone and it will work!
            stringsInZone = GetComponentsInChildren<GuitarString>();
        }

        [ContextMenu("Vibrate All")] // Testable via context menu in editor
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
