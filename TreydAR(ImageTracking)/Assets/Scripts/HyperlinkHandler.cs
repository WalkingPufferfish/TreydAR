using UnityEngine;
using TMPro; // Required for TextMeshPro
using UnityEngine.EventSystems; // Required for IPointerClickHandler

[RequireComponent(typeof(TextMeshProUGUI))]
public class HyperlinkHandler : MonoBehaviour, IPointerClickHandler
{
    private TextMeshProUGUI pTextMeshPro;
    private Camera uiCamera; // Used for screen-to-world point conversion for links

    public string googleFormURL = "YOUR_GOOGLE_FORM_URL_HERE"; // Paste your GForm URL here

    void Awake()
    {
        pTextMeshPro = GetComponent<TextMeshProUGUI>();

        // Try to find a Canvas and its camera.
        // This assumes the TextMeshPro object is on a Canvas.
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = null; // No camera needed for ScreenSpaceOverlay
            }
            else
            {
                uiCamera = canvas.worldCamera; // For ScreenSpaceCamera or WorldSpace
            }
        }
        else
        {
            Debug.LogWarning("HyperlinkHandler: Could not find parent Canvas. Link detection might not work correctly if not ScreenSpaceOverlay.", this);
        }

        if (string.IsNullOrEmpty(googleFormURL) || googleFormURL == "YOUR_GOOGLE_FORM_URL_HERE")
        {
            Debug.LogError("HyperlinkHandler: Google Form URL is not set in the Inspector!", this);
            enabled = false; // Disable if URL is missing
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Check if the click was on a link
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(pTextMeshPro, eventData.position, uiCamera);

        if (linkIndex != -1) // A link was clicked
        {
            TMP_LinkInfo linkInfo = pTextMeshPro.textInfo.linkInfo[linkIndex];
            string linkID = linkInfo.GetLinkID();

            // Check if the clicked link ID matches the one we expect for the feedback form
            if (linkID == "gform_feedback") // This ID must match the ID in your TextMeshPro text
            {
                Debug.Log($"Clicked on link ID: {linkID}. Opening URL: {googleFormURL}");
                Application.OpenURL(googleFormURL);
            }
            // You can add more else if blocks here if you have multiple links in the same text field
            // else if (linkID == "another_link_id") { ... }
        }
    }
}