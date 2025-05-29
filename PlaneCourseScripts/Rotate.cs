using UnityEngine;


namespace Aircraft
{
    public class Rotate : MonoBehaviour
    {
        [Tooltip("The speed at which to rotate")]
        public Vector3 rotateSpeed;

        [Tooltip("Whether to randomize the start position")]
        public bool randomize = false;

        void Start()
        {
            if (randomize) transform.Rotate(rotateSpeed.normalized * UnityEngine.Random.Range(0f, 360f));
        }

        void Update()
        {
            transform.Rotate(rotateSpeed * Time.deltaTime, Space.Self);
        }
    }
}