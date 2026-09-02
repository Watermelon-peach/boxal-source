using Boxal.Util;
using System.Collections.Generic;
using UnityEngine;

namespace Boxal.Game
{
    public class SpawnManager : Singleton<SpawnManager>
    {
        #region Variables
        public List<Boxmon> aliveBoxmons;
        public int currentEnemies = 0;
        [SerializeField] private GameObject boxmonPrefab;

        private float yOffset = 2f;
        private Transform lastSpawned;
        private float initOffset = 20f;

        private GameObjectPool pool;
        private TransformSnapshot snapshot;
        #endregion


        #region Unity Event Methods
        private void Start()
        {
            pool = new GameObjectPool(boxmonPrefab, 20, transform);
            aliveBoxmons = new List<Boxmon>();
            snapshot = new TransformSnapshot();
            snapshot.SaveSnapshot(boxmonPrefab);
        }

        #endregion
        
        #region Custom Methods
        public Boxmon Spawn(long maxHp, bool isBoss = false, bool isKingBoss = false)
        {
            currentEnemies++;
            GameObject obj = pool.Get();

            //Boxmon 개별 체력 설정
            Boxmon boxmon = obj.GetComponent<Boxmon>();
            boxmon.MaxHp = maxHp;
            boxmon.ResetBox();
            // ResetBox가 등급/크기/색을 기본으로 되돌리므로 그 다음에 등급 적용
            boxmon.ConfigureTier(isBoss, isKingBoss);


            Vector3 spawnPos;

            if (lastSpawned == null)
            {
                // 첫 스폰이면 기본 위치 + 20f
                spawnPos = Player.Instance.transform.position + Vector3.up * initOffset;
            }
            else
            {
                // 마지막 오브젝트 위치 기준 + y
                spawnPos = lastSpawned.position + Vector3.up * yOffset;
            }

            obj.transform.position = spawnPos;
            lastSpawned = obj.transform;

            aliveBoxmons.Add(boxmon);
            return boxmon;
        }

        /// <summary>씬에 남은 몬스터를 전부 정리하고 스폰 상태를 초기화한다. 게임 재시작용.</summary>
        public void ResetSpawns()
        {
            // 파괴 연출 중인 개체까지 포함해 활성 Boxmon 전부 회수
            // (pool.Release의 SetActive(false)가 진행 중인 코루틴도 자동 중단)
            Boxmon[] all = GetComponentsInChildren<Boxmon>(true);
            foreach (Boxmon boxmon in all)
            {
                if (!boxmon.gameObject.activeSelf)
                    continue;

                snapshot.ResetState(boxmon);
                pool.Release(boxmon.gameObject);
            }

            aliveBoxmons.Clear();
            currentEnemies = 0;
            lastSpawned = null;
        }

        public void Despawn(Boxmon boxmon)
        {
            currentEnemies--;
            if (currentEnemies == 0) lastSpawned = null;
            snapshot.ResetState(boxmon);
            pool.Release(boxmon.gameObject);
        }
        #endregion

    }

}
