using UnityEngine;

public sealed class DigSpot : MonoBehaviour
{
    [SerializeField] private Transform mound;
    [SerializeField] private GameObject dugSoil;
    [SerializeField] private Transform revealPoint;

    public int CompletedDigs { get; private set; }

    private Vector3 moundRestPosition;

    public bool Excavated { get; private set; }
    public Vector3 ImpactPoint => transform.position;
    public Transform RevealPoint => revealPoint;

    private void Awake()
    {
        if (mound != null)
            moundRestPosition = mound.localPosition;
    }

    public void Wiggle(float distance) =>
        mound.localPosition = moundRestPosition + Vector3.right * distance;

    public void ShowDisturbedSoil() => mound.gameObject.SetActive(true);

    public void RecordDig() => CompletedDigs++;

    public void Complete()
    {
        Wiggle(0f);
        Excavated = true;
        mound.gameObject.SetActive(false);
        dugSoil.SetActive(true);
    }
}
