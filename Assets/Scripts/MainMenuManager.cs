using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button startButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button backButton;

    [Header("Settings UI")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Slider maxMarkersSlider;
    [SerializeField] private TextMeshProUGUI markersCountText;
    [SerializeField] private TextMeshProUGUI sliderValueText;

    [Header("Audio")]
    [SerializeField] private AudioSource menuMusic;

    private bool _isInitialized = false;

    void Start()
    {
        Debug.Log("=== MainMenuManager Start ===");

        // Настройка кнопок
        SetupButtons();

        // Настройка слайдера громкости
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        // Настройка слайдера количества меток
        if (maxMarkersSlider != null)
        {
            SetupMarkersSlider();
        }
        else
        {
            Debug.LogError("❌ maxMarkersSlider не назначен в инспекторе!");
        }

        // Загрузка настроек
        LoadSettings();

        // Показать главное меню
        ShowMainMenu();

        // Включить музыку меню
        if (menuMusic != null && !menuMusic.isPlaying)
            menuMusic.Play();

        _isInitialized = true;

        Debug.Log("MainMenuManager инициализирован");
    }

    private void SetupMarkersSlider()
    {
        // Настраиваем слайдер
        maxMarkersSlider.minValue = 1;
        maxMarkersSlider.maxValue = 10;
        maxMarkersSlider.wholeNumbers = true;

        // Очищаем старые слушатели и добавляем новые
        maxMarkersSlider.onValueChanged.RemoveAllListeners();
        maxMarkersSlider.onValueChanged.AddListener(OnMarkersSliderChanged);

        Debug.Log("Слайдер количества меток настроен: от 1 до 10, целые числа");
    }

    private void SetupButtons()
    {
        Debug.Log("Настройка кнопок...");

        // Start Button
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(StartAR);
        }

        // Settings Button
        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(ShowSettings);
        }

        // Quit Button
        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(QuitApp);
        }

        // Back Button
        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(ShowMainMenu);
        }
    }

    public void StartAR()
    {
        Debug.Log("🚀 Запуск AR сцены...");

        // Сохраняем настройки
        SaveSettings();

        // Останавливаем музыку
        if (menuMusic != null)
            menuMusic.Stop();

        // Загружаем сцену
        if (SceneManager.sceneCountInBuildSettings > 1)
        {
            SceneManager.LoadScene(1);
        }
        else
        {
            Debug.LogError("❌ В Build Settings нет второй сцены!");
        }
    }

    private void ShowSettings()
    {
        Debug.Log("=== Открытие настроек ===");

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        // Загружаем и отображаем текущее значение
        if (maxMarkersSlider != null)
        {
            int currentValue = PlayerPrefs.GetInt("MaxVisibleMarkers", 3);
            maxMarkersSlider.value = currentValue;

            // Обновляем текст значения
            UpdateSliderValueText(currentValue);

            Debug.Log($"В слайдер установлено значение: {currentValue}");
        }
    }

    private void ShowMainMenu()
    {
        Debug.Log("Открытие главного меню");

        // Сохраняем настройки перед закрытием
        SaveSettings();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        // Обновляем текст с количеством меток
        UpdateMarkersCountText();
    }

    private void QuitApp()
    {
        Debug.Log("Выход из приложения");
        SaveSettings();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;

        if (menuMusic != null)
            menuMusic.volume = value;

        // Сохраняем сразу
        PlayerPrefs.SetFloat("Volume", value);
        PlayerPrefs.Save();
    }

    private void OnMarkersSliderChanged(float value)
    {
        if (!_isInitialized) return;

        int intValue = Mathf.RoundToInt(value);

        // Обновляем текст значения
        UpdateSliderValueText(intValue);

        // Сохраняем в PlayerPrefs
        PlayerPrefs.SetInt("MaxVisibleMarkers", intValue);
        PlayerPrefs.Save();

        Debug.Log($"Количество меток изменено: {intValue}");

        // Обновляем текст в главном меню
        UpdateMarkersCountText();
    }

    private void UpdateSliderValueText(int value)
    {
        if (sliderValueText != null)
        {
            sliderValueText.text = value.ToString();
        }
    }

    private void LoadSettings()
    {
        Debug.Log("Загрузка настроек...");

        // Громкость
        if (volumeSlider != null)
        {
            float savedVolume = PlayerPrefs.GetFloat("Volume", 0.7f);
            volumeSlider.value = savedVolume;
            AudioListener.volume = savedVolume;
        }

        // Количество меток
        if (maxMarkersSlider != null)
        {
            int savedMarkers = PlayerPrefs.GetInt("MaxVisibleMarkers", 3);
            maxMarkersSlider.value = savedMarkers;
            UpdateSliderValueText(savedMarkers);
        }
    }

    private void SaveSettings()
    {
        bool hasChanges = false;

        // Сохраняем громкость
        if (volumeSlider != null)
        {
            PlayerPrefs.SetFloat("Volume", volumeSlider.value);
            hasChanges = true;
        }

        // Сохраняем количество меток
        if (maxMarkersSlider != null)
        {
            int markersValue = Mathf.RoundToInt(maxMarkersSlider.value);
            PlayerPrefs.SetInt("MaxVisibleMarkers", markersValue);
            hasChanges = true;
        }

        if (hasChanges)
        {
            PlayerPrefs.Save();
        }
    }

    private void UpdateMarkersCountText()
    {
        if (markersCountText == null) return;

        try
        {
            string savePath = System.IO.Path.Combine(Application.persistentDataPath, "geo_markers.json");
            int savedCount = 0;

            if (System.IO.File.Exists(savePath))
            {
                string json = System.IO.File.ReadAllText(savePath);
                var database = JsonUtility.FromJson<MarkerDatabase>(json);
                savedCount = database?.Markers?.Count ?? 0;
            }

            int maxMarkers = PlayerPrefs.GetInt("MaxVisibleMarkers", 3);
            markersCountText.text = $"Сохранено меток: {savedCount} (максимум: {maxMarkers})";
        }
        catch (System.Exception e)
        {
            markersCountText.text = "Ошибка загрузки меток";
        }
    }
}