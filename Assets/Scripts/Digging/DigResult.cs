using UnityEngine;

[CreateAssetMenu(menuName = "Potatoes/Dig Result")]
public sealed class DigResult : ScriptableObject
{
    public GameObject prefab;

    [Header("Potato health")]
    [Min(1)] public int maxHealth = 3;

    [Header("Optional roll")]
    [Min(0f)] public float rollSpeed;
    [Min(0f)] public float rollDelaySeconds = 0.35f;
}

