using UnityEngine;

[CreateAssetMenu(menuName = "Potatoes/Dig Settings")]
public sealed class DigSettings : ScriptableObject
{
    [Header("Interaction")]
    [Min(0.1f)] public float reach = 1.65f;
    [Min(1)] [Tooltip("Completed shovel strokes required to uncover one potato.")]
    public int digsRequired = 1;

    [Header("Shovel pose")]
    public Vector3 tipOffset = new(0f, -1.06f, 0f);
    public Vector3 raisedOffset = new(0.49f, 0.69f, 0.33f);
    public Vector3 raisedAngles = new(-48f, 0f, -18f);
    public Vector3 contactAngles = new(27f, 0f, -12f);
    public Vector3 liftedOffset = new(0.42f, 0.17f, 0.79f);
    public Vector3 liftedAngles = new(-65f, 0f, -15f);
    [Min(0.01f)] public float windupSeconds = 0.23f;
    [Min(0.01f)] public float strikeSeconds = 0.14f;
    [Min(0.01f)] public float prySeconds = 0.25f;
    [Min(0.01f)] public float resetSeconds = 0.18f;

    [Header("Impact and anticipation")]
    public ParticleSystem dirtPrefab;
    public AudioClip impactSound;
    [Range(0f, 1f)] public float impactVolume = 0.65f;
    [Min(0.01f)] public float anticipationSeconds = 0.32f;
    [Min(0f)] public float moundWiggle = 0.08f;

    [Header("Reveal")]
    [Min(0.01f)] public float revealSeconds = 0.38f;
    [Min(0f)] public float revealHop = 0.32f;
}

