using UnityEngine;
using UnityEngine.InputSystem;

public class SphereKeyboardMovement : MonoBehaviour
{
    [SerializeField] private float speed = 5f;

    private void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        var movement = Vector3.zero;
        if (keyboard.wKey.isPressed) movement += Vector3.forward;
        if (keyboard.sKey.isPressed) movement += Vector3.back;
        if (keyboard.aKey.isPressed) movement += Vector3.left;
        if (keyboard.dKey.isPressed) movement += Vector3.right;

        if (movement.sqrMagnitude > 1f) movement.Normalize();
        transform.position += movement * speed * Time.deltaTime;
    }
}