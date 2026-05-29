using UnityEngine;

[ExecuteAlways]
public class ObjectHorizontalLayout : MonoBehaviour
{
    [Header("Layout Settings")]
    [SerializeField] private float spacing = 2.0f;
    [SerializeField] private bool useCenterAlign = true;

    [Header("Child Settings")]
    [SerializeField] private Vector3 childScale = Vector3.one; // 자식들의 크기 일괄 제어

    private void OnValidate()
    {
        UpdateLayout();
    }

    public void UpdateLayout()
    {
        int childCount = transform.childCount;
        if (childCount == 0)
        {
            return;
        }

        float startX = CalculateStartPosition(childCount);

        for (int i = 0; i < childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (child != null)
            {
                // 위치 업데이트
                child.localPosition = new Vector3(startX + (i * spacing), 0f, 0f);
                
                // 크기 업데이트 (추가된 기능)
                child.localScale = childScale;
            }
        }
    }

    private float CalculateStartPosition(int childCount)
    {
        if (useCenterAlign)
        {
            return -(childCount - 1) * spacing * 0.5f;
        }
        return 0f;
    }
}