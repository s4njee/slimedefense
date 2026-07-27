using UnityEngine;

public class SpriteBillboard : MonoBehaviour
{
    Camera mainCamera;

    void Awake()
    {
        mainCamera = Camera.main;
    }

    void LateUpdate()
    {
        if (mainCamera != null)
        {
            transform.rotation = mainCamera.transform.rotation;
        }
    }
}