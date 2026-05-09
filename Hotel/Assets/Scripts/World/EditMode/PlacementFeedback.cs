// Assets/Scripts/EditMode/PlacementFeedback.cs
using UnityEngine;

public class PlacementFeedback : MonoBehaviour
{
    public static PlacementFeedback Instance { get; private set; }

    public enum SoundType
    {
        PickUp, Place, Rotate, Delete, ModeEnter, ModeExit
    }

    [Header("Audio")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip clipPickUp;
    [SerializeField] private AudioClip clipPlace;
    [SerializeField] private AudioClip clipRotate;   // звук перемикача
    [SerializeField] private AudioClip clipDelete;
    [SerializeField] private AudioClip clipModeEnter;
    [SerializeField] private AudioClip clipModeExit;

    [Header("Particles")]
    [SerializeField] private ParticleSystem pickupFX;
    [SerializeField] private ParticleSystem placeFX;
    [SerializeField] private ParticleSystem rotateFX;

    [Header("UI")]
    [SerializeField] private GameObject freePlaceIndicator;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void PlaySound(SoundType type)
    {
        AudioClip clip = type switch
        {
            SoundType.PickUp     => clipPickUp,
            SoundType.Place      => clipPlace,
            SoundType.Rotate     => clipRotate,
            SoundType.Delete     => clipDelete,
            SoundType.ModeEnter  => clipModeEnter,
            SoundType.ModeExit   => clipModeExit,
            _                    => null
        };
        if (clip != null && sfxSource != null)
            sfxSource.PlayOneShot(clip);
    }

    public void PlayUISound(SoundType type) => PlaySound(type);

    public void SpawnPickupParticles(Vector3 pos)  => SpawnFX(pickupFX, pos);
    public void SpawnPlaceParticles(Vector3 pos)   => SpawnFX(placeFX, pos);
    public void SpawnRotateParticles(Vector3 pos)  => SpawnFX(rotateFX, pos);

    private void SpawnFX(ParticleSystem ps, Vector3 pos)
    {
        if (ps == null) return;
        ps.transform.position = pos;
        ps.Play();
    }

    public void ShowFreePlaceIndicator(bool show)
    {
        if (freePlaceIndicator != null)
            freePlaceIndicator.SetActive(show);
    }
}