#if UNITY_EDITOR
using UnityEditor;
#endif

using System.Collections.Generic;
using UnityEngine;
using Hanzzz.MeshDemolisher;

namespace Boxal.Util
{
    public class DemolishToPrefabTool : MonoBehaviour
    {
        [Header("Input")]
        public GameObject targetObject;
        public Transform breakPointsParent;
        public Material interiorMaterial;

        [Header("Output")]
        public string saveFolder = "Assets/DemolishedPrefabs";

        private MeshDemolisher demolisher = new MeshDemolisher();

        [ContextMenu("Generate Prefab")]
        public void GeneratePrefab()
        {
#if UNITY_EDITOR
            if (targetObject == null || breakPointsParent == null)
            {
                Debug.LogError("세팅 안됨");
                return;
            }

            // breakPoints 수집
            List<Transform> breakPoints = new List<Transform>();
            for (int i = 0; i < breakPointsParent.childCount; i++)
            {
                breakPoints.Add(breakPointsParent.GetChild(i));
            }

            // 메쉬 분해
            List<GameObject> pieces = demolisher.Demolish(targetObject, breakPoints, interiorMaterial);

            // 폴더 없으면 생성
            if (!AssetDatabase.IsValidFolder(saveFolder))
            {
                AssetDatabase.CreateFolder("Assets", "DemolishedPrefabs");
            }

            // 루트 생성
            GameObject root = new GameObject(targetObject.name + "_Demolished");

            int index = 0;

            foreach (var piece in pieces)
            {
                MeshFilter mf = piece.GetComponent<MeshFilter>();

                Mesh mesh = Instantiate(mf.sharedMesh);

                string meshPath = $"{saveFolder}/{targetObject.name}_mesh_{index}.asset";

                AssetDatabase.CreateAsset(mesh, meshPath);

                Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                mf.sharedMesh = savedMesh;

                // ⭐ 부모 설정 (중요)
                piece.transform.SetParent(root.transform);

                index++; // ⭐ 이거 반드시
            }

            // Prefab 저장
            string prefabPath = $"{saveFolder}/{targetObject.name}_Demolished.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);

            Debug.Log($"Prefab 생성 완료: {prefabPath}");

            // 씬 정리 (선택)
            DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif
        }
    }
}
