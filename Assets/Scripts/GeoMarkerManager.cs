using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Niantic.Lightship.AR.WorldPositioning;
using Niantic.Lightship.AR.XRSubsystems;
using System.Linq;
using System.Collections;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;

[System.Serializable]
public class MarkerDatabase
{
    public List<MarkerData> Markers = new List<MarkerData>();
}

[RequireComponent(typeof(ARWorldPositioningManager))]
public class GeoMarkerManager : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private GameObject markerPrefab;
    [SerializeField] private int maxVisibleMarkers = 10;
    [SerializeField] private float maxDistance = 200f;

    [Header("UI References")]
    [SerializeField] private TMPro.TextMeshProUGUI markersCounterText;

    [Header("Debug")]
    [SerializeField] private bool showDebugUI = true;
    [SerializeField] private bool verboseLogging = true;

    public UnityAction OnWPSInitialized;

    private ARWorldPositioningManager _wpsManager;
    private ARWorldPositioningObjectHelper _objectHelper;
    private ARWorldPositioningCameraHelper _cameraHelper;
    private Camera _arCamera;

    private MarkerDatabase _database;
    private string _savePath;
    private List<GameObject> _activeMarkers = new();

    private bool _isInitialized = false;
    private int _spawnedThisSession = 0;
    private int _lastKnownMaxMarkers = 0;

    // ЗАЩИТА ОТ ДВОЙНОГО ДОБАВЛЕНИЯ
    private float _lastMarkerAddTime = 0f;
    private const float ADD_COOLDOWN = 1f;

    public bool IsInitialized => _isInitialized;
    public List<GameObject> ActiveMarkers => _activeMarkers;

    void Awake()
    {
        _arCamera = Camera.main;

        _wpsManager = GetComponent<ARWorldPositioningManager>();
        _objectHelper = GetComponent<ARWorldPositioningObjectHelper>();
        _cameraHelper = FindObjectOfType<ARWorldPositioningCameraHelper>();
        _savePath = Path.Combine(Application.persistentDataPath, "geo_markers.json");

        // ГАРАНТИРОВАННАЯ загрузка значения из настроек
        Debug.Log("=== GeoMarkerManager Awake ===");

        // 1. Проверяем PlayerPrefs
        if (!PlayerPrefs.HasKey("MaxVisibleMarkers"))
        {
            Debug.LogWarning("Ключ MaxVisibleMarkers не найден! Создаем со значением 3.");
            PlayerPrefs.SetInt("MaxVisibleMarkers", 3);
            PlayerPrefs.Save();
        }

        // 2. Загружаем значение
        maxVisibleMarkers = PlayerPrefs.GetInt("MaxVisibleMarkers");

        // 3. Проверяем и корректируем значение (должно быть от 1 до 10)
        if (maxVisibleMarkers < 1)
        {
            Debug.LogWarning($"Некорректное значение {maxVisibleMarkers}, устанавливаем 1");
            maxVisibleMarkers = 1;
            PlayerPrefs.SetInt("MaxVisibleMarkers", 1);
            PlayerPrefs.Save();
        }
        else if (maxVisibleMarkers > 10)
        {
            Debug.LogWarning($"Некорректное значение {maxVisibleMarkers}, устанавливаем 10");
            maxVisibleMarkers = 10;
            PlayerPrefs.SetInt("MaxVisibleMarkers", 10);
            PlayerPrefs.Save();
        }

        _lastKnownMaxMarkers = maxVisibleMarkers;

        Debug.Log($"Загружено максимальное количество меток: {maxVisibleMarkers}");
        Debug.Log($"PlayerPrefs сохранен: {PlayerPrefs.HasKey("MaxVisibleMarkers")}");
        Debug.Log($"PlayerPrefs значение: {PlayerPrefs.GetInt("MaxVisibleMarkers")}");

        LoadMarkers();
        UpdateMarkersCounterUI();
    }

    void OnEnable()
    {
        if (_wpsManager) _wpsManager.OnStatusChanged += OnWPSStatusChanged;
    }

    void OnDisable()
    {
        if (_wpsManager) _wpsManager.OnStatusChanged -= OnWPSStatusChanged;
    }

    private void OnWPSStatusChanged(WorldPositioningStatus status)
    {
        if (status == WorldPositioningStatus.Available && !_isInitialized)
        {
            _isInitialized = true;
            UpdateVisibleMarkers();
            OnWPSInitialized?.Invoke();
        }
    }

    void Update()
    {
        if (!_isInitialized) return;

        // Проверяем, изменилось ли значение в настройках
        CheckForMaxMarkersUpdate();

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            Vector2 touchPos = Input.GetTouch(0).position;
            TryTapOnMarker(touchPos);
        }
    }

    private void CheckForMaxMarkersUpdate()
    {
        if (PlayerPrefs.HasKey("MaxVisibleMarkers"))
        {
            int currentValue = PlayerPrefs.GetInt("MaxVisibleMarkers");
            if (currentValue != _lastKnownMaxMarkers)
            {
                Debug.Log($"Обнаружено изменение максимального количества меток: {_lastKnownMaxMarkers} -> {currentValue}");
                maxVisibleMarkers = currentValue;
                _lastKnownMaxMarkers = currentValue;

                // Проверяем корректность значения
                if (maxVisibleMarkers < 1) maxVisibleMarkers = 1;
                if (maxVisibleMarkers > 10) maxVisibleMarkers = 10;

                // Перезагружаем видимые маркеры с новым лимитом
                UpdateVisibleMarkers();
            }
        }
    }

    private void SaveMarkers()
    {
        string json = JsonUtility.ToJson(_database, true);
        File.WriteAllText(_savePath, json);
        UpdateMarkersCounterUI();
    }

    private void LoadMarkers()
    {
        if (File.Exists(_savePath))
        {
            string json = File.ReadAllText(_savePath);
            _database = JsonUtility.FromJson<MarkerDatabase>(json);
            Debug.Log($"Загружено {_database.Markers.Count} сохраненных меток");
        }
        else
        {
            _database = new MarkerDatabase();
            Debug.Log("Файл меток не найден, создана новая база данных");
        }
    }

    public void AddMarkerAtCurrentLocation()
    {
        Debug.Log($"=== AddMarkerAtCurrentLocation вызван (Time: {Time.time:F2}) ===");

        if (!_isInitialized)
        {
            Debug.LogWarning("AR не инициализирован!");
            return;
        }

        if (_cameraHelper == null)
        {
            Debug.LogError("ARWorldPositioningCameraHelper не найден!");
            return;
        }

        // ЗАЩИТА: проверяем время с последнего добавления
        if (Time.time - _lastMarkerAddTime < ADD_COOLDOWN)
        {
            Debug.LogWarning($"Пропускаем - прошло только {Time.time - _lastMarkerAddTime:F2} секунд с последнего добавления");
            return;
        }

        _lastMarkerAddTime = Time.time;

        // Используем актуальное значение из настроек
        int currentMaxMarkers = maxVisibleMarkers;

        Debug.Log($"Проверка лимита: {_database.Markers.Count}/{currentMaxMarkers}");

        // Проверяем, не превышает ли количество сохраненных меток максимальное
        if (_database.Markers.Count >= currentMaxMarkers && currentMaxMarkers > 0)
        {
            Debug.Log($"Достигнут лимит в {currentMaxMarkers} меток, удаляем самую старую");

            // Удаляем самую старую метку
            var oldestMarker = _database.Markers.OrderBy(m => m.Timestamp).FirstOrDefault();
            if (oldestMarker != null)
            {
                _database.Markers.Remove(oldestMarker);
                Debug.Log($"Удалена старая метка от {oldestMarker.Timestamp}");

                // Также удаляем соответствующий GameObject из активных
                var oldestGameObject = _activeMarkers.FirstOrDefault(m =>
                    m != null &&
                    m.GetComponent<GeoMarker>()?.Data == oldestMarker);
                if (oldestGameObject != null)
                {
                    _activeMarkers.Remove(oldestGameObject);
                    Destroy(oldestGameObject);
                }
            }
        }

        // Создаем новые данные маркера
        var data = new MarkerData
        {
            Latitude = _cameraHelper.Latitude,
            Longitude = _cameraHelper.Longitude,
            Timestamp = System.DateTime.Now.ToString("o")
        };

        // Создаем новый маркер
        var newObject = Instantiate(markerPrefab, Vector3.zero, Quaternion.identity);
        var geoComp = newObject.GetComponent<GeoMarker>();
        if (geoComp == null)
        {
            Debug.LogError("Префаб маркера не содержит компонент GeoMarker!");
            Destroy(newObject);
            return;
        }

        geoComp.Data = data;

        // Добавляем в список активных маркеров
        _activeMarkers.Add(newObject);

        // Позиционируем с помощью WPS
        _objectHelper.AddOrUpdateObject(
            newObject,
            data.Latitude,
            data.Longitude,
            0,
            Quaternion.identity
        );

        // Запускаем проверку расстояния
        StartCoroutine(CheckDistanceAndHide(newObject, data));

        // Сохраняем в базу данных
        _database.Markers.Add(data);
        SaveMarkers();

        Debug.Log($"✅ Добавлен новый маркер! Всего меток: {_database.Markers.Count}");

        // Обновляем UI
        UpdateMarkersCounterUI();
    }

    private void UpdateVisibleMarkers()
    {
        // Удаляем все текущие маркеры
        foreach (var marker in _activeMarkers)
        {
            if (marker)
            {
                Destroy(marker);
            }
        }
        _activeMarkers.Clear();
        _spawnedThisSession = 0;

        if (_database.Markers.Count == 0)
        {
            Debug.Log("Нет сохраненных меток для отображения");
            UpdateMarkersCounterUI();
            return;
        }

        // Берем самые новые метки, но не более maxVisibleMarkers
        var recent = _database.Markers
            .OrderByDescending(m => m.Timestamp)
            .Take(maxVisibleMarkers)
            .ToList();

        Debug.Log($"Отображаем {recent.Count} самых новых меток из {_database.Markers.Count} (лимит: {maxVisibleMarkers})");

        foreach (var data in recent)
        {
            // Проверяем расстояние
            var tempObj = new GameObject("TempDistanceCheck");
            _objectHelper.AddOrUpdateObject(tempObj, data.Latitude, data.Longitude, 0, Quaternion.identity);

            float distance = Vector3.Distance(Camera.main.transform.position, tempObj.transform.position);

            Destroy(tempObj);

            // Если слишком далеко - пропускаем
            if (distance > maxDistance + 100f)
            {
                Debug.Log($"Маркер слишком далеко ({distance:F0}m), пропускаем");
                continue;
            }

            // Создаем маркер
            var newObject = Instantiate(markerPrefab, Vector3.zero, Quaternion.identity);
            var geoComp = newObject.GetComponent<GeoMarker>();
            geoComp.Data = data;

            _activeMarkers.Add(newObject);
            _spawnedThisSession++;

            // Позиционируем
            _objectHelper.AddOrUpdateObject(newObject, data.Latitude, data.Longitude, 0, Quaternion.identity);

            // Запускаем проверку расстояния
            StartCoroutine(CheckDistanceAndHide(newObject, data));
        }

        UpdateMarkersCounterUI();
        Debug.Log($"Загружено {_activeMarkers.Count} маркеров на поле");
    }

    private IEnumerator CheckDistanceAndHide(GameObject marker, MarkerData data)
    {
        var wait = new WaitForSeconds(1f);
        bool wasVisible = false;

        while (marker != null)
        {
            if (!marker.activeInHierarchy)
            {
                yield return wait;
                continue;
            }

            float distance = Vector3.Distance(Camera.main.transform.position, marker.transform.position);
            bool inRange = distance <= maxDistance;

            if (inRange && !wasVisible)
            {
                wasVisible = true;
            }

            // Показываем/скрываем маркер в зависимости от расстояния
            marker.SetActive(inRange);

            // Обновляем текст с расстоянием
            if (showDebugUI && inRange)
            {
                var textComponents = marker.GetComponentsInChildren<TMPro.TextMeshPro>();
                var tmpComponents = marker.GetComponentsInChildren<TMPro.TextMeshProUGUI>();

                // Обновляем все текстовые компоненты
                foreach (var text in textComponents)
                {
                    text.text = $"{distance:F0}m";
                }

                foreach (var tmp in tmpComponents)
                {
                    tmp.text = $"{distance:F0}m";
                }

                // Поворачиваем текст к камере
                if (_arCamera != null)
                {
                    foreach (var text in textComponents)
                    {
                        text.transform.rotation = Quaternion.LookRotation(
                            text.transform.position - _arCamera.transform.position
                        );
                    }

                    foreach (var tmp in tmpComponents)
                    {
                        tmp.transform.rotation = Quaternion.LookRotation(
                            tmp.transform.position - _arCamera.transform.position
                        );
                    }
                }
            }

            yield return wait;
        }
    }

    // ==============================
    // ПУБЛИЧНЫЕ МЕТОДЫ ДЛЯ UI
    // ==============================

    /// <summary>
    /// Возвращает упрощенную информацию о маркерах для UI
    /// </summary>
    public string GetMarkersInfoString()
    {
        int onMapCount = _activeMarkers.Count(m => m != null);
        return $"Лимит: {maxVisibleMarkers}\nНа карте: {onMapCount}\nAR: {(IsInitialized ? "Готов" : "Загрузка")}";
    }

    /// <summary>
    /// Возвращает количество маркеров на карте
    /// </summary>
    public int GetActiveMarkersCount()
    {
        return _activeMarkers.Count(m => m != null);
    }

    private void UpdateMarkersCounterUI()
    {
        if (markersCounterText != null)
        {
            markersCounterText.text = GetMarkersInfoString();
        }
    }

    private bool IsPointerOverUI(Vector2 screenPos)
    {
        if (EventSystem.current == null) return false;

        var eventData = new PointerEventData(EventSystem.current)
        {
            position = screenPos
        };

        var results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            if (result.gameObject.GetComponent<UnityEngine.UI.Button>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private void TryTapOnMarker(Vector2 screenPos)
    {
        if (IsPointerOverUI(screenPos))
        {
            return;
        }

        if (_arCamera == null)
        {
            Debug.LogError("AR камера не найдена!");
            return;
        }

        Ray ray = _arCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity))
        {
            var geoMarker = hit.collider.GetComponent<GeoMarker>();
            if (geoMarker != null)
            {
                RemoveMarkerOnTap(geoMarker);
                return;
            }
        }
    }

    public void RemoveMarkerOnTap(GeoMarker marker)
    {
        if (marker == null || marker.Data == null)
        {
            Debug.LogWarning("Попытка удалить null маркер");
            return;
        }

        if (_activeMarkers.Contains(marker.gameObject))
        {
            _activeMarkers.Remove(marker.gameObject);
            Destroy(marker.gameObject);
            Debug.Log("Маркер удален с поля");
        }

        if (_database.Markers.Contains(marker.Data))
        {
            _database.Markers.Remove(marker.Data);
            SaveMarkers();
            Debug.Log("Маркер удален из базы данных");
        }
        else
        {
            Debug.LogWarning("Маркер не найден в базе данных");
        }

        // Обновляем UI
        UpdateMarkersCounterUI();
    }

    /// <summary>
    /// Метод для принудительного обновления UI
    /// </summary>
    public void RefreshUI()
    {
        UpdateMarkersCounterUI();
    }

    /// <summary>
    /// Очищает все маркеры (для отладки)
    /// </summary>
    public void ClearAllMarkers()
    {
        foreach (var marker in _activeMarkers)
        {
            if (marker)
            {
                Destroy(marker);
            }
        }
        _activeMarkers.Clear();

        _database.Markers.Clear();
        SaveMarkers();

        Debug.Log("Все маркеры очищены");
        UpdateMarkersCounterUI();
    }
}