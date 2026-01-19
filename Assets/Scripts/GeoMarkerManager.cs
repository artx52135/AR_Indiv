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
    [SerializeField] private bool verboseLogging = false;

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

    private float _lastMarkerAddTime = 0f;
    private const float ADD_COOLDOWN = 1f;

    private bool _markersAlreadyCreated = false;

    public bool IsInitialized => _isInitialized;
    public List<GameObject> ActiveMarkers => _activeMarkers;

    void Awake()
    {
        _arCamera = Camera.main;

        _wpsManager = GetComponent<ARWorldPositioningManager>();
        _objectHelper = GetComponent<ARWorldPositioningObjectHelper>();
        _cameraHelper = FindObjectOfType<ARWorldPositioningCameraHelper>();
        _savePath = Path.Combine(Application.persistentDataPath, "geo_markers.json");

        if (!PlayerPrefs.HasKey("MaxVisibleMarkers"))
        {
            PlayerPrefs.SetInt("MaxVisibleMarkers", 3);
            PlayerPrefs.Save();
        }

        maxVisibleMarkers = PlayerPrefs.GetInt("MaxVisibleMarkers");

        if (maxVisibleMarkers < 1)
        {
            maxVisibleMarkers = 1;
            PlayerPrefs.SetInt("MaxVisibleMarkers", 1);
            PlayerPrefs.Save();
        }
        else if (maxVisibleMarkers > 10)
        {
            maxVisibleMarkers = 10;
            PlayerPrefs.SetInt("MaxVisibleMarkers", 10);
            PlayerPrefs.Save();
        }

        _lastKnownMaxMarkers = maxVisibleMarkers;

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
                maxVisibleMarkers = currentValue;
                _lastKnownMaxMarkers = currentValue;

                if (maxVisibleMarkers < 1) maxVisibleMarkers = 1;
                if (maxVisibleMarkers > 10) maxVisibleMarkers = 10;

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
        }
        else
        {
            _database = new MarkerDatabase();
        }
    }

    // Лимит при добавлении новой (удаление старой метки)
    public void AddMarkerAtCurrentLocation()
    {
        if (!_isInitialized)
        {
            return;
        }

        if (_cameraHelper == null)
        {
            return;
        }

        if (Time.time - _lastMarkerAddTime < ADD_COOLDOWN)
        {
            return;
        }

        _lastMarkerAddTime = Time.time;

        int currentMaxMarkers = maxVisibleMarkers;

        if (_database.Markers.Count >= currentMaxMarkers && currentMaxMarkers > 0)
        {
            var oldestMarker = _database.Markers.OrderBy(m => m.Timestamp).FirstOrDefault();
            if (oldestMarker != null)
            {
                _database.Markers.Remove(oldestMarker);

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

        var data = new MarkerData
        {
            Latitude = _cameraHelper.Latitude,
            Longitude = _cameraHelper.Longitude,
            Timestamp = System.DateTime.Now.ToString("o")
        };

        var newObject = Instantiate(markerPrefab, Vector3.zero, Quaternion.identity);
        var geoComp = newObject.GetComponent<GeoMarker>();
        if (geoComp == null)
        {
            Destroy(newObject);
            return;
        }

        geoComp.Data = data;
        _activeMarkers.Add(newObject);

        _objectHelper.AddOrUpdateObject(
            newObject,
            data.Latitude,
            data.Longitude,
            0,
            Quaternion.identity
        );

        StartCoroutine(CheckDistanceAndHide(newObject, data));
        _database.Markers.Add(data);
        SaveMarkers();
        UpdateMarkersCounterUI();
    }

    // Лимит отображения на поле (после загрузки)
    private void UpdateVisibleMarkers()
    {
        if (_markersAlreadyCreated)
        {
            return;
        }

        _markersAlreadyCreated = true;

        if (_activeMarkers.Count > 0)
        {
            return;
        }

        foreach (var marker in _activeMarkers.ToList())
        {
            if (marker != null)
            {
                if (_objectHelper != null)
                    _objectHelper.RemoveObject(marker);
                Destroy(marker);
            }
        }
        _activeMarkers.Clear();
        _spawnedThisSession = 0;

        if (_database.Markers.Count == 0)
        {
            UpdateMarkersCounterUI();
            return;
        }

        int markersToShow = Mathf.Min(_database.Markers.Count, maxVisibleMarkers);

        var recent = _database.Markers
            .OrderByDescending(m => m.Timestamp)
            .Take(markersToShow)
            .ToList();

        foreach (var data in recent)
        {
            var tempObj = new GameObject("TempDistanceCheck");
            _objectHelper.AddOrUpdateObject(tempObj, data.Latitude, data.Longitude, 0, Quaternion.identity);

            float distance = Vector3.Distance(Camera.main.transform.position, tempObj.transform.position);

            Destroy(tempObj);

            if (distance > maxDistance + 100f)
            {
                continue;
            }

            var newObject = Instantiate(markerPrefab, Vector3.zero, Quaternion.identity);
            newObject.name = $"GeoMarker_{data.Latitude:F6}_{data.Longitude:F6}_{data.Timestamp}";

            var geoComp = newObject.GetComponent<GeoMarker>();
            if (geoComp == null)
            {
                Destroy(newObject);
                continue;
            }

            geoComp.Data = data;
            _activeMarkers.Add(newObject);
            _spawnedThisSession++;

            _objectHelper.AddOrUpdateObject(newObject, data.Latitude, data.Longitude, 0, Quaternion.identity);
            StartCoroutine(CheckDistanceAndHide(newObject, data));
        }

        UpdateMarkersCounterUI();
    }

    // Проверка расстояния
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

            marker.SetActive(inRange);

            if (showDebugUI && inRange)
            {
                var textComponents = marker.GetComponentsInChildren<TMPro.TextMeshPro>();
                var tmpComponents = marker.GetComponentsInChildren<TMPro.TextMeshProUGUI>();

                foreach (var text in textComponents)
                {
                    text.text = $"{distance:F0}m";
                }

                foreach (var tmp in tmpComponents)
                {
                    tmp.text = $"{distance:F0}m";
                }

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

    public string GetMarkersInfoString()
    {
        int onMapCount = _activeMarkers.Count(m => m != null);
        return $"Лимит: {maxVisibleMarkers}\nAR: {(IsInitialized ? "Готов" : "Загрузка")}";
    }

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


    // Метод вызывается при касании экрана
    private void TryTapOnMarker(Vector2 screenPos)
    {
        if (IsPointerOverUI(screenPos))
        {
            return;
        }

        if (_arCamera == null)
        {
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

    // Метод удаления метки
    public void RemoveMarkerOnTap(GeoMarker marker)
    {
        if (marker == null || marker.Data == null)
        {
            return;
        }

        if (_activeMarkers.Contains(marker.gameObject))
        {
            _activeMarkers.Remove(marker.gameObject);
            Destroy(marker.gameObject);
        }

        if (_database.Markers.Contains(marker.Data))
        {
            _database.Markers.Remove(marker.Data);
            SaveMarkers();
        }

        UpdateMarkersCounterUI();
    }

    public void RefreshUI()
    {
        UpdateMarkersCounterUI();
    }

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
        UpdateMarkersCounterUI();
    }

    void OnDestroy()
    {
        _markersAlreadyCreated = false;
    }
}