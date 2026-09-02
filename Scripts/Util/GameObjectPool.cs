using Boxal.Game;
using System.Collections.Generic;
using UnityEngine;

namespace Boxal.Util
{
    public class GameObjectPool
    {
        #region Variables

        private Stack<GameObject> pool = new Stack<GameObject>();

        private GameObject prefab;
        private Transform parent;
        #endregion

        //생성자
        public GameObjectPool(GameObject prefab, int initialSize, Transform parent = null)
        {
            this.prefab = prefab;
            this.parent = parent;

            for (int i = 0; i < initialSize; i++)
            {
                Create();
            }
        }

        private GameObject Create()
        {
            GameObject obj = Object.Instantiate(prefab, parent);
            obj.SetActive(false);

            pool.Push(obj);
            return obj;
        }

        public GameObject Get()
        {
            if (pool.Count == 0)
            {
                Create();
            }

            GameObject obj = pool.Pop();

            obj.SetActive(true);
            return obj;
        }

        public void Release(GameObject obj)
        {
            obj.SetActive(false);
            obj.transform.SetParent(parent);
            pool.Push(obj);
        }
    }

}
