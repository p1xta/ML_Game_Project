using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CarMover : MonoBehaviour
{
    public float speed = 10f;
    public float deactivateZ = -5.1f;

    void Update()
    {
        transform.Translate(Vector3.forward * speed * Time.deltaTime);

        if (transform.localPosition.z < deactivateZ)
        {
            gameObject.SetActive(false);
        }
    }
}
