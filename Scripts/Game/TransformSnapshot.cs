using UnityEngine;

namespace Boxal.Game
{
    public class TransformSnapshot
    {
        #region Variables
        private Transform[] transforms;

        private Vector3[] localPositions;
        private Quaternion[] localRotations;
        private Vector3[] localScales;
        private bool[] activeStates;
        private Rigidbody[] rigidbodies;
        #endregion

        public void SaveSnapshot(GameObject target)
        {
            // 비활성 오브젝트까지 포함해서 전부 캐싱
            transforms = target.GetComponentsInChildren<Transform>(true);

            int count = transforms.Length;

            localPositions = new Vector3[count];
            localRotations = new Quaternion[count];
            localScales = new Vector3[count];
            activeStates = new bool[count];

            rigidbodies = target.GetComponentsInChildren<Rigidbody>(true);

            for (int i = 0; i < count; i++)
            {
                localPositions[i] = transforms[i].localPosition;
                localRotations[i] = transforms[i].localRotation;
                localScales[i] = transforms[i].localScale;
                activeStates[i] = transforms[i].gameObject.activeSelf;
            }
        }

        public void ResetState(Boxmon boxmon)
        {
            foreach (var t in boxmon.Transforms)
            {
                t.gameObject.SetActive(false);
            }
            for (int i = 0; i < transforms.Length; i++)
            {
                var t = boxmon.Transforms[i];

                t.localPosition = localPositions[i];
                t.localRotation = localRotations[i];
                t.localScale = localScales[i];

                t.gameObject.SetActive(activeStates[i]);

            }

            for (int i = 0; i < rigidbodies.Length; i++)
            {
                boxmon.Rigidbodies[i].isKinematic = false;
                boxmon.Rigidbodies[i].linearVelocity = Vector3.zero;
                boxmon.Rigidbodies[i].angularVelocity = Vector3.zero;
            }
        }
    }

}
