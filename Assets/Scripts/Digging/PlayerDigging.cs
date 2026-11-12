using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(PlayerMovement), typeof(AudioSource))]
public sealed class PlayerDigging : MonoBehaviour
{
    private enum Phase { Ready, Windup, Strike, Pry, Anticipation, Reveal, Reset }

    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private DigSettings settings;
    [SerializeField] private Transform shovelGrip;
    [SerializeField] private DigSpot[] spots = System.Array.Empty<DigSpot>();

    [Header("Runtime inspection")]
    [SerializeField] private Phase phase;
    [SerializeField] private DigSpot activeSpot;
    [SerializeField] private DigResult lastResult;

    private Rigidbody body;
    private PlayerMovement movement;
    private AudioSource audioSource;
    private Vector3 restOffset;
    private Quaternion restRotation;
    private GameObject emergingPotato;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        movement = GetComponent<PlayerMovement>();
        audioSource = GetComponent<AudioSource>();
        if (settings == null || shovelGrip == null || interactAction?.action == null)
        {
            Debug.LogError("Assign Dig Settings, Shovel Grip and Interact Action.", this);
            enabled = false;
            return;
        }

        restOffset = shovelGrip.localPosition;
        restRotation = shovelGrip.localRotation;
    }

    private void OnEnable() => interactAction.action.Enable();

    private void OnDisable()
    {
        interactAction?.action?.Disable();
        StopAllCoroutines();
        if (activeSpot == null)
            return;

        activeSpot.Wiggle(0f);
        if (emergingPotato != null)
            Destroy(emergingPotato);
        RestoreControl();
    }

    private void Update()
    {
        if (interactAction.action.WasPressedThisFrame())
            TryDig();
    }

    public bool TryDig()
    {
        if (!isActiveAndEnabled || phase != Phase.Ready)
            return false;

        DigSpot target = FindNearestSpot();
        if (target == null)
            return false;

        StartCoroutine(Dig(target));
        return true;
    }

    private DigSpot FindNearestSpot()
    {
        float bestDistance = settings.reach * settings.reach;
        DigSpot nearest = null;
        foreach (DigSpot spot in spots)
        {
            if (spot == null || !spot.Available)
                continue;

            Vector3 offset = spot.ImpactPoint - transform.position;
            offset.y = 0f;
            if (offset.sqrMagnitude >= bestDistance)
                continue;

            bestDistance = offset.sqrMagnitude;
            nearest = spot;
        }
        return nearest;
    }

    private IEnumerator Dig(DigSpot target)
    {
        activeSpot = target;
        phase = Phase.Windup;
        movement.enabled = false;
        body.linearVelocity = Vector3.Project(body.linearVelocity, Vector3.up);
        Vector3 direction = target.ImpactPoint - body.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            body.rotation = Quaternion.LookRotation(direction);

        int requiredDigs = Mathf.Max(1, settings.digsRequired);
        do
        {
            yield return PerformStroke(target);
            if (target.CompletedDigs + 1 >= requiredDigs)
            {
                yield return Reveal(target);
                break;
            }

            phase = Phase.Reset;
            yield return MoveGrip(restOffset, restRotation, settings.resetSeconds);
            target.RecordDig();
        }
        while (interactAction.action.IsPressed());

        RestoreControl();
    }

    private IEnumerator PerformStroke(DigSpot target)
    {
        phase = Phase.Windup;
        Quaternion raised = Quaternion.Euler(settings.raisedAngles);
        Quaternion contact = Quaternion.Euler(settings.contactAngles);
        Quaternion lifted = Quaternion.Euler(settings.liftedAngles);
        yield return MoveGrip(settings.raisedOffset, raised, settings.windupSeconds);

        phase = Phase.Strike;
        Vector3 contactOffset = transform.InverseTransformPoint(target.ImpactPoint) -
            contact * settings.tipOffset;
        yield return MoveGrip(contactOffset, contact, settings.strikeSeconds);

        if (settings.dirtPrefab != null)
        {
            ParticleSystem dirt = Instantiate(settings.dirtPrefab, target.ImpactPoint, Quaternion.identity);
            dirt.Play();
        }
        if (settings.impactSound != null)
            audioSource.PlayOneShot(settings.impactSound, settings.impactVolume);

        phase = Phase.Pry;
        yield return MoveGrip(settings.liftedOffset, lifted, settings.prySeconds);
    }

    private IEnumerator Reveal(DigSpot target)
    {
        phase = Phase.Anticipation;
        for (float elapsed = 0f; elapsed < settings.anticipationSeconds; elapsed += Time.deltaTime)
        {
            float t = elapsed / settings.anticipationSeconds;
            target.Wiggle(Mathf.Sin(t * Mathf.PI * 6f) * (1f - t) * settings.moundWiggle);
            yield return null;
        }

        phase = Phase.Reveal;
        Transform reveal = target.RevealPoint;
        emergingPotato = Instantiate(target.Result.prefab, reveal.position, reveal.rotation);
        Vector3 finalPosition = reveal.position;
        for (float elapsed = 0f; elapsed < settings.revealSeconds; elapsed += Time.deltaTime)
        {
            float t = elapsed / settings.revealSeconds;
            float height = Mathf.Lerp(-0.42f, 0f, Mathf.SmoothStep(0f, 1f, t)) +
                Mathf.Sin(t * Mathf.PI) * settings.revealHop;
            emergingPotato.transform.position = finalPosition + Vector3.up * height;
            yield return null;
        }
        emergingPotato.transform.position = finalPosition;
        yield return MoveGrip(restOffset, restRotation, settings.resetSeconds);

        target.RecordDig();
        target.Complete();
        lastResult = target.Result;
        emergingPotato = null;
    }

    private IEnumerator MoveGrip(Vector3 offset, Quaternion rotation, float duration)
    {
        Vector3 startOffset = shovelGrip.localPosition;
        Quaternion startRotation = shovelGrip.localRotation;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            shovelGrip.SetLocalPositionAndRotation(
                Vector3.Lerp(startOffset, offset, t), Quaternion.Slerp(startRotation, rotation, t));
            yield return null;
        }
        shovelGrip.SetLocalPositionAndRotation(offset, rotation);
    }

    private void RestoreControl()
    {
        shovelGrip.SetLocalPositionAndRotation(restOffset, restRotation);
        activeSpot = null;
        phase = Phase.Ready;
        movement.enabled = true;
    }

    private void OnGUI()
    {
        if (phase != Phase.Ready)
            return;

        DigSpot target = FindNearestSpot();
        if (target == null)
            return;

        string prompt = interactAction.action.GetBindingDisplayString(0) + "  Dig";
        if (settings.digsRequired > 1)
            prompt = $"Hold {prompt}  ({target.CompletedDigs}/{settings.digsRequired})";
        GUI.Box(new Rect((Screen.width - 140) * 0.5f, Screen.height - 66, 140, 28), prompt);
    }
}

