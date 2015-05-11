using System;
using System.Collections.Generic;
using UnityEngine;

public class ObjectPooler : MonoBehaviour
{
    public static ObjectPooler Current { get; private set; }

    public int pooledAmount;
    public bool willGrow = true;

    private readonly Dictionary<GameObject, IList<GameObject>> _objectPools =
        new Dictionary<GameObject, IList<GameObject>>();

    private void Awake()
    {
        Current = this;
    }

    public void CreatePoolForObject(GameObject poolObject)
    {
        CreatePoolForObject(poolObject, pooledAmount);
    }

    public void CreatePoolForObject(GameObject poolObject, int initialPoolSize)
    {
        InternalCreatePoolForObject(poolObject, initialPoolSize);
    }

    private IList<GameObject> InternalCreatePoolForObject(GameObject poolObject, int initialPoolSize)
    {
        if (_objectPools.ContainsKey(poolObject))
        {
            throw new InvalidOperationException("Pool already exists for this GameObject.");
        }
        var pool = new List<GameObject>();
        for (var i = 0; i < initialPoolSize; i++)
        {
            CreateAndAddObjectToPool(poolObject, pool);
        }
        _objectPools.Add(poolObject, pool);
        return pool;
    }

    public GameObject GetPooledObject(GameObject poolObject)
    {
        IList<GameObject> pool;
        if (!_objectPools.TryGetValue(poolObject, out pool))
        {
            Debug.LogWarning("Pool not created for object. Creating one now.");
            pool = InternalCreatePoolForObject(poolObject, pooledAmount);
        }

        for (var i = 0; i < pool.Count; i++)
        {
            if (!pool[i].activeInHierarchy)
            {
                return pool[i];
            }
        }

        if (willGrow)
        {
            return CreateAndAddObjectToPool(poolObject, pool);
        }

        return null;
    }

    private GameObject CreateAndAddObjectToPool(GameObject poolObject, IList<GameObject> pool)
    {
        var obj = Instantiate(poolObject);
        obj.transform.parent = gameObject.transform;
        obj.SetActive(false);
        pool.Add(obj);
        return obj;
    }
}