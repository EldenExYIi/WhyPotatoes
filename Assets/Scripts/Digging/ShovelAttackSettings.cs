using UnityEngine;

[CreateAssetMenu(menuName = "Potatoes/Shovel Attack Settings")]
public sealed class ShovelAttackSettings : ScriptableObject
{
    [Header("Animation")]
    public AnimationClip swing;
    [Min(0.01f)] public float playbackSpeed = 1f;
    [Min(0f)] public float hitStopSeconds = 0.06f;

    [Header("Hit detection")]
    public Vector3 bladeTipOffset = new(0f, -1.06f, 0f);
    public LayerMask hitLayers = Physics.AllLayers;
    [Range(0f, 1f)] public float hitStart = 0.25f;
    [Range(0f, 1f)] public float hitEnd = 0.65f;
    [Min(0f)] public float hitRadius = 0.22f;
    [Min(0)] public int damage = 1;

    [Header("Knockback")]
    [Min(0f)] public float knockbackSpeed = 4f;
    [Min(0f)] public float launchSpeed = 3.5f;

    [Header("Hit sound")]
    public AudioClip hitSound;
    [Range(0f, 1f)] public float hitVolume = 0.65f;

    [Header("Hit effect")]
    [Tooltip("Leave empty to disable particles. The prefab emits along its local +Z axis.")]
    public ParticleSystem hitEffect;
    [Min(0.01f)] public float hitEffectLifetime = 1f;

    [Header("Camera shake")]
    [Min(0f)] public float shakeAmplitude = 0.045f;
    [Min(0f)] public float shakeSeconds = 0.14f;
    [Min(0f)] public float shakeFrequency = 24f;

    private void OnValidate()
    {
        hitEnd = Mathf.Max(hitStart, hitEnd);
    }
}
