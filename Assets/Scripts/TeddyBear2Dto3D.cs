using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Attach to the TeddyBear child inside Frame 2.
/// Manages the flat→3D visual transition as the frame is enlarged,
/// and handles grab once fully 3D.
/// </summary>
public class TeddyBear2Dto3D : MonoBehaviour
{
    [Header("=== References ===")]
    [Tooltip("The 3D teddy bear model. Assign after importing the model. A Cube placeholder is created by default.")]
    public GameObject teddyModel;

    [Header("=== Settings ===")]
    public float flatZ = 0.01f;

    public event System.Action OnTeddyGrabbed;

    private Vector3 _baseScale = Vector3.one;
    private XRGrabInteractable _grab;
    private bool _grabbable = false;
    private bool _grabbed = false;

    void Awake()
    {
        if (teddyModel == null)
            teddyModel = gameObject;

        // Start flat. Preserve the authored X/Y scale (the teddy is sized to overlay the
        // drawn teddy in the frame art) — only Z is flattened.
        _baseScale = teddyModel.transform.localScale;
        teddyModel.transform.localScale = new Vector3(_baseScale.x, _baseScale.y, _baseScale.z * flatZ);

        // At t=0 the DRAWN teddy in the frame art is the 2D teddy. The 3D model stays
        // invisible until the first fold push, then materializes and inflates out of the
        // drawing — otherwise a flat 3D 'sticker' floats misaligned over the art.
        SetRenderersVisible(false);

        _grab = GetComponent<XRGrabInteractable>();
        if (_grab == null) _grab = GetComponentInChildren<XRGrabInteractable>();

        if (_grab != null)
        {
            _grab.enabled = false;   // disabled until fully 3D
            _grab.selectEntered.AddListener(OnGrabbed);
            _grab.selectExited.AddListener(OnReleased);
        }
    }

    /// <summary>
    /// Called by FrameEnlarger with t = 0..1 (0=flat, 1=fully 3D).
    /// </summary>
    public void UpdateTransitionState(float t)
    {
        float zScale = Mathf.Lerp(flatZ, 1f, t);
        teddyModel.transform.localScale = new Vector3(_baseScale.x, _baseScale.y, _baseScale.z * zScale);

        // materialize once the transition starts (the drawn teddy 'comes to life')
        SetRenderersVisible(t > 0.01f);

        if (t >= 0.99f && !_grabbable)
        {
            _grabbable = true;
            if (_grab != null) _grab.enabled = true;
            Debug.Log("[TeddyBear2Dto3D] Teddy is now fully 3D and grabbable!");
        }
    }

    /// <summary>
    /// Placed down: the teddy stays where the player left it (kinematic, no gravity)
    /// and turns to FACE the player — pick it up again any time.
    /// </summary>
    void OnReleased(UnityEngine.XR.Interaction.Toolkit.SelectExitEventArgs args)
    {
        if (!_grabbed) return;
        var rb = GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
        StartCoroutine(FacePlayer());
    }

    System.Collections.IEnumerator FacePlayer()
    {
        var cam = Camera.main;
        if (cam == null) yield break;
        Vector3 look = cam.transform.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude < 0.001f) yield break;
        Quaternion from = transform.rotation;
        Quaternion to = Quaternion.LookRotation(look.normalized); // model faces +Z at yaw 180 = toward player already handled by look dir
        float e = 0f, dur = 0.5f;
        while (e < dur)
        {
            e += Time.deltaTime;
            transform.rotation = Quaternion.Slerp(from, to, e / dur);
            yield return null;
        }
    }

    void SetRenderersVisible(bool on)
    {
        foreach (var r in teddyModel.GetComponentsInChildren<Renderer>(true))
            r.enabled = on;
    }

    [Tooltip("World scale of the teddy once carried. The frame is ~3x enlarged at grab time and the teddy inherits that — without normalizing, the player would hold a 2m bear.")]
    public float carriedScale = 0.45f;

    void OnGrabbed(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
    {
        if (_grabbed) return;
        // With a TeddyPullOut, grabbing alone isn't enough — the teddy must be TORN free
        // (3 tugs). TeddyPullOut calls CompleteGrab() when that happens.
        var pull = GetComponent<TeddyPullOut>();
        if (pull != null && !pull.IsFree) return;
        CompleteGrab();
    }

    /// <summary>The teddy is truly free: detach, shrink to carry size, advance the story.</summary>
    public void CompleteGrab()
    {
        if (_grabbed) return;
        _grabbed = true;

        // Detach from Frame1 so the frame doesn't follow the player's hand
        transform.SetParent(null);

        // Shrink from inherited frame scale (~3x) down to a carryable teddy
        StartCoroutine(ShrinkToCarrySize());

        SfxPlayer.Play("grab_pop", transform.position);
        Debug.Log("[TeddyBear2Dto3D] Teddy grabbed — detached from Frame1.");
        OnTeddyGrabbed?.Invoke();

        DialogueSystem.Instance?.ShowDialogue(GameDialogue.TeddyGrabbedDialogue);
    }

    System.Collections.IEnumerator ShrinkToCarrySize()
    {
        Vector3 from = transform.localScale;
        Vector3 to = Vector3.one * carriedScale;
        float dur = 0.45f, t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(from, to, t / dur);
            yield return null;
        }
        transform.localScale = to;
    }
}
