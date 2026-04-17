using UnityEngine;

public class BackgroundScroller : MonoBehaviour
{
    [Header("Scrolling Settings")]
    [SerializeField] private Vector2 scrollDirection = new Vector2(0, 1); // 1이면 정방향, -1이면 역방향
    [SerializeField] private float scrollSpeed = 0.5f;
    [SerializeField] private string texturePropertyName = "_BaseMap";
    
    private MeshRenderer meshRenderer;
    private Material targetMaterial;
    private Vector2 currentOffset;

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            targetMaterial = meshRenderer.material;
        }
    }

    private void Update()
    {
        if (targetMaterial == null) return;

        // 시간과 속도, 방향을 곱해 오프셋 계산
        currentOffset += scrollDirection * (scrollSpeed * Time.deltaTime);
        
        // 텍스처 오프셋 적용
        targetMaterial.SetTextureOffset(texturePropertyName, currentOffset);
    }

    private void OnDestroy()
    {
        if (targetMaterial != null)
        {
            Destroy(targetMaterial);
        }
    }
}
