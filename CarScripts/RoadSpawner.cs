using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class RoadSpawner : MonoBehaviour
{
    public List<GameObject> roads;
    private float offset = 83.9f;
    public float speed = 10f;

    void Start()
    {
        if(roads != null && roads.Count > 0)
        {
            roads = roads.OrderBy(r => r.transform.localPosition.z).ToList();
        }
    }

    void Update()
    {
        foreach (var road in roads)
        {
            road.transform.Translate(Vector3.back * speed * Time.deltaTime);
        }
    }

    public void MoveRoad()
    {
        GameObject movedRoad = roads[0];
        roads.Remove(movedRoad);
        float newZ = roads[roads.Count - 1].transform.localPosition.z + offset;

        Vector3 currentPos = movedRoad.transform.localPosition;
        movedRoad.transform.localPosition = new Vector3(currentPos.x, currentPos.y, newZ);

        roads.Add(movedRoad);
    }
}
