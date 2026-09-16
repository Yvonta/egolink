using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Yvonta
{
public class EgoLinkSubtitles : MonoBehaviour
{
    public static EgoLinkSubtitles Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private Image backgroundPanel;

    [Header("Settings")]
    [SerializeField] private bool hideBackgroundWhenEmpty = true;
    [SerializeField] private float defaultTimeoutSeconds = 12.0f;

    private Coroutine activeDisplayRoutine;
    
    public static EgoLinkSubtitles Initialize()
    {
        if (Instance != null) return Instance;

        // 1. Create Manager GameObject
        GameObject managerGO = new GameObject("EgoLinkSubtitles");
        Instance = managerGO.AddComponent<EgoLinkSubtitles>();
        DontDestroyOnLoad(managerGO);

        // 2. Setup Canvas
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("SubtitleCanvas");
            canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            DontDestroyOnLoad(canvasGO);
        }

        // 3. Create TextMeshPro Element
        GameObject textGO = new GameObject("SubtitleText");
        textGO.transform.SetParent(canvas.transform, false);

        TextMeshProUGUI tmp = textGO.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = 32;
        tmp.alignment = TextAlignmentOptions.Bottom;
        tmp.color = Color.white;
        tmp.raycastTarget = false; // Prevent subtitles from blocking UI clicks

        // Anchor at bottom-center elevated ABOVE the bottom dialog bar
        RectTransform rect = textGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.1f, 0.18f); // Raised bottom anchor (18% height)
        rect.anchorMax = new Vector2(0.9f, 0.28f); // Raised top anchor (28% height)
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        // Assign reference to manager instance
        Instance.subtitleText = tmp;

        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != UnityEngine.Object.FindAnyObjectByType<EgoLinkSubtitles>())
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        ClearSubtitle();
    }

    public void DisplaySubtitle(string message, float duration = -1f, Color? textColor = null)
    {
        float showDuration = (duration > 0f) ? duration : defaultTimeoutSeconds;

        if (activeDisplayRoutine != null)
        {
            StopCoroutine(activeDisplayRoutine);
        }

        activeDisplayRoutine = StartCoroutine(ShowSubtitleRoutine(message, showDuration, textColor ?? Color.white));
    }

    public void ClearSubtitle()
    {
        if (activeDisplayRoutine != null)
        {
            StopCoroutine(activeDisplayRoutine);
            activeDisplayRoutine = null;
        }

        if (subtitleText != null)
        {
            subtitleText.text = string.Empty;
        }

        SetVisibility(false);
    }

    private IEnumerator ShowSubtitleRoutine(string message, float duration, Color textColor)
    {
        subtitleText.color = textColor;
        subtitleText.text = message;
        SetVisibility(true);

        yield return new WaitForSeconds(duration);

        ClearSubtitle();
    }

    private void SetVisibility(bool visible)
    {
        if (subtitleText != null)
        {
            subtitleText.enabled = visible;
        }

        if (backgroundPanel != null && hideBackgroundWhenEmpty)
        {
            backgroundPanel.enabled = visible;
        }
    }
}
}