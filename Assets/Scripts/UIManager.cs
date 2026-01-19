using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class UIManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject loadingPanel;
    [SerializeField] private Button addButton;
    [SerializeField] private Button backToMenuButton;
    [SerializeField] private TextMeshProUGUI loadingText;
    [SerializeField] private TextMeshProUGUI markersInfoText;

    [Header("Manager")]
    [SerializeField] private GeoMarkerManager markerManager;

    [Header("Settings")]
    [SerializeField] private float timeoutSeconds = 10f;
    [SerializeField] private bool skipInitialization = true;

    private Coroutine _timeoutCoroutine;
    private float _updateTimer = 0f;
    private const float UPDATE_INTERVAL = 1f;
    private bool _buttonBlocked = false;
    private float _lastClickTime = 0f;
    private const float CLICK_COOLDOWN = 1f;

    void Start()
    {
        if (markerManager == null)
        {
            markerManager = FindObjectOfType<GeoMarkerManager>();
        }

        if (markersInfoText != null)
        {
            markersInfoText.text = "Инициализация...";
        }

        if (backToMenuButton != null)
        {
            backToMenuButton.onClick.RemoveAllListeners();
            backToMenuButton.onClick.AddListener(BackToMainMenu);
            backToMenuButton.gameObject.SetActive(true);
        }

        if (addButton != null)
        {
            addButton.onClick.RemoveAllListeners();
            addButton.onClick.AddListener(OnAddButtonPressed);
        }

        InitializeUI();
        ApplySettings();
    }

    private void InitializeUI()
    {
        if (skipInitialization)
        {
            ShowUI();
            return;
        }

        if (markerManager != null)
        {
            markerManager.OnWPSInitialized += OnWPSReady;
            _timeoutCoroutine = StartCoroutine(InitializationTimeout());
        }
        else
        {
            ShowUI();
        }
    }

    private IEnumerator InitializationTimeout()
    {
        float elapsedTime = 0f;

        while (elapsedTime < timeoutSeconds)
        {
            elapsedTime += Time.deltaTime;

            if (loadingText != null)
            {
                float progress = Mathf.Clamp01(elapsedTime / timeoutSeconds);
                loadingText.text = $"Инициализация AR... {Mathf.FloorToInt(progress * 100)}%";
            }

            yield return null;
        }

        ForceShowUI();
    }

    private void OnWPSReady()
    {
        if (_timeoutCoroutine != null)
        {
            StopCoroutine(_timeoutCoroutine);
            _timeoutCoroutine = null;
        }

        ShowUI();
    }

    private void ShowUI()
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        if (addButton != null)
        {
            addButton.gameObject.SetActive(true);
            addButton.interactable = true;
        }

        UpdateMarkersInfo();
    }

    private void ForceShowUI()
    {
        if (loadingPanel != null)
            loadingPanel.SetActive(false);

        if (addButton != null)
        {
            addButton.gameObject.SetActive(true);
            addButton.interactable = false;
            var text = addButton.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = "AR не готов";
        }
    }

    private void ApplySettings()
    {
        if (PlayerPrefs.HasKey("Volume"))
        {
            float volume = PlayerPrefs.GetFloat("Volume", 0.7f);
            AudioListener.volume = volume;
        }
    }

    public void OnAddButtonPressed()
    {
        if (Time.time - _lastClickTime < CLICK_COOLDOWN)
        {
            return;
        }

        if (_buttonBlocked)
        {
            return;
        }

        _lastClickTime = Time.time;
        _buttonBlocked = true;

        if (markerManager != null && markerManager.IsInitialized)
        {
            markerManager.AddMarkerAtCurrentLocation();
            UpdateMarkersInfo();
        }

        StartCoroutine(UnblockButtonAfterDelay(1f));
    }

    private IEnumerator UnblockButtonAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        _buttonBlocked = false;
    }

    private void UpdateMarkersInfo()
    {
        if (markersInfoText == null) return;

        if (markerManager == null)
        {
            markersInfoText.text = "Лимит: ?\nAR: Нет менеджера";
            return;
        }

        try
        {
            string info = markerManager.GetMarkersInfoString();
            markersInfoText.text = info;
        }
        catch
        {
            markersInfoText.text = "Ошибка загрузки";
        }
    }

    private void BackToMainMenu()
    {
        PlayerPrefs.Save();
        SceneManager.LoadScene("MainMenuScene");
    }

    void OnDestroy()
    {
        if (markerManager != null)
            markerManager.OnWPSInitialized -= OnWPSReady;
    }

    void Update()
    {
        _updateTimer += Time.deltaTime;
        if (_updateTimer >= UPDATE_INTERVAL)
        {
            UpdateMarkersInfo();
            _updateTimer = 0f;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            BackToMainMenu();
        }
    }
}