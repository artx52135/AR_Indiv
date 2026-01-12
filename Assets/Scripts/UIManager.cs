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
    [SerializeField] private Button backToMenuButton; // ← НОВАЯ КНОПКА!
    [SerializeField] private TextMeshProUGUI loadingText;

    [Header("Manager")]
    [SerializeField] private GeoMarkerManager markerManager;

    [Header("Settings")]
    [SerializeField] private float timeoutSeconds = 10f;
    [SerializeField] private bool skipInitialization = true; // Пропускаем ожидание

    private Coroutine _timeoutCoroutine;

    void Start()
    {
        Debug.Log("=== AR UIManager Start ===");

        // Настройка кнопки "В меню"
        if (backToMenuButton != null)
        {
            backToMenuButton.onClick.AddListener(BackToMainMenu);
            backToMenuButton.gameObject.SetActive(true);
            Debug.Log("Кнопка 'В меню' настроена");
        }
        else
        {
            Debug.LogError("❌ BackToMenuButton не назначена! Добавьте кнопку в Canvas");
        }

        // Инициализация UI
        InitializeUI();

        // Настройки
        ApplySettings();
    }

    private void InitializeUI()
    {
        Debug.Log("Инициализация UI...");

        // Если пропустить инициализацию (для теста)
        if (skipInitialization)
        {
            Debug.Log("Пропускаем ожидание инициализации AR");
            ShowUI();
            return;
        }

        // Проверяем есть ли менеджер
        if (markerManager == null)
        {
            markerManager = FindObjectOfType<GeoMarkerManager>();
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

        // Таймаут истек
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
        // Громкость
        if (PlayerPrefs.HasKey("Volume"))
        {
            float volume = PlayerPrefs.GetFloat("Volume", 0.7f);
            AudioListener.volume = volume;
        }
    }

    public void OnAddButtonPressed()
    {
        Debug.Log("Добавление маркера...");

        if (markerManager != null && markerManager.IsInitialized)
        {
            markerManager.AddMarkerAtCurrentLocation();
        }
        else
        {
            Debug.LogWarning("AR не инициализирован!");
        }
    }

    // ВОТ ЭТОТ МЕТОД ДЛЯ ВОЗВРАТА В МЕНЮ
    private void BackToMainMenu()
    {
        Debug.Log("🔄 Возврат в главное меню...");

        // Сохраняем настройки если нужно
        PlayerPrefs.Save();

        // Загружаем главное меню
        SceneManager.LoadScene("MainMenuScene");
    }

    void OnDestroy()
    {
        if (markerManager != null)
            markerManager.OnWPSInitialized -= OnWPSReady;
    }

    void Update()
    {
        // Дополнительно: кнопка Escape тоже возвращает в меню
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            BackToMainMenu();
        }
    }
}