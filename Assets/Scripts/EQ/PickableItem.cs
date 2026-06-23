using UnityEngine;
using System.Collections.Generic;

public class PickableItem : MonoBehaviour
{
    [Header("Item Settings")]
    public string itemName;
    public Sprite interactionIcon;
    public float soundRange = 16f; // Zasiêg s³yszalnoœci dla AI przy upadku

    [Header("Audio Settings")]
    public AudioClip dropSound;
    private AudioSource audioSource;

    private Rigidbody rb;
    private Collider col;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        audioSource = GetComponent<AudioSource>();

        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
    }

    public void OnPickUp(Transform handTransform)
    {
        rb.isKinematic = true;
        col.enabled = false;
        transform.SetParent(handTransform);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }

    public void OnDrop()
    {
        transform.SetParent(null);
        rb.isKinematic = false;
        col.enabled = true;
        rb.AddForce(transform.forward * 2f, ForceMode.Impulse);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.relativeVelocity.magnitude > 1.5f && dropSound != null)
        {
            audioSource.PlayOneShot(dropSound);
            // POWIADOMIENIE AI O HA£ASIE UPADKU:
            EnemyAI.Instance?.RegisterNoise(transform.position, soundRange);
        }
    }
}