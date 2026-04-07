using UnityEngine;

namespace BeatDodger.Game
{
    /// <summary>
    /// Professional Guitar String Physics with Visual Highlighting
    /// High-frequency oscillation with exponential decay, harmonics, and hit-flash colors.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class GuitarString : MonoBehaviour
    {
        [Header("Appearance")]
        [SerializeField] private float length = 10f;
        [SerializeField] private float width = 0.04f;
        [SerializeField] private int resolution = 30;

        [Header("Professional Physics")]
        [SerializeField] private float intensity = 0.08f;
        [SerializeField] private float frequency = 600f;
        [SerializeField] private float decaySpeed = 8f;

        [Header("Highlight Settings")]
        [SerializeField] private Color normalColor = new Color(0.6f, 0.6f, 0.7f, 1f);
        [SerializeField] private Color hitColor = new Color(1f, 1f, 1f, 1f);

        private LineRenderer line;
        private Vector3[] initialPositions;
        private float currentVibration;
        private float timeSinceVibrate;

        private void OnValidate()
        {
            if (!Application.isPlaying) SetupLine();
        }

        private void Awake()
        {
            SetupLine();
        }

        private void SetupLine()
        {
            line = GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = resolution;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = normalColor;
            line.endColor = normalColor;

            initialPositions = new Vector3[resolution];
            for (int i = 0; i < resolution; i++)
            {
                float t = (float)i / (resolution - 1);
                float x = Mathf.Lerp(-length / 2f, length / 2f, t);
                initialPositions[i] = new Vector3(x, 0, 0);
                line.SetPosition(i, initialPositions[i]);
            }
        }

        public void Vibrate()
        {
            if (intensity <= 0.0001f)
            {
                Debug.LogWarning($"<color=orange>[GuitarString]</color> {gameObject.name}: Intensity is 0! Reset to 0.08 in Inspector.");
            }

            currentVibration = intensity;
            timeSinceVibrate = 0f;
            
            // Instantly highlight when hit
            if (line != null)
            {
                line.startColor = hitColor;
                line.endColor = hitColor;
            }
        }

        private void Update()
        {
            if (currentVibration > 0.0001f)
            {
                timeSinceVibrate += Time.deltaTime;
                currentVibration = intensity * Mathf.Exp(-decaySpeed * timeSinceVibrate);
                
                float phase = timeSinceVibrate * frequency;

                // Dynamically blend color back to normal as vibration dies down
                float t_lerp = currentVibration / intensity;
                Color currentColor = Color.Lerp(normalColor, hitColor, t_lerp);
                line.startColor = currentColor;
                line.endColor = currentColor;

                for (int i = 0; i < resolution; i++)
                {
                    float t = (float)i / (resolution - 1);
                    float weight = Mathf.Sin(t * Mathf.PI);
                    
                    // Complex multi-harmonic wave
                    float wave = Mathf.Sin(phase);
                    wave += Mathf.Sin(phase * 2.17f) * 0.5f; 
                    wave += Mathf.Sin(phase * 4.33f) * 0.25f;

                    float offset = wave * currentVibration * weight;
                    line.SetPosition(i, initialPositions[i] + Vector3.up * offset);
                }
            }
            else if (currentVibration > 0)
            {
                currentVibration = 0;
                line.startColor = normalColor;
                line.endColor = normalColor;
                for (int i = 0; i < resolution; i++)
                {
                    line.SetPosition(i, initialPositions[i]);
                }
            }
        }
    }
}
