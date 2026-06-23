using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Collections;

// Definicje struktur zintegrowane w jednym pliku, aby uniknπÊ b≥ÍdÛw zduplikowanych klas
public enum NoiseType
{
    ItemDrop,
    DoorInteraction,
    LockUnlock,
    PlayerFootstep
}

public struct NoiseData
{
    public Vector3 position;
    public float radius;
    public NoiseType type;

    public NoiseData(Vector3 pos, float rad, NoiseType t)
    {
        this.position = pos;
        this.radius = rad;
        this.type = t;
    }
}

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public static EnemyAI Instance;

    [Header("References")]
    public Transform playerTransform;
    public LayerMask obstacleLayer;  // Warstwa úcian blokujπcych wzrok
    public LayerMask doorLayer;      // Warstwa drzwi
    public List<Transform> patrolPoints; // Sta≥e punkty patrolowe

    [Header("Senses - Field of View")]
    public float baseViewDistance = 15f;
    [Range(0f, 360f)] public float viewAngle = 110f;

    [Header("Senses - Separate Hearing Limits (Inspector)")]
    [Tooltip("ZasiÍg, w jakim AI s≥yszy bieg gracza")]
    public float hearRunningRadius = 15f;

    [Tooltip("Maksymalny zasiÍg z jakiego AI us≥yszy upuszczony przedmiot")]
    public float maxItemDropHearingRadius = 35f;

    [Tooltip("Maksymalny zasiÍg z jakiego AI us≥yszy interakcjÍ z drzwiami")]
    public float maxDoorHearingRadius = 25f;

    [Tooltip("Maksymalny zasiÍg z jakiego AI us≥yszy rozwalenie/otwarcie k≥Ûdki")]
    public float maxLockHearingRadius = 40f;

    [Header("Audio")]
    public AudioSource chaseMusicSource; // ZapÍtlona muzyka poúcigu
    public AudioClip killSound;          // DüwiÍk jumpscare / krzyku wroga

    private NavMeshAgent agent;
    private Movement playerMovement;
    private PlayerInteraction playerInteraction;

    private List<NoiseData> dynamicNoiseQueue = new List<NoiseData>();
    private int currentPatrolIndex = 0;
    private bool isChasing = false;
    private bool isExecutingKill = false;

    void Awake()
    {
        Instance = this;
        agent = GetComponent<NavMeshAgent>();
        playerMovement = playerTransform.GetComponent<Movement>();
        playerInteraction = playerTransform.GetComponentInChildren<PlayerInteraction>();
    }

    void Update()
    {
        if (isExecutingKill) return;

        CheckSenses();
        ExecuteBehavior();
        AutoOpenDoorsAhead();
    }

    private void CheckSenses()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        float currentViewRange = baseViewDistance;

        if (playerMovement != null)
        {
            switch (playerMovement.moveMode)
            {
                case Movement.MoveMode.Crouch: currentViewRange *= 0.45f; break;
                case Movement.MoveMode.Walk: currentViewRange *= 1.0f; break;
                case Movement.MoveMode.Run: currentViewRange *= 1.45f; break;
            }
        }

        bool playerDetected = false;

        // 1. Sprawdzanie Wzroku
        if (distanceToPlayer <= currentViewRange)
        {
            Vector3 dirToPlayer = (playerTransform.position - transform.position).normalized;
            float angleBetween = Vector3.Angle(transform.forward, dirToPlayer);

            if (angleBetween < viewAngle / 2f)
            {
                if (!Physics.Linecast(transform.position + Vector3.up, playerTransform.position + Vector3.up, obstacleLayer))
                {
                    playerDetected = true;
                }
            }
        }

        // 2. S≥yszenie biegania gracza
        if (playerMovement != null && playerMovement.moveMode == Movement.MoveMode.Run)
        {
            if (distanceToPlayer <= hearRunningRadius)
            {
                playerDetected = true;
            }
        }

        if (playerDetected)
        {
            if (!isChasing)
            {
                isChasing = true;
                agent.isStopped = false;
                if (chaseMusicSource && !chaseMusicSource.isPlaying) chaseMusicSource.Play();
            }
        }
        else
        {
            if (isChasing && distanceToPlayer > currentViewRange)
            {
                isChasing = false;
                if (chaseMusicSource) chaseMusicSource.Stop();
            }
        }
    }

    private void ExecuteBehavior()
    {
        if (isChasing)
        {
            agent.SetDestination(playerTransform.position);

            // Sprawdzenie ataku
            if (Vector3.Distance(transform.position, playerTransform.position) <= agent.stoppingDistance + 0.5f)
            {
                if (!Physics.Linecast(transform.position + Vector3.up, playerTransform.position + Vector3.up, obstacleLayer))
                {
                    StartCoroutine(KillSequence());
                }
            }
        }
        else if (dynamicNoiseQueue.Count > 0)
        {
            // PrzywrÛcone p≥ynne poruszanie siÍ ze starego skryptu (bez bugujπcego isStopped)
            agent.SetDestination(dynamicNoiseQueue[0].position);

            if (!agent.pathPending && agent.remainingDistance <= 1.2f)
            {
                dynamicNoiseQueue.RemoveAt(0); // Punkt sprawdzony, usuwamy z kolejki i idziemy p≥ynnie dalej
            }
        }
        else
        {
            if (patrolPoints.Count == 0) return;
            agent.SetDestination(patrolPoints[currentPatrolIndex].position);

            if (!agent.pathPending && agent.remainingDistance <= 1.2f)
            {
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
            }
        }
    }

    public void RegisterNoise(NoiseData newNoise)
    {
        if (isChasing) return;

        float allowedHearingRange = 0f;
        switch (newNoise.type)
        {
            case NoiseType.ItemDrop: allowedHearingRange = maxItemDropHearingRadius; break;
            case NoiseType.DoorInteraction: allowedHearingRange = maxDoorHearingRadius; break;
            case NoiseType.LockUnlock: allowedHearingRange = maxLockHearingRadius; break;
            case NoiseType.PlayerFootstep: allowedHearingRange = hearRunningRadius; break;
        }

        float distanceToNoise = Vector3.Distance(transform.position, newNoise.position);

        Debug.Log($"[S£UCH AI] Wykryto: {newNoise.type}. Dystans: {distanceToNoise}m / Max: {allowedHearingRange}m");

        if (distanceToNoise > allowedHearingRange || distanceToNoise > newNoise.radius)
        {
            return;
        }

        if (dynamicNoiseQueue.Count >= 3)
        {
            dynamicNoiseQueue.RemoveAt(dynamicNoiseQueue.Count - 1);
        }
        dynamicNoiseQueue.Add(newNoise);

        // Sortowanie drogi
        dynamicNoiseQueue.Sort((a, b) => Vector3.Distance(transform.position, a.position).CompareTo(Vector3.Distance(transform.position, b.position)));
    }

    private void AutoOpenDoorsAhead()
    {
        RaycastHit hit;
        // ZwiÍkszony minimalnie zasiÍg promienia do 2.5f, øeby AI otwiera≥o drzwi u≥amek sekundy wczeúniej i nie zwalnia≥o
        if (Physics.Raycast(transform.position + Vector3.up, transform.forward, out hit, 2.5f, doorLayer))
        {
            InteractiveObject door = hit.collider.GetComponent<InteractiveObject>();
            if (door != null && door.objectType == InteractiveObject.ObjectType.Door && !door.isOpen)
            {
                door.isOpen = true;
            }
        }
    }

    private IEnumerator KillSequence()
    {
        isExecutingKill = true;

        // 1. NATYCHMIASTOWE CA£KOWITE UNIERUCHOMIENIE WROGA
        agent.isStopped = true;
        agent.ResetPath();
        if (chaseMusicSource) chaseMusicSource.Stop();

        // 2. BLOKADA LOGIKI I RUCHU GRACZA
        if (playerMovement != null) playerMovement.enabled = false;
        if (playerInteraction != null) playerInteraction.canInteract = false;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        // 3. NATYCHMIASTOWE ODTWORZENIE DèWI KU JUMPSCARE
        if (killSound != null)
        {
            AudioSource enemyAudio = GetComponent<AudioSource>();
            if (enemyAudio != null)
            {
                enemyAudio.Stop(); // Zatrzymujemy inne düwiÍki wroga
                enemyAudio.PlayOneShot(killSound);
            }
        }

        // 4. P£YNNE SKIEROWANIE KAMERY GRACZA NA OCZY WROGA
        Transform mainCam = Camera.main.transform;
        float elapsed = 0f;
        float duration = 0.35f; // Jak szybko kamera obraca siÍ na twarz wroga

        Vector3 targetEyeLevel = transform.position + Vector3.up * 1.4f;

        while (elapsed < duration)
        {
            Vector3 faceDirection = targetEyeLevel - mainCam.position;
            if (faceDirection != Vector3.zero)
            {
                Quaternion targetLook = Quaternion.LookRotation(faceDirection);
                mainCam.rotation = Quaternion.Slerp(mainCam.rotation, targetLook, elapsed / duration);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        mainCam.LookAt(targetEyeLevel);
        Debug.Log("JUMPSCARE WYKONANY - GRACZ NIE ØYJE");

        // 5. WY£•CZENIE SKRYPTU AI
        this.enabled = false;
    }

    private void OnDrawGizmos()
    {
        // 1. Wzrok (ØÛ≥ty)
        Gizmos.color = Color.yellow;
        Vector3 fovLeft = Quaternion.AngleAxis(-viewAngle / 2, Vector3.up) * transform.forward;
        Vector3 fovRight = Quaternion.AngleAxis(viewAngle / 2, Vector3.up) * transform.forward;
        Gizmos.DrawRay(transform.position + Vector3.up, fovLeft * baseViewDistance);
        Gizmos.DrawRay(transform.position + Vector3.up, fovRight * baseViewDistance);

        // 2. OkrÍgi s≥uchu wokÛ≥ wroga w Inspektorze
        Gizmos.color = new Color(1f, 0f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, hearRunningRadius);

        Gizmos.color = new Color(0f, 1f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, maxItemDropHearingRadius);

        Gizmos.color = new Color(1f, 0f, 1f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, maxDoorHearingRadius);

        Gizmos.color = new Color(0.8f, 0.1f, 0.1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, maxLockHearingRadius);

        // 3. Trasa do punktÛw ha≥asu
        if (dynamicNoiseQueue != null && dynamicNoiseQueue.Count > 0)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, dynamicNoiseQueue[0].position);

            for (int i = 0; i < dynamicNoiseQueue.Count; i++)
            {
                switch (dynamicNoiseQueue[i].type)
                {
                    case NoiseType.LockUnlock: Gizmos.color = Color.red; break;
                    case NoiseType.ItemDrop: Gizmos.color = Color.cyan; break;
                    case NoiseType.DoorInteraction: Gizmos.color = Color.magenta; break;
                }

                Gizmos.DrawSphere(dynamicNoiseQueue[i].position, 0.6f - (i * 0.15f));

                if (i < dynamicNoiseQueue.Count - 1)
                {
                    Gizmos.color = Color.grey;
                    Gizmos.DrawLine(dynamicNoiseQueue[i].position, dynamicNoiseQueue[i + 1].position);
                }
            }
        }

        // 4. Zielona kostka celu NavMesh
        if (Application.isPlaying && agent != null && agent.hasPath)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(agent.destination, new Vector3(0.5f, 0.1f, 0.5f));
        }
    }
}