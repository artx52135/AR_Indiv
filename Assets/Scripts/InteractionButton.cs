using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Button))]
public class InteractionButton : MonoBehaviour, IPointerClickHandler
{
    private GameObject _currentTarget;

    private void Awake()
    {
        HideButton();
    }

    public void ShowButton(GameObject target)
    {
        if (target == null)
        {
            HideButton();
            return;
        }
        _currentTarget = target;
        gameObject.SetActive(true);
    }

    public void HideButton()
    {
        _currentTarget = null;
        gameObject.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (_currentTarget == null) return;

        var geoMarker = _currentTarget.GetComponent<GeoMarker>();
        if (geoMarker != null)
        {
            geoMarker.PlayBarkSound();
        }
    }
}