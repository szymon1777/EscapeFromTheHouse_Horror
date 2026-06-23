using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerInteraction : MonoBehaviour
{
    [Header("Raycast Settings")]
    public float interactionDistance = 3f;
    public LayerMask interactionLayer;

    [Header("Hands & Inventory")]
    public Transform handSlot;
    [HideInInspector] public PickableItem heldItem = null;

    [Header("UI Elements")]
    public Image crosshairImage;
    public Sprite defaultCrosshair;
    public TextMeshProUGUI promptText; // DODANE: Miejsce na tekst podpowiedzi TMP

    [HideInInspector] public bool canInteract = true;

    void Update()
    {
        if (!canInteract)
        {
            if (promptText != null) promptText.text = "";
            crosshairImage.sprite = defaultCrosshair;
            return;
        }

        CheckRaycast();

        // Klawisz E lub Lewy Myszki - Interakcja
        if (Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0))
        {
            PerformInteraction();
        }

        // Spacja - Upuszczenie przedmiotu (zgodnie z Twoim kodem)
        if (Input.GetKeyDown(KeyCode.Space))
        {
            DropCurrentItem();
        }
    }

    private void CheckRaycast()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactionDistance, interactionLayer))
        {
            // Sprawdzamy czy to przedmiot do podniesienia
            PickableItem item = hit.collider.GetComponent<PickableItem>();
            if (item != null)
            {
                crosshairImage.sprite = item.interactionIcon != null ? item.interactionIcon : defaultCrosshair;
                if (promptText != null) promptText.text = $"Podnieœ: {item.itemName}";
                return;
            }

            // Sprawdzamy czy to obiekt interaktywny (k³ódka, drzwi itp.)
            InteractiveObject interactive = hit.collider.GetComponent<InteractiveObject>();
            if (interactive != null)
            {
                crosshairImage.sprite = interactive.interactionIcon != null ? interactive.interactionIcon : defaultCrosshair;
                if (promptText != null) promptText.text = interactive.GetPromptMessage(heldItem);
                return;
            }
        }

        // Jeœli nic nie trafiliœmy, przywróæ domyœlny celownik i wyczyœæ tekst
        crosshairImage.sprite = defaultCrosshair;
        if (promptText != null) promptText.text = "";
    }

    private void PerformInteraction()
    {
        Ray ray = new Ray(transform.position, transform.forward);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, interactionDistance, interactionLayer))
        {
            // 1. Próba interakcji z obiektem typu k³ódka/drzwi
            InteractiveObject interactive = hit.collider.GetComponent<InteractiveObject>();
            if (interactive != null)
            {
                if (interactive.TryInteract(heldItem))
                {
                    if (interactive.requiresItem && interactive.destroyOnUse) heldItem = null;
                }
                return;
            }

            // 2. Próba podniesienia przedmiotu
            PickableItem item = hit.collider.GetComponent<PickableItem>();
            if (item != null)
            {
                if (heldItem != null)
                {
                    DropCurrentItem();
                }

                heldItem = item;
                heldItem.OnPickUp(handSlot);
            }
        }
    }

    public void DropCurrentItem()
    {
        if (heldItem != null)
        {
            heldItem.OnDrop();
            heldItem = null;
        }
    }
}