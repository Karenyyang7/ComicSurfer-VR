using UnityEngine;

using System.Collections;

/// <summary>
/// Handles page flipping on the open book.
/// Each "page" is a child object of the open book with its comic art as a texture.
/// When the player reaches the thief page, it triggers the thief spawn.
/// 
/// SETUP:
/// 1. Add this script to your open book model
/// 2. Create child objects for each page (Quads or Planes with comic textures)
///    Name them Page_0, Page_1, Page_2, etc.
///    OR drag them into the pages array manually
/// 3. Set thiefPageIndex to whichever page triggers the thief
/// 4. Drag your ThiefSpawner into the thiefSpawner slot
/// 5. Drag your UIInstructions into the instructions slot
/// 6. Start this script DISABLED (TableSnapZone enables it when book opens)
/// 
/// HOW PAGE FLIPPING WORKS:
/// The player grabs the right side of the book to flip forward,
/// or the left side to flip backward. Two invisible grab zones
/// are created automatically on either side of the book.
/// </summary>
public class BookPageFlipper : MonoBehaviour
{
    [Header("=== Pages ===")]
    [Tooltip("Drag page objects here in order, OR name them Page_0, Page_1, etc.")]
    public GameObject[] pages;

    [Tooltip("Which page triggers the thief (0-indexed)")]
    public int thiefPageIndex = 3;

    [Header("=== References ===")]
    public ThiefSpawner thiefSpawner;
    public UIInstructions instructions;

    [Header("=== Flip Settings ===")]
    [Tooltip("Time for a page flip animation")]
    public float flipDuration = 0.5f;

    [Tooltip("Sound to play on page flip (optional)")]
    public AudioClip flipSound;

    // Internal
    private int currentPage = 0;
    private bool isFlipping = false;
    private AudioSource audioSource;
    private GameObject flipForwardZone;
    private GameObject flipBackwardZone;

    void Start()
    {
        // Auto-find pages if not manually assigned
        if (pages == null || pages.Length == 0)
        {
            FindPagesAutomatically();
        }

        // Set up audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null && flipSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 1f; // 3D sound
        }

        // Create grab zones for flipping
        CreateFlipZones();

        // Show the first page, hide the rest
        ShowPage(0);

        if (instructions != null)
        {
            instructions.ShowMessage("Flip through the pages...");
        }
    }

    void FindPagesAutomatically()
    {
        // Look for children named Page_0, Page_1, etc.
        var pageList = new System.Collections.Generic.List<GameObject>();
        int i = 0;
        while (true)
        {
            Transform page = transform.Find("Page_" + i);
            if (page == null) break;
            pageList.Add(page.gameObject);
            i++;
        }

        if (pageList.Count > 0)
        {
            pages = pageList.ToArray();
            Debug.Log("BookPageFlipper: Found " + pages.Length + " pages automatically");
        }
        else
        {
            Debug.LogWarning("BookPageFlipper: No pages found! Create child objects named Page_0, Page_1, etc.");
        }
    }

    void CreateFlipZones()
    {
        // Right side of book = flip forward
        flipForwardZone = CreateGrabZone("FlipForward", new Vector3(0.1f, 0.02f, 0));
        var forwardInteractable = flipForwardZone.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        forwardInteractable.selectEntered.AddListener((args) => FlipForward());

        // Left side of book = flip backward
        flipBackwardZone = CreateGrabZone("FlipBackward", new Vector3(-0.1f, 0.02f, 0));
        var backwardInteractable = flipBackwardZone.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
        backwardInteractable.selectEntered.AddListener((args) => FlipBackward());
    }

    GameObject CreateGrabZone(string name, Vector3 localPos)
    {
        GameObject zone = new GameObject(name);
        zone.transform.SetParent(transform);
        zone.transform.localPosition = localPos;
        zone.transform.localRotation = Quaternion.identity;

        // Add a collider for the interaction zone
        BoxCollider col = zone.AddComponent<BoxCollider>();
        col.size = new Vector3(0.08f, 0.05f, 0.15f);
        col.isTrigger = true;

        // Add simple interactable (not grabbable, just selectable)
        UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable interactable = zone.AddComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();

        return zone;
    }

    public void FlipForward()
    {
        if (isFlipping) return;
        if (pages == null || pages.Length == 0) return;
        if (currentPage >= pages.Length - 1) return;

        StartCoroutine(FlipAnimation(currentPage, currentPage + 1, true));
    }

    public void FlipBackward()
    {
        if (isFlipping) return;
        if (pages == null || pages.Length == 0) return;
        if (currentPage <= 0) return;

        StartCoroutine(FlipAnimation(currentPage, currentPage - 1, false));
    }

    IEnumerator FlipAnimation(int fromPage, int toPage, bool forward)
    {
        isFlipping = true;

        // Play flip sound
        if (audioSource != null && flipSound != null)
        {
            audioSource.PlayOneShot(flipSound);
        }

        // Simple flip: rotate the current page out, show the next page
        GameObject fromObj = pages[fromPage];
        GameObject toObj = pages[toPage];

        // Animate the page rotating (like turning a real page)
        float elapsed = 0;
        Quaternion startRot = fromObj.transform.localRotation;
        float targetAngle = forward ? -180f : 180f;
        Quaternion endRot = startRot * Quaternion.Euler(0, targetAngle, 0);

        while (elapsed < flipDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flipDuration;

            // Ease in/out
            t = t * t * (3f - 2f * t);

            fromObj.transform.localRotation = Quaternion.Lerp(startRot, endRot, t);
            yield return null;
        }

        // Hide the old page, show the new one
        fromObj.SetActive(false);
        fromObj.transform.localRotation = startRot; // reset rotation
        toObj.SetActive(true);

        currentPage = toPage;
        isFlipping = false;

        Debug.Log("Now on page " + currentPage);

        // Check if this is the thief page
        if (currentPage == thiefPageIndex)
        {
            OnThiefPageReached();
        }
    }

    void ShowPage(int pageIndex)
    {
        if (pages == null) return;

        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null)
                pages[i].SetActive(i == pageIndex);
        }
        currentPage = pageIndex;
    }

    void OnThiefPageReached()
    {
        Debug.Log("THIEF PAGE REACHED!");

        if (instructions != null)
        {
            instructions.ShowMessage("!!!");
        }

        // Make the book glow again
        // Find the GlowingBook component if it exists on the original book
        GlowingBook glowingBook = FindObjectOfType<GlowingBook>();
        if (glowingBook != null)
        {
            glowingBook.StartGlowing();
        }

        // Spawn the thief!
        if (thiefSpawner != null)
        {
            thiefSpawner.SpawnThief();
        }
    }
}
