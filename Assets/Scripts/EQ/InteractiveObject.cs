using UnityEngine;
using System.Collections.Generic;

public class InteractiveObject : MonoBehaviour
{
    public enum ObjectType { GenricLock, Door }

    [Header("General Settings")]
    public ObjectType objectType = ObjectType.GenricLock;
    public bool requiresItem;
    public List<string> requiredItemNames = new List<string>(); // Wsparcie dla jednego lub wielu przedmiotów

    [Header("UI & Interaction")]
    public Sprite interactionIcon;

    [Header("Audio & Effects")]
    public AudioClip interactionSound;
    public float interactionSoundRange = 10f;
    private AudioSource audioSource;

    [Header("Settings")]
    public bool destroyOnUse = false;

    [Header("Door Sub-Settings")]
    public float openAngle = 90f;
    public float doorSpeed = 5f;
    [HideInInspector] public bool isOpen = false;

    private Quaternion defaultRotation;
    private Quaternion openRotation;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

        defaultRotation = transform.localRotation;
        openRotation = Quaternion.Euler(transform.localEulerAngles + new Vector3(0, openAngle, 0));
    }

    void Update()
    {
        if (objectType == ObjectType.Door)
        {
            transform.localRotation = Quaternion.Slerp(transform.localRotation, isOpen ? openRotation : defaultRotation, Time.deltaTime * doorSpeed);
        }
    }

    public string GetPromptMessage(PickableItem currentHeldItem)
    {
        if (!requiresItem)
        {
            if (objectType == ObjectType.Door) return isOpen ? "Zamknij Drzwi" : "Otwórz Drzwi";
            return "Interakcja";
        }

        if (currentHeldItem != null && requiredItemNames.Contains(currentHeldItem.itemName))
        {
            return $"U¿yj {currentHeldItem.itemName}";
        }

        return $"Wymaga: {string.Join(" lub ", requiredItemNames)}";
    }

    public bool TryInteract(PickableItem currentHeldItem)
    {
        if (requiresItem)
        {
            if (currentHeldItem == null || !requiredItemNames.Contains(currentHeldItem.itemName))
            {
                return false;
            }
            requiresItem = false; // Odblokowano zamek
        }

        ExecuteInteraction(currentHeldItem);
        return true;
    }

    private void ExecuteInteraction(PickableItem itemUsed)
    {
        // Odtwórz dŸwiêk interakcji
        if (interactionSound != null)
        {
            audioSource.PlayOneShot(interactionSound);

            // POPRAWIONE: Okreœlamy typ dŸwiêku i pakujemy go w NoiseData
            NoiseType currentType = (objectType == ObjectType.Door) ? NoiseType.DoorInteraction : NoiseType.LockUnlock;
            NoiseData noise = new NoiseData(transform.position, interactionSoundRange, currentType);

            EnemyAI.Instance?.RegisterNoise(noise);

            Debug.Log($"Ha³as: U¿yto obiektu {gameObject.name} w pozycji {transform.position}");
        }

        // Jeœli u¿yliœmy przedmiotu (np. klucza do k³ódki), niszczymy ten przedmiot z rêki gracza
        if (requiresItem && itemUsed != null)
        {
            Destroy(itemUsed.gameObject);
        }

        // Logika zachowania obiektu (np. animacja otwierania drzwi lub zniszczenie k³ódki)
        if (objectType == ObjectType.Door)
        {
            isOpen = !isOpen;
        }
        else if (destroyOnUse)
        {
            Destroy(gameObject, 0.1f);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}