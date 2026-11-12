using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody), typeof(PlayerMovement), typeof(AudioSource))]
public sealed class PlayerDigging : MonoBehaviour
{
    private enum Phase { Ready, Windup, Strike, Pry, Anticipation, Reveal, Reset, Attack }
    private const float SwingImpactNormalizedTime = 0.5f;

    [SerializeField] private InputActionReference interactAction;
    [SerializeField] private InputActionReference attackAction;
    [SerializeField] private DigSettings settings;
    [SerializeField] private ShovelAttackSettings attackSettings;
    [SerializeField] private Transform shovelGrip;
    [SerializeField] private AnimationClip shovelSwing;
    [SerializeField] private DigSpot[] spots = System.Array.Empty<DigSpot>();

    [Header("Runtime inspection")]
    [SerializeField] private Phase phase;
    [SerializeField] private DigSpot activeSpot;
    [SerializeField] private DigResult lastResult;

    private Rigidbody body;
    private PlayerMovement movement;
    private AudioSource audioSource;
    private CameraMovement cameraMovement;
    private Vector3 restOffset;
    private Quaternion restRotation;
    private GameObject emergingPotato;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        movement = GetComponent<PlayerMovement>();
        audioSource = GetComponent<AudioSource>();
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
            cameraMovement = mainCamera.GetComponent<CameraMovement>();
        if (settings == null || shovelGrip == null || interactAction?.action == null)
        {
            Debug.LogError("Assign Dig Settings, Shovel Grip and Interact Action.", this);
            enabled = false;
            return;
        }

        restOffset = shovelGrip.localPosition;
        restRotation = shovelGrip.localRotation;
    }

    private void OnEnable()
    {
        interactAction.action.Enable();
        attackAction?.action?.Enable();
    }

    private void OnDisable()
    {
        interactAction?.action?.Disable();
        attackAction?.action?.Disable();
        StopAllCoroutines();
        if (phase == Phase.Attack)
            ResetShovel();
        if (activeSpot == null)
            return;

        activeSpot.Wiggle(0f);
        if (emergingPotato != null)
            Destroy(emergingPotato);
        RestoreControl();
    }

    private void Update()
    {
        if (phase != Phase.Ready)
            return;
        if (interactAction.action.WasPressedThisFrame() && TryDig())
            return;
        if (attackSettings != null && attackSettings.swing != null &&
            attackAction?.action?.WasPressedThisFrame() == true)
            StartCoroutine(Attack(attackSettings));
    }

    private IEnumerator Attack(ShovelAttackSettings attack)
    {
        phase = Phase.Attack;
        var hitPotatoes = new HashSet<PotatoRoll>();
        AnimationClip clip = attack.swing;
        Pose clipStart = SampleSwing(clip, 0f);
        Quaternion gripRotation = restRotation * Quaternion.Inverse(clipStart.rotation);
        shovelGrip.SetLocalPositionAndRotation(restOffset, restRotation);
        Vector3 previousTip = shovelGrip.TransformPoint(attack.bladeTipOffset);
        float previousProgress = 0f;
        float duration = clip.length;

        for (float elapsed = 0f; elapsed < duration;)
        {
            yield return null;
            elapsed += Time.deltaTime * Mathf.Max(0.01f, attack.playbackSpeed);
            float progress = Mathf.Clamp01(elapsed / duration);
            Pose sample = SampleSwing(clip, progress * duration);
            shovelGrip.SetLocalPositionAndRotation(restOffset + sample.position - clipStart.position,
                gripRotation * sample.rotation);
            Vector3 tip = shovelGrip.TransformPoint(attack.bladeTipOffset);

            // Sweep only the part of this frame's blade path inside the strike window.
            if (progress >= attack.hitStart && previousProgress <= attack.hitEnd)
            {
                float from = Mathf.InverseLerp(previousProgress, progress, attack.hitStart);
                float to = Mathf.InverseLerp(previousProgress, progress, attack.hitEnd);
                bool hit = HitPotatoes(Vector3.Lerp(previousTip, tip, from),
                    Vector3.Lerp(previousTip, tip, to), hitPotatoes, attack);
                if (hit && attack.hitStopSeconds > 0f)
                {
                    yield return new WaitForSeconds(attack.hitStopSeconds);
                    // The player can move during the pause, so resume from the current blade position.
                    tip = shovelGrip.TransformPoint(attack.bladeTipOffset);
                }
            }

            previousTip = tip;
            previousProgress = progress;
        }

        ResetShovel();
    }

    private bool HitPotatoes(Vector3 from, Vector3 to, HashSet<PotatoRoll> hitPotatoes, ShovelAttackSettings attack)
    {
        int previousHitCount = hitPotatoes.Count;
        foreach (Collider hit in Physics.OverlapCapsule(from, to, attack.hitRadius,
                     attack.hitLayers, QueryTriggerInteraction.Ignore))
        {
            Rigidbody target = hit.attachedRigidbody;
            if (target == null || target.isKinematic || !target.TryGetComponent(out PotatoRoll potato) ||
                potato.CurrentHealth <= 0 || !hitPotatoes.Add(potato))
                continue;

            Vector3 away = Vector3.ProjectOnPlane(target.worldCenterOfMass - body.position, Vector3.up).normalized;
            if (away == Vector3.zero)
                away = transform.forward;
            if (attack.hitEffect != null)
                PlayHitEffect(hit, from, to, -away, attack);
            if (!potato.TakeDamage(attack.damage))
                target.AddForce(away * attack.knockbackSpeed + Vector3.up * attack.launchSpeed, ForceMode.VelocityChange);
        }

        if (hitPotatoes.Count == previousHitCount)
            return false;

        if (attack.hitSound != null && attack.hitVolume > 0f)
            audioSource.PlayOneShot(attack.hitSound, attack.hitVolume);
        if (cameraMovement != null)
            cameraMovement.Shake(attack.shakeAmplitude, attack.shakeSeconds, attack.shakeFrequency);
        return true;
    }

    private void PlayHitEffect(Collider hit, Vector3 from, Vector3 to, Vector3 fallbackNormal,
        ShovelAttackSettings attack)
    {
        Bounds bounds = hit.bounds;
        Vector3 sweep = to - from;
        float sweepLengthSquared = sweep.sqrMagnitude;
        float fraction = sweepLengthSquared > 0f
            ? Mathf.Clamp01(Vector3.Dot(bounds.center - from, sweep) / sweepLengthSquared)
            : 0f;
        Vector3 normal = (from + sweep * fraction - bounds.center).normalized;
        if (normal == Vector3.zero)
            normal = fallbackNormal;

        // Query from outside the collider so the effect spawns on its surface even on a deep overlap.
        Vector3 contact = hit.ClosestPoint(bounds.center + normal * (bounds.extents.magnitude + 0.01f));
        Quaternion rotation = Quaternion.LookRotation(normal + Vector3.up * 0.35f);
        ParticleSystem effect = Instantiate(attack.hitEffect, contact, rotation);
        effect.Play(true);
        Destroy(effect.gameObject, attack.hitEffectLifetime);
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
            if (offset.sqrMagnitude >= bestDistance || IsObstructed(spot))
                continue;

            bestDistance = offset.sqrMagnitude;
            nearest = spot;
        }
        return nearest;
    }

    private bool IsObstructed(DigSpot spot)
    {
        Vector3 end = spot.ImpactPoint + Vector3.up * settings.obstructionProbeOffset;
        Vector3 start = transform.position;
        start.y = end.y;
        return Physics.Linecast(start, end, settings.obstructionLayers, QueryTriggerInteraction.Ignore);
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
        while (interactAction.action.IsPressed() && !IsObstructed(target));

        RestoreControl();
    }

    private IEnumerator PerformStroke(DigSpot target)
    {
        phase = Phase.Windup;
        Quaternion contact = Quaternion.Euler(settings.contactAngles);
        Quaternion lifted = Quaternion.Euler(settings.liftedAngles);

        if (shovelSwing == null)
        {
            Quaternion raised = Quaternion.Euler(settings.raisedAngles);
            yield return MoveGrip(settings.raisedOffset, raised, settings.windupSeconds);
            phase = Phase.Strike;
        }

        Vector3 contactOffset = transform.InverseTransformPoint(target.ImpactPoint) -
            contact * settings.tipOffset;
        float impactTime = shovelSwing != null ? shovelSwing.length * SwingImpactNormalizedTime : 0f;
        if (shovelSwing != null)
            yield return PlaySwing(0f, impactTime, new Pose(contactOffset, contact),
                settings.windupSeconds + settings.strikeSeconds, settings.windupSeconds);
        else
            yield return MoveGrip(contactOffset, contact, settings.strikeSeconds);

        if (settings.dirtPrefab != null)
        {
            ParticleSystem dirt = Instantiate(settings.dirtPrefab, target.ImpactPoint, Quaternion.identity);
            dirt.Play();
        }
        if (settings.impactSound != null)
            audioSource.PlayOneShot(settings.impactSound, settings.impactVolume);

        phase = Phase.Pry;
        if (shovelSwing != null)
            yield return PlaySwing(impactTime, shovelSwing.length,
                new Pose(settings.liftedOffset, lifted), settings.prySeconds);
        else
            yield return MoveGrip(settings.liftedOffset, lifted, settings.prySeconds);
    }

    private IEnumerator PlaySwing(float from, float to, Pose target, float duration,
        float strikeStartsAt = float.PositiveInfinity)
    {
        var start = new Pose(shovelGrip.localPosition, shovelGrip.localRotation);
        Pose clipStart = SampleSwing(shovelSwing, from);
        Pose clipEnd = SampleSwing(shovelSwing, to);

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (elapsed >= strikeStartsAt)
                phase = Phase.Strike;

            float t = elapsed / duration;
            Pose sample = SampleSwing(shovelSwing, Mathf.Lerp(from, to, t));
            // Retarget the clip's endpoints to the grip and mound while keeping its motion between them.
            Vector3 clipOffset = sample.position - Vector3.Lerp(clipStart.position, clipEnd.position, t);
            Quaternion clipRotation = Quaternion.Inverse(Quaternion.Slerp(clipStart.rotation, clipEnd.rotation, t)) *
                sample.rotation;
            float blend = Mathf.SmoothStep(0f, 1f, t);
            shovelGrip.SetLocalPositionAndRotation(
                Vector3.Lerp(start.position, target.position, blend) + clipOffset,
                Quaternion.Slerp(start.rotation, target.rotation, blend) * clipRotation);
            yield return null;
        }

        shovelGrip.SetLocalPositionAndRotation(target.position, target.rotation);
    }

    private Pose SampleSwing(AnimationClip clip, float time)
    {
        // The clip binds to ShovelPivot, a direct child of the player.
        clip.SampleAnimation(gameObject, time);
        return new Pose(shovelGrip.localPosition, shovelGrip.localRotation);
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
        Vector3 finalPosition = reveal.position;
        Quaternion finalRotation = reveal.rotation;
        emergingPotato = Instantiate(target.Result.prefab, finalPosition, finalRotation);
        yield return AnimateReveal(emergingPotato.transform, finalPosition, finalRotation);
        yield return MoveGrip(restOffset, restRotation, settings.resetSeconds);

        target.RecordDig();
        target.Complete();
        lastResult = target.Result;
        if (emergingPotato.TryGetComponent(out PotatoRoll roll))
        {
            Vector3 direction = Vector3.ProjectOnPlane(finalPosition - body.position, Vector3.up).normalized;
            if (direction == Vector3.zero)
                direction = transform.forward;
            roll.Begin(direction, lastResult, GetComponent<Collider>());
        }
        emergingPotato = null;
    }

    private IEnumerator AnimateReveal(Transform potato, Vector3 landingPosition, Quaternion landingRotation)
    {
        const float liftEnd = 0.26f;
        const float flipEnd = 0.82f;
        Vector3 pivotOffset = potato.TryGetComponent(out MeshFilter mesh) && mesh.sharedMesh != null
            ? potato.TransformVector(mesh.sharedMesh.bounds.center)
            : Vector3.zero;
        Vector3 flipAxis = landingRotation * Vector3.right;
        float duration = settings.revealSeconds;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = elapsed / duration;
            float lift = Mathf.SmoothStep(-0.42f, settings.revealHoverHeight, Mathf.InverseLerp(0f, liftEnd, t));
            float land = Mathf.SmoothStep(0f, settings.revealHoverHeight, Mathf.InverseLerp(flipEnd, 1f, t));
            float turn = Mathf.SmoothStep(0f, 360f, Mathf.InverseLerp(liftEnd, flipEnd, t));
            Quaternion flip = Quaternion.AngleAxis(-turn, flipAxis);
            Vector3 position = landingPosition + Vector3.up * (lift - land) + pivotOffset - flip * pivotOffset;
            potato.SetPositionAndRotation(position, flip * landingRotation);
            yield return null;
        }

        potato.SetPositionAndRotation(landingPosition, landingRotation);
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
        ResetShovel();
        activeSpot = null;
        movement.enabled = true;
    }

    private void ResetShovel()
    {
        shovelGrip.SetLocalPositionAndRotation(restOffset, restRotation);
        phase = Phase.Ready;
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

