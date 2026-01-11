using UnityEngine;

[System.Serializable]
public class MarkerData
{
    public double Latitude;
    public double Longitude;
    public string Timestamp;
}

public class GeoMarker : MonoBehaviour
{
    public MarkerData Data;
    public AudioClip barkSound;

    private AudioSource _audioSource;

    private void Awake()
    {
        _audioSource = GetComponentInChildren<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1.0f;
            _audioSource.minDistance = 1f;
            _audioSource.maxDistance = 10f;
            _audioSource.playOnAwake = false;
        }
    }

    public void PlayBarkSound()
    {
        if (barkSound != null && _audioSource != null)
        {
            _audioSource.PlayOneShot(barkSound);
        }
    }

    public void RemoveMarker()
    {
        var manager = FindObjectOfType<GeoMarkerManager>();
        if (manager != null)
        {
            manager.RemoveMarkerOnTap(this);
        }
        else
        {

            Destroy(gameObject);
        }
    }
}