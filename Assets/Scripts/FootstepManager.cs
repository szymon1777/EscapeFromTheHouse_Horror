using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class FootstepManager : MonoBehaviour
{
    [Header("Referencje")]
    [SerializeField] private Movement movementScript; 

    [Header("DŸwiêki kroków")]
    [SerializeField] private AudioClip[] footstepClips;

    [Header("G³oœnoœæ dla stanów")]
    [Range(0f, 1f)][SerializeField] private float walkVolume = 0.4f;
    [Range(0f, 1f)][SerializeField] private float runVolume = 0.8f;
    [Range(0f, 1f)][SerializeField] private float crouchVolume = 0.15f;

    [Header("Ustawienia Losowoœci (Pitch)")]
    [Range(0f, 0.3f)][SerializeField] private float pitchRandomness = 0.12f;

    [Header("Zabezpieczenie przed dublowaniem")]
    [Tooltip("Minimalny czas w sekundach jaki musi up³yn¹æ miêdzy krokami")]
    [SerializeField] private float stepCooldown = 0.15f;

    private AudioSource audioSource;
    private float basePitch;
    private int lastClipIndex = -1;
    private float lastStepTime; 

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        basePitch = audioSource.pitch;

        
        if (movementScript == null)
        {
            movementScript = GetComponentInParent<Movement>();
            if (movementScript == null) movementScript = GetComponent<Movement>();
        }
    }

    public void PlayFootstep()
    {
        if (Time.time - lastStepTime < stepCooldown)
        {
            return;
        }

        if (footstepClips == null || footstepClips.Length == 0)
        {
            Debug.LogWarning("Brak przypisanych klipów audio w FootstepManager!");
            return;
        }

        float targetVolume = walkVolume;

        if (movementScript != null)
        {
            switch (movementScript.moveMode)
            {
                case Movement.MoveMode.Walk:
                    targetVolume = walkVolume;
                    break;
                case Movement.MoveMode.Run:
                    targetVolume = runVolume;
                    break;
                case Movement.MoveMode.Crouch:
                    targetVolume = crouchVolume;
                    break;
            }
        }

        int randomIndex = 0;
        if (footstepClips.Length > 1)
        {
            do
            {
                randomIndex = Random.Range(0, footstepClips.Length);
            } while (randomIndex == lastClipIndex);

            lastClipIndex = randomIndex;
        }

        AudioClip clipToPlay = footstepClips[randomIndex];

        audioSource.pitch = basePitch + Random.Range(-pitchRandomness, pitchRandomness);

        audioSource.PlayOneShot(clipToPlay, targetVolume);

        lastStepTime = Time.time;
    }
}