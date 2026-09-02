using UnityEngine;

namespace Boxal.Util
{
    public class RandomBreakPointGenerator : MonoBehaviour
    {
        [SerializeField] private MeshFilter targetMeshFilter;

    [Header("Break Point Settings")]
    [SerializeField] private int pointCountMin = 10;
    [SerializeField] private int pointCountMax = 15;
    [SerializeField] private float edgePadding = 0.2f;

    [Header("Parent")]
    [SerializeField] private Transform breakPointParent;

    [ContextMenu("Generate Break Points")]
    public void Generate()
    {
        if (targetMeshFilter == null || breakPointParent == null)
        {
            Debug.LogError("MeshFilter 또는 Parent 없음");
            return;
        }

        Mesh mesh = targetMeshFilter.sharedMesh;
        Bounds bounds = mesh.bounds;

        int count = Random.Range(pointCountMin, pointCountMax + 1);

        // 기존 포인트 삭제
        for (int i = breakPointParent.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(breakPointParent.GetChild(i).gameObject);
        }

        for (int i = 0; i < count; i++)
        {
            Vector3 localPoint = GetRandomPointInside(bounds);

            GameObject p = new GameObject("BreakPoint_" + i);
            p.transform.SetParent(breakPointParent);

            // 위치 적용
            p.transform.position = targetMeshFilter.transform.TransformPoint(localPoint);
        }
    }

    Vector3 GetRandomPointInside(Bounds bounds)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;

        Vector3 size = max - min;
        Vector3 padding = size * edgePadding;

        min += padding;
        max -= padding;

        float x = Random.Range(min.x, max.x);
        float y = Random.Range(min.y, max.y);
        float z = Random.Range(min.z, max.z);

        // 중심으로 살짝 당김 (자연스럽게)
        Vector3 center = bounds.center;
        Vector3 random = new Vector3(x, y, z);

        return Vector3.Lerp(random, center, 0.3f);
    }
    }

}
