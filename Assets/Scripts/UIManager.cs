using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Button addButton;

    [Header("Manager")]
    [SerializeField] private GeoMarkerManager markerManager;

    void Start()
    {
        loadingPanel.SetActive(true);
        addButton.gameObject.SetActive(false);

        markerManager.OnWPSInitialized += OnWPSReady;
    }

    private void OnWPSReady()
    {
        loadingPanel.SetActive(false);
        addButton.gameObject.SetActive(true);
    }

    public void OnAddButtonPressed()
    {
        markerManager.AddMarkerAtCurrentLocation();
    }
}