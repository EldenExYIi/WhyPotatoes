using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public sealed class PotatoRoll : MonoBehaviour
{
    public int CurrentHealth { get; private set; }

    public void Begin(Vector3 direction, DigResult result, Collider playerCollider)
    {
        CurrentHealth = Mathf.Max(1, result.maxHealth);
        StartCoroutine(ActivatePhysics(direction, result.rollSpeed, result.rollDelaySeconds, playerCollider));
    }

    public bool TakeDamage(int damage)
    {
        CurrentHealth = Mathf.Max(0, CurrentHealth - Mathf.Max(0, damage));
        if (CurrentHealth > 0)
            return false;

        gameObject.SetActive(false);
        Destroy(gameObject);
        return true;
    }

    private IEnumerator ActivatePhysics(Vector3 direction, float speed, float delay, Collider playerCollider)
    {
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        var body = GetComponent<Rigidbody>();
        var sphere = GetComponent<SphereCollider>();
        sphere.enabled = true;

        // The potato may have emerged inside the player's capsule.
        bool overlapsPlayer = OverlapsPlayer(sphere, playerCollider);
        if (overlapsPlayer)
            Physics.IgnoreCollision(sphere, playerCollider);

        body.isKinematic = false;
        body.linearVelocity = direction * speed;
        float radius = Mathf.Max(0.01f, sphere.bounds.extents.y);
        body.angularVelocity = Vector3.Cross(Vector3.up, direction) * (speed / radius);

        if (!overlapsPlayer)
            yield break;

        var fixedStep = new WaitForFixedUpdate();
        do
        {
            yield return fixedStep;
        }
        while (OverlapsPlayer(sphere, playerCollider));

        if (playerCollider != null)
            Physics.IgnoreCollision(sphere, playerCollider, false);
    }

    private static bool OverlapsPlayer(SphereCollider sphere, Collider player)
    {
        return player != null && player.enabled && player.gameObject.activeInHierarchy &&
            Physics.ComputePenetration(
                sphere, sphere.transform.position, sphere.transform.rotation,
                player, player.transform.position, player.transform.rotation,
                out _, out _);
    }
}
