using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class CompassArrow : MonoBehaviour
{
    [SerializeField] private GeoMarkerManager _manager;
    [SerializeField] private InteractionButton _interactionButton;

    private RectTransform _rect;
    private Camera _cam;
    private Image arrowImage;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _cam = Camera.main;
        arrowImage = GetComponent<Image>();
        HideArrow();
    }

    private void Update()
    {
        _interactionButton.HideButton();
        if (!_manager || !_manager.IsInitialized || _manager.ActiveMarkers.Count == 0)
        {
            HideArrow();
            return;
        }

        var target = FindClosestMarkerOffScreen();
        if (target == null)
        {
            HideArrow();
            return;
        }
        ShowArrowTowards(target.transform.position);
    }

    private GameObject FindClosestMarkerOffScreen()
    {
        GameObject closest = null;
        float minDist = float.MaxValue;

        foreach (var m in _manager.ActiveMarkers)
        {
            if (m == null) continue;

            float dist = Vector3.Distance(_cam.transform.position, m.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = m;
            }
        }

        if (closest == null)
        {
            return null;
        }

        Vector3 screenPos = _cam.WorldToScreenPoint(new Vector3(closest.transform.position.x, closest.transform.position.y - 1.5f, closest.transform.position.z));
        bool onScreen = screenPos.z > 0 &&
                        screenPos.x >= 0 && screenPos.x <= Screen.width &&
                        screenPos.y >= 0 && screenPos.y <= Screen.height;

        if (onScreen)
        {
            _interactionButton.ShowButton(closest);
            return null;
        }
        else
        {
            _interactionButton.HideButton();
            return closest;
        }
    }

    private void ShowArrowTowards(Vector3 worldPos)
    {
        Vector3 toTarget = worldPos - _cam.transform.position;

        // ÿ¨þõú²ø  ýð ÿûþ¸úþ¸¥¹
        Vector3 flatDir = Vector3.ProjectOnPlane(toTarget, _cam.transform.up).normalized;

        // ´óþû ÿþòþ¨þ¥ð
        float angle = Mathf.Atan2(
            Vector3.Dot(flatDir, _cam.transform.right),
            Vector3.Dot(flatDir, _cam.transform.forward)
        ) * Mathf.Rad2Deg;

        _rect.rotation = Quaternion.Euler(
            _rect.rotation.eulerAngles.x,
            0,
            -angle
        );

        if (!arrowImage.enabled)
            arrowImage.enabled = true;
    }

    private void HideArrow()
    {
        if (arrowImage.enabled)
            arrowImage.enabled = false;
    }
}