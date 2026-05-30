using UnityEngine;

/// <summary>
/// Scrolls the material's main texture UV continuously for a flowing comic-book ground effect.
/// Attach to the FlowingGround plane.
/// </summary>
public class FlowingGround : MonoBehaviour
{
    [Header("=== Settings ===")]
    public float flowSpeed = 0.2f;
    public Vector2 flowDirection = new Vector2(0.5f, 0.5f);

    private Material _mat;

    void Start()
    {
        var r = GetComponent<Renderer>();
        if (r == null)
        {
            Debug.LogWarning("[FlowingGround] No Renderer found on this GameObject.");
            return;
        }
        _mat = r.material;   // auto-clones to avoid shared material mutation
    }

    void Update()
    {
        if (_mat == null) return;
        _mat.mainTextureOffset += flowDirection.normalized * flowSpeed * Time.deltaTime;
    }
}
