using UnityEngine;

/// <summary>
/// DEV/TEST ONLY. Records the thief hand's lowest point during the RUNTIME lift
/// (edit-mode clip sampling underestimates — animator blending shifts the path).
/// Logs hand world pos, thief root pos and yaw at that moment.
/// </summary>
public class HandPathTracker : MonoBehaviour
{
    private ThiefSpawner _sp;
    private float _minY = float.MaxValue;
    private Vector3 _handAtMin, _thiefAtMin;
    private float _yawAtMin;
    private float _logTimer;

    void Start() { _sp = FindFirstObjectByType<ThiefSpawner>(); }

    void LateUpdate()
    {
        if (_sp == null || _sp.thiefModel == null || !_sp.thiefModel.activeSelf || _sp.thiefHandBone == null) return;

        Vector3 h = _sp.thiefHandBone.position;
        if (h.y < _minY)
        {
            _minY = h.y;
            _handAtMin = h;
            _thiefAtMin = _sp.thiefModel.transform.position;
            _yawAtMin = _sp.thiefModel.transform.eulerAngles.y;
        }

        _logTimer += Time.deltaTime;
        if (_logTimer > 2f)
        {
            _logTimer = 0f;
            Debug.Log($"[HandPathTracker] minY={_minY:F3} hand={_handAtMin:F3} thief={_thiefAtMin:F3} yaw={_yawAtMin:F1}");
        }
    }
}
