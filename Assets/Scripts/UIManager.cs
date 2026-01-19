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
    private const float CLICK_COOLDOWN = 1f; // 1 секунда между кликами

    void Start()
    {
        Debug.Log("=== AR UIManager Start ===");

        // Ищем менеджер если не назначен
        if (markerManager == null)
        {
            markerManager = FindObjectOfType<GeoMarkerManager>();
            if (markerManager != null)
            {
                Debug.Log($"Найден GeoMarkerManager: {markerManager.gameObject.name}");
            }
        }

        // Проверяем текстовый элемент
        if (markersInfoText != null)
        {
            markersInfoText.text = "Инициализация...";
        }

        // Настройка кнопки "В меню"
        if (backToMenuButton != null)
        {
            backToMenuButton.onClick.RemoveAllListeners();
            backToMenuButton.onClick.AddListener(BackToMainMenu);
            backToMenuButton.gameObject.SetActive(true);
        }

        // Настройка кнопки "Добавить" - ОЧИЩАЕМ ВСЕ ПРЕДЫДУЩИЕ СЛУШАТЕЛИ
        if (addButton != null)
        {
            Debug.Log("Настройка кнопки Добавить...");

            // ОЧЕНЬ ВАЖНО: удаляем ВСЕ слушатели
            addButton.onClick.RemoveAllListeners();
            Debug.Log($"После очистки слушателей: {addButton.onClick.GetPersistentEventCount()}");

            // Добавляем НАШ слушатель
            addButton.onClick.AddListener(OnAddButtonPressed);
            Debug.Log($"Добавлен наш слушатель, всего: {addButton.onClick.GetPersistentEventCount()}");
        }

        // Инициализация UI
        InitializeUI();

        // Настройки
        ApplySettings();
    }

    private void InitializeUI()
    {
        Debug.Log("Инициализация UI...");

        if (skipInitialization)
        {
            Debug.Log("Пропускаем ожидание инициализации AR");
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
            Debug.LogError("GeoMarkerManager не найден!");
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

        Debug.LogWarning("Таймаут инициализации!");
        ForceShowUI();
    }

    private void OnWPSReady()
    {
        Debug.Log("✅ AR инициализирован!");

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
        Debug.Log($"=== НАЖАТИЕ КНОПКИ ДОБАВИТЬ (Time: {Time.time:F2}) ===");

        // ЗАЩИТА №1: Проверяем время с последнего клика
        if (Time.time - _lastClickTime < CLICK_COOLDOWN)
        {
            Debug.Log($"Игнорируем - прошло только {Time.time - _lastClickTime:F2} секунд");
            return;
        }

        // ЗАЩИТА №2: Проверяем блокировку
        if (_buttonBlocked)
        {
            Debug.Log("Игнорируем - кнопка заблокирована");
            return;
        }

        _lastClickTime = Time.time;
        _buttonBlocked = true;

        if (markerManager != null && markerManager.IsInitialized)
        {
            Debug.Log("Вызываем AddMarkerAtCurrentLocation");
            markerManager.AddMarkerAtCurrentLocation();
            UpdateMarkersInfo();
        }
        else
        {
            Debug.LogWarning("AR не инициализирован!");
        }

        // Разблокируем кнопку через 1 секунду
        StartCoroutine(UnblockButtonAfterDelay(1f));
    }

    private IEnumerator UnblockButtonAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        _buttonBlocked = false;
        Debug.Log("Кнопка разблокирована");
    }

    private void UpdateMarkersInfo()
    {
        if (markersInfoText == null) return;

        if (markerManager == null)
        {
            markersInfoText.text = "Лимит: ?\nНа карте: ?\nAR: Нет менеджера";
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
        Debug.Log("Возврат в главное меню...");
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