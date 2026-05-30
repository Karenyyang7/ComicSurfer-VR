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

    private XRGrabInteractable _grab;
    private bool _grabbable = false;
    private bool _grabbed = false;

    void Awake()
    {
        if (teddyModel == null)
            teddyModel = gameObject;

        // Start flat
        teddyModel.transform.localScale = new Vector3(1f, 1f, flatZ);

        _grab = GetComponent<XRGrabInteractable>();
        if (_grab == null) _grab = GetComponentInChildren<XRGrabInteractable>();

        if (_grab != null)
        {
            _grab.enabled = false;   // disabled until fully 3D
            _grab.selectEntered.AddListener(OnGrabbed);
        }
    }

    /// <summary>
    /// Called by FrameEnlarger with t = 0..1 (0=flat, 1=fully 3D).
    /// </summary>
    public void UpdateTransitionState(float t)
    {
        float zScale = Mathf.Lerp(flatZ, 1f, t);
        teddyModel.transform.localScale = new Vector3(1f, 1f, zScale);

        if (t >= 0.99f && !_grabbable)
        {
            _grabbable = true;
            if (_grab != null) _grab.enabled = true;
            Debug.Log("[TeddyBear2Dto3D] Teddy is now fully 3D and grabbable!");
        }
    }

    void OnGrabbed(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
    {
        if (_grabbed) return;
        _grabbed = true;

        // Detach from Frame1 so the frame doesn't follow the player's hand
        transform.SetParent(null);

        Debug.Log("[TeddyBear2Dto3D] Teddy grabbed — detached from Frame1.");
        OnTeddyGrabbed?.Invoke();

        DialogueSystem.Instance?.ShowDialogue(GameDialogue.TeddyGrabbedDialogue);
    }
}
