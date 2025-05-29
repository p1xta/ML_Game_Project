using UnityEngine;
using System.Collections.Generic;

public class CarPoolManager : MonoBehaviour
{
    public GameObject[] carPrefabs;
    public int poolSizePerType = 5;

    public Transform carParent; 

    private List<GameObject> carPool = new List<GameObject>();

    void Start()
    {
        foreach (GameObject prefab in carPrefabs)
        {
            for (int i = 0; i < poolSizePerType; i++)
            {
                GameObject car = Instantiate(prefab, carParent);
                car.SetActive(false);
                carPool.Add(car);
            }
        }
    }

    public GameObject GetRandomCar()
    {
        List<GameObject> inactiveCars = carPool.FindAll(c => !c.activeInHierarchy);
        if (inactiveCars.Count == 0) return null;

        int index = Random.Range(0, inactiveCars.Count);
        return inactiveCars[index];
    }
}
