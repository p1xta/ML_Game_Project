using UnityEngine;
using System.Collections;

public class CarSpawner : MonoBehaviour
{
    private CarPoolManager carPool;  
    public float minSpawnTime = 1f;
    public float maxSpawnTime = 3f;

    public Transform[] lanes;  
    public float spawnZ = 50f; 

    void Start()
    {
        carPool = GetComponent<CarPoolManager>();

        StartCoroutine(SpawnCars()); 
    }

    IEnumerator SpawnCars()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(minSpawnTime, maxSpawnTime));

            GameObject car = carPool.GetRandomCar();
            if (car != null)
            {
                int laneIndex = Random.Range(0, lanes.Length);
                Vector3 spawnPos = lanes[laneIndex].localPosition;
                spawnPos.z = spawnZ;

                car.transform.localPosition = spawnPos;
                car.SetActive(true); 
            }
        }
    }
}
