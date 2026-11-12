using UnityEngine;

public sealed class DigSpot : MonoBehaviour
{
    [SerializeField] private Transform mound;
    [SerializeField] private Transform impactPoint;
    [SerializeField] private Transform revealPoint;
    [SerializeField] private DigResult fixedResult;
    [SerializeField] private bool excavated;

    public int CompletedDigs { get; private set; }

    private Vector3 moundRestPosition;

    public bool Available => !excavated && mound != null && impactPoint != null &&
        revealPoint != null && fixedResult != null && fixedResult.prefab != null;
    public Vector3 ImpactPoint => impactPoint.position;
    public Transform RevealPoint => revealPoint;
    public DigResult Result => fixedResult;

    private void Awake()
    {
        if (mound != null)
            moundRestPosition = mound.localPosition;
    }

    public void Wiggle(float distance) =>
        mound.localPosition = moundRestPosition + Vector3.right * distance;

    public void RecordDig() => CompletedDigs++;

    public void Complete()
    {
        Wiggle(0f);
        excavated = true;
        mound.gameObject.SetActive(false);
    }
}

