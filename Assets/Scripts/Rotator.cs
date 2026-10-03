using UnityEngine;

/// <summary>Spins its transform around an axis at a constant rate (the lighthouse lens and its beams).</summary>
public class Rotator : MonoBehaviour
{
    public Vector3 Axis = Vector3.up;
    public float DegreesPerSecond = 30f;

    void Update() => transform.Rotate(Axis, DegreesPerSecond * Time.deltaTime, Space.World);
}
