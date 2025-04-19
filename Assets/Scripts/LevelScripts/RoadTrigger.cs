using UnityEngine;

public class RoadTrigger : MonoBehaviour
{
    public SpawnManager spawnManager;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Agent"))
        {
            spawnManager.SpawnTriggerEntered();
        }
    }
}
