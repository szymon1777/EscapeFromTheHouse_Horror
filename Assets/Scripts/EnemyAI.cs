using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Collections;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyAI : MonoBehaviour
{
    public static EnemyAI Instance;

    [Header("References")]
    public Transform playerTransform;
    public LayerMask obstacleLayer;  // Warstwa œcian blokuj¹cych wzrok
    public LayerMask doorLayer;      // Warstwa drzwi, które AI potrafi otwieraæ przed sob¹
    public List<Transform> patrolPoints; // Twoje sta³e punkty patrolowe

    [Header("Senses Range")]
    public float baseViewDistance = 15f;
    public float viewAngle = 110f;
    public float hearRunningRadius = 14f; // Zasiêg, w którym us³yszy bieg gracza

    [Header("Audio")]
    public AudioSource chaseMusicSource; // Muzyka poœcigu w pêtli

    private NavMeshAgent agent;
    private Movement playerMovement;
    private PlayerInteraction playerInteraction;

    private List<Vector3> dynamicNoisePoints = new List<Vector3>(); // Max 3 ostatnie ha³asy
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

        // Widocznoœæ dynamiczna bazuj¹ca na stanie Twojego skryptu 'Movement'
        if (playerMovement != null)
        {
            switch (playerMovement.moveMode)
            {
                case Movement.MoveMode.Crouch: currentViewRange *= 0.45f; break; // Bardzo trudny do zauwa¿enia
                case Movement.MoveMode.Walk: currentViewRange *= 1.0f; break;
                case Movement.MoveMode.Run: currentViewRange *= 1.45f; break;   // Bardzo ³atwy do zauwa¿enia
            }
        }

        bool playerDetected = false;

        // 1. Sprawdzanie Wzroku (Z uwzglêdnieniem k¹ta i œcian)
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

        // 2. S³yszenie biegania gracza (w obrêbie Twojego skryptu chodzenia)
        if (playerMovement != null && playerMovement.moveMode == Movement.MoveMode.Run)
        {
            if (distanceToPlayer <= hearRunningRadius)
            {
                playerDetected = true;
            }
        }

        // Zarz¹dzanie stanem Agro i muzyk¹ poœcigu
        if (playerDetected)
        {
            if (!isChasing)
            {
                isChasing = true;
                if (chaseMusicSource && !chaseMusicSource.isPlaying) chaseMusicSource.Play();
            }
        }
        else
        {
            // Tracenie agro, gdy gracz zniknie z pola widzenia i zasiêgu
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

            // Sprawdzenie udanego ataku (Z³apanie)
            if (Vector3.Distance(transform.position, playerTransform.position) <= agent.stoppingDistance + 0.6f)
            {
                if (!Physics.Linecast(transform.position + Vector3.up, playerTransform.position + Vector3.up, obstacleLayer))
                {
                    StartCoroutine(KillSequence());
                }
            }
        }
        else if (dynamicNoisePoints.Count > 0)
        {
            // IdŸ do najœwie¿szego zoptymalizowanego punktu ha³asu
            agent.SetDestination(dynamicNoisePoints[0]);

            if (!agent.pathPending && agent.remainingDistance <= 1.2f)
            {
                dynamicNoisePoints.RemoveAt(0); // Punkt sprawdzony, usuwamy z kolejki
            }
        }
        else
        {
            // Sta³y, bezpieczny patrol stacjonarny
            if (patrolPoints.Count == 0) return;
            agent.SetDestination(patrolPoints[currentPatrolIndex].position);

            if (!agent.pathPending && agent.remainingDistance <= 1.2f)
            {
                currentPatrolIndex = (currentPatrolIndex + 1) % patrolPoints.Count;
            }
        }
    }

    // Wywo³ywane inteligentnie przez upuszczane przedmioty lub zamki
    public void RegisterNoise(Vector3 noisePos, float radius)
    {
        if (isChasing || Vector3.Distance(transform.position, noisePos) > radius) return;

        if (dynamicNoisePoints.Count >= 3)
        {
            dynamicNoisePoints.RemoveAt(dynamicNoisePoints.Count - 1); // Usuñ najstarszy z 3, jeœli jest przepe³nienie
        }
        dynamicNoisePoints.Add(noisePos);

        // Inteligentne sortowanie drogi (najkrótsza trasa od aktualnej pozycji AI)
        dynamicNoisePoints.Sort((a, b) => Vector3.Distance(transform.position, a).CompareTo(Vector3.Distance(transform.position, b)));
    }

    private void AutoOpenDoorsAhead()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position + Vector3.up, transform.forward, out hit, 2.2f, doorLayer))
        {
            InteractiveObject door = hit.collider.GetComponent<InteractiveObject>();
            if (door != null && door.objectType == InteractiveObject.ObjectType.Door && !door.isOpen)
            {
                door.isOpen = true; // AI automatycznie otwiera zamkniête drzwi przed sob¹
            }
        }
    }

    private IEnumerator KillSequence()
    {
        isExecutingKill = true;
        agent.isStopped = true;

        // Wy³¹czenie skryptów poruszania siê i interakcji Twojego gracza
        if (playerMovement != null) playerMovement.enabled = false;
        if (playerInteraction != null) playerInteraction.canInteract = false;

        // Blokada kursora
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        Transform mainCam = Camera.main.transform;
        float elapsed = 0f;

        // Nakierowanie i p³ynny obrót kamery na twarz wroga (Granny style)
        Vector3 faceDirection = (transform.position + Vector3.up * 1.3f) - mainCam.position;
        Quaternion targetLook = Quaternion.LookRotation(faceDirection);

        while (elapsed < 0.6f)
        {
            mainCam.rotation = Quaternion.Slerp(mainCam.rotation, targetLook, elapsed / 0.6f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Debug.Log("JUMPSCARE: Gracz nie ¿yje. Wyœwietl menu lub zresetuj dzieñ.");
        // Tutaj opcjonalnie dajesz: UnityEngine.SceneManagement.SceneManager.LoadScene(0);
    }
}