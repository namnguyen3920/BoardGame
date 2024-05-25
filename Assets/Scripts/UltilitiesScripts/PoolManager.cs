using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoolManager : Singleton_Mono_Method<PoolManager>
{
    [SerializeField] List<Transform> DeadEnemiesPool = new();
    [SerializeField] List<Transform> CoinPool = new();

    public void DeadEnemiesCollector(Transform dead_enemy)
    {
        ReturnObjToPool(dead_enemy, DeadEnemiesPool);
    }

    public void CoinCollected(Transform coin_prefab)
    {
        ReturnObjToPool(coin_prefab, CoinPool);
    }

    public int GetCoinAmount()
    {
        return CoinPool.Count;
    }

    void ReturnObjToPool(Transform d_obj, List<Transform> pool)
    {
        pool.Add(d_obj);
        InactiveObj(pool);
    }

    void InactiveObj(List<Transform> pool)
    {
        foreach (Transform e in pool)
        {
            e.gameObject.SetActive(false);
        }
    }
}
