using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SpawnManager : MonoBehaviour
{
    RoadSpawner roadSpawner;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        roadSpawner = GetComponent<RoadSpawner>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void SpawnTriggerEntered()
    {
        roadSpawner.MoveRoad();
    }
}
