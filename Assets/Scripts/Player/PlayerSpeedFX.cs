using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlayerSpeedFX : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Volume speedVolume;

    [Header("FOV")]
    [SerializeField] private float normalFOV = 75f;
    [SerializeField] private float maxFOV = 90f;
    [SerializeField] private float fovSmoothSpeed = 8f;

    [Header("Speed")]
    [SerializeField] private float speedForMaximumFX = 11f;

    [Header("Vignette")]
    [SerializeField] private float maxVignette = 0.18f;

    [Header("Chromatic Aberration")]
    [SerializeField] private float maxChromatic = 0.35f;

    [Header("Lens Distortion")]
    [SerializeField] private float maxLensDistortion = -0.15f;

    [Header("Effect Smoothing")]
    [SerializeField] private float effectSmoothSpeed = 6f;

    private Vignette vignette;
    private ChromaticAberration chromatic;
    private LensDistortion lensDistortion;

    private void Awake()
    {
        if (speedVolume == null)
        {
            Debug.LogError("PlayerSpeedFX: Speed Volume is not assigned.");
            return;
        }

        VolumeProfile profile = speedVolume.profile;

        profile.TryGet(out vignette);
        profile.TryGet(out chromatic);
        profile.TryGet(out lensDistortion);

        if (vignette == null)
            Debug.LogError("Vignette override not found.");

        if (chromatic == null)
            Debug.LogError("Chromatic Aberration override not found.");

        if (lensDistortion == null)
            Debug.LogError("Lens Distortion override not found.");

        playerCamera.fieldOfView = normalFOV;
    }

    private void Update()
    {
        if (playerMovement == null)
            return;

        float speed = playerMovement.CurrentSpeed;

        float speedPercent = Mathf.Clamp01(
            speed / speedForMaximumFX
        );

        UpdateFOV(speedPercent);
        UpdateVignette(speedPercent);
        UpdateChromatic(speedPercent);
        UpdateLensDistortion(speedPercent);
    }

    private void UpdateFOV(float speedPercent)
    {
        float targetFOV = Mathf.Lerp(
            normalFOV,
            maxFOV,
            speedPercent
        );

        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFOV,
            fovSmoothSpeed * Time.deltaTime
        );
    }

    private void UpdateVignette(float speedPercent)
    {
        if (vignette == null)
            return;

        float targetIntensity =
            maxVignette * speedPercent;

        vignette.intensity.value = Mathf.Lerp(
            vignette.intensity.value,
            targetIntensity,
            effectSmoothSpeed * Time.deltaTime
        );
    }

    private void UpdateChromatic(float speedPercent)
    {
        if (chromatic == null)
            return;

        float targetIntensity =
            maxChromatic * speedPercent;

        chromatic.intensity.value = Mathf.Lerp(
            chromatic.intensity.value,
            targetIntensity,
            effectSmoothSpeed * Time.deltaTime
        );
    }

    private void UpdateLensDistortion(float speedPercent)
    {
        if (lensDistortion == null)
            return;

        float targetIntensity =
            maxLensDistortion * speedPercent;

        lensDistortion.intensity.value = Mathf.Lerp(
            lensDistortion.intensity.value,
            targetIntensity,
            effectSmoothSpeed * Time.deltaTime
        );
    }
}