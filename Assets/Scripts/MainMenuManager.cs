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

    [Header("Audio")]
    [SerializeField] private AudioSource menuMusic;

    void Start()
    {
        Debug.Log("=== MainMenuManager Start ===");

        // Настройка кнопок
        SetupButtons();

        // Настройки громкости
        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        // Загрузка настроек
        LoadSettings();

        // Показать главное меню
        ShowMainMenu();

        // Включить музыку меню
        if (menuMusic != null && !menuMusic.isPlaying)
            menuMusic.Play();
    }

    private void SetupButtons()
    {
        Debug.Log("Настройка кнопок главного меню...");

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

        // Сохранить настройки
        SaveSettings();

        // Остановить музыку меню
        if (menuMusic != null)
            menuMusic.Stop();

        // Загрузить AR сцену по индексу 1
        Debug.Log($"Загружаем сцену с индексом 1 (всего сцен: {SceneManager.sceneCountInBuildSettings})");

        if (SceneManager.sceneCountInBuildSettings > 1)
        {
            SceneManager.LoadScene(1);
        }
        else
        {
            Debug.LogError("❌ В Build Settings нет второй сцены!");
            Debug.Log("Добавьте MainScene в Build Settings (File → Build Settings)");
        }
    }

    private void ShowSettings()
    {
        Debug.Log("Открытие настроек");

        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(true);
    }

    private void ShowMainMenu()
    {
        Debug.Log("Открытие главного меню");

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);
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
    }

    private void LoadSettings()
    {
        // Громкость
        float savedVolume = PlayerPrefs.GetFloat("Volume", 0.7f);
        if (volumeSlider != null)
        {
            volumeSlider.value = savedVolume;
            AudioListener.volume = savedVolume;
        }
    }

    private void SaveSettings()
    {
        if (volumeSlider != null)
        {
            PlayerPrefs.SetFloat("Volume", volumeSlider.value);
            PlayerPrefs.Save();
        }
    }
}