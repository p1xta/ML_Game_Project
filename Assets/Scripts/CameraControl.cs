using System;
using UnityEngine;

public class CameraControl : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float mouseSensitivity = 2f;
    public Transform player;

    private float xRotation = 0f;
    private float yRotation = 0f;

    void Start() {}

    void Update()
    {
        if (GameState.IsGameActive)
        {
            // rotation
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -90f, 90f);
            yRotation += mouseX;

            transform.localRotation = Quaternion.Euler(xRotation, yRotation, 0f);
            player.Rotate(Vector3.up * mouseX);
            // movement
            float moveX = Input.GetAxis("Horizontal");
            float moveZ = Input.GetAxis("Vertical");

            Vector3 move = transform.right * moveX + transform.forward * moveZ;
            transform.position += move * moveSpeed * Time.deltaTime;
        }
    }
    public void LockCursor(bool isLocked)
    {
        Cursor.lockState = isLocked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !isLocked;
    }
}
