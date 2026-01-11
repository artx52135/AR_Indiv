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

    public bool IsInitialized => _isInitialized;
    public List<GameObject> ActiveMarkers => _activeMarkers;

    void Awake()
    {
        _arCamera = Camera.main;

        _wpsManager = GetComponent<ARWorldPositioningManager>();
        _objectHelper = GetComponent<ARWorldPositioningObjectHelper>();
        _cameraHelper = FindObjectOfType<ARWorldPositioningCameraHelper>();
        _savePath = Path.Combine(Application.persistentDataPath, "geo_markers.json");

        LoadMarkers();
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

        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            Vector2 touchPos = Input.GetTouch(0).position;
            TryTapOnMarker(touchPos);
        }
    }

    private void SaveMarkers()
    {
        string json = JsonUtility.ToJson(_database, true);
        File.WriteAllText(_savePath, json);
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

    public void AddMarkerAtCurrentLocation()
    {

        if (!_isInitialized)
        {
            return;
        }

        var data = new MarkerData
        {
            Latitude = _cameraHelper.Latitude,
            Longitude = _cameraHelper.Longitude,
            Timestamp = System.DateTime.Now.ToString("o")
        };

        var newObject = Instantiate(markerPrefab, Vector3.zero, Quaternion.identity);
        var geoComp = newObject.GetComponent<GeoMarker>();
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

    }

    private void UpdateVisibleMarkers()
    {

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
            return;
        }

        var recent = _database.Markers
            .OrderByDescending(m => m.Timestamp)
            .Take(maxVisibleMarkers)
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
            var geoComp = newObject.GetComponent<GeoMarker>();
            geoComp.Data = data;

            _activeMarkers.Add(newObject);
            _spawnedThisSession++;


            _objectHelper.AddOrUpdateObject(newObject, data.Latitude, data.Longitude, 0, Quaternion.identity);

            StartCoroutine(CheckDistanceAndHide(newObject, data));
        }

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

            marker.SetActive(inRange);

            if (showDebugUI && inRange)
            {
                var label = marker.GetComponentInChildren<TMPro.TextMeshPro>();
                if (label) label.text = $"{distance:F0}m";
            }

            yield return wait;
        }
    }

    private bool IsPointerOverUI(Vector2 screenPos)
    {
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
        if (marker == null || marker.Data == null) return;

        if (_activeMarkers.Contains(marker.gameObject))
        {
            _activeMarkers.Remove(marker.gameObject);
            Destroy(marker.gameObject);
        }

        _database.Markers.Remove(marker.Data);
        SaveMarkers();
    }
}