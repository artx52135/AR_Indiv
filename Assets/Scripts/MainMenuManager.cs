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
        SetupButtons();

        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        if (maxMarkersSlider != null)
        {
            SetupMarkersSlider();
        }

        LoadSettings();
        ShowMainMenu();

        if (menuMusic != null && !menuMusic.isPlaying)
            menuMusic.Play();

        _isInitialized = true;
    }

    private void SetupMarkersSlider()
    {
        maxMarkersSlider.minValue = 1;
        maxMarkersSlider.maxValue = 10;
        maxMarkersSlider.wholeNumbers = true;

        maxMarkersSlider.onValueChanged.RemoveAllListeners();
        maxMarkersSlider.onValueChanged.AddListener(OnMarkersSliderChanged);
    }

    private void SetupButtons()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveAllListeners();
            startButton.onClick.AddListener(StartAR);
        }

        if (settingsButton != null)
        {
            settingsButton.onClick.RemoveAllListeners();
            settingsButton.onClick.AddListener(ShowSettings);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveAllListeners();
            quitButton.onClick.AddListener(QuitApp);
        }

        if (backButton != null)
        {
            backButton.onClick.RemoveAllListeners();
            backButton.onClick.AddListener(ShowMainMenu);
        }
    }

    public void StartAR()
    {
        SaveSettings();

        if (menuMusic != null)
            menuMusic.Stop();

        if (SceneManager.sceneCountInBuildSettings > 1)
        {
            SceneManager.LoadScene(1);
        }
    }

    private void ShowSettings()
    {
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        if (maxMarkersSlider != null)
        {
            int currentValue = PlayerPrefs.GetInt("MaxVisibleMarkers", 3);
            maxMarkersSlider.value = currentValue;
            UpdateSliderValueText(currentValue);
        }
    }

    private void ShowMainMenu()
    {
        SaveSettings();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        UpdateMarkersCountText();
    }

    private void QuitApp()
    {
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

        PlayerPrefs.SetFloat("Volume", value);
        PlayerPrefs.Save();
    }

    private void OnMarkersSliderChanged(float value)
    {
        if (!_isInitialized) return;

        int intValue = Mathf.RoundToInt(value);
        UpdateSliderValueText(intValue);

        PlayerPrefs.SetInt("MaxVisibleMarkers", intValue);
        PlayerPrefs.Save();
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
        if (volumeSlider != null)
        {
            float savedVolume = PlayerPrefs.GetFloat("Volume", 0.7f);
            volumeSlider.value = savedVolume;
            AudioListener.volume = savedVolume;
        }

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

        if (volumeSlider != null)
        {
            PlayerPrefs.SetFloat("Volume", volumeSlider.value);
            hasChanges = true;
        }

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
        catch (System.Exception)
        {
            markersCountText.text = "Ошибка загрузки меток";
        }
    }
}