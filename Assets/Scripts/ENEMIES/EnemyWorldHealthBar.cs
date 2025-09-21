using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Health))]
public class EnemyWorldHealthBar : MonoBehaviour
{
    [Header("Choose one:")]
    public bool usePrefabUI = false;       // use prefab or auto-build
    public Canvas prefabCanvas;            // World Space canvas prefab
    public Image prefabFill;               // Fill Image inside prefab

    [Header("Sprites (runtime build only)")]
    public Sprite borderSprite;
    public Sprite fillSprite;

    [Header("Layout (world units, runtime-build only)")]
    public Vector2 sizeWorld = new Vector2(1.6f, 0.22f);
    public Vector3 worldOffset = new Vector3(0f, 2.2f, 0f);
    public float worldScale = 0.02f;

    [Header("Behavior")]
    public bool faceCamera = true;
    public bool hideWhenFull = false;
    public bool smoothFill = true;
    public float fillLerpSpeed = 12f;
    public bool forceAlwaysShow = false;
    public float visibleDistanceOverride = -1f;

    [Header("Optional Gradient")]
    public bool useGradient = true;
    public Gradient barGradient = new Gradient
    {
        colorKeys = new[]
        {
            new GradientColorKey(new Color(0.9f, 0.2f, 0.2f), 0f),  // low = red
            new GradientColorKey(new Color(0.2f, 0.8f, 0.25f), 1f), // high = green
        },
        alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
    };

    // internals
    Canvas canvas;
    RectTransform root;
    Image fillImg;
    Camera cam;
    Health hp;
    Transform self, player;
    ChaserEnemyAI ai;
    float targetFill = 1f;

    void Awake()
    {
        self = transform;
        hp = GetComponent<Health>();
        ai = GetComponent<ChaserEnemyAI>();

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p) player = p.transform;

        if (usePrefabUI)
        {
            canvas = prefabCanvas;
            root = prefabCanvas.GetComponent<RectTransform>();
            fillImg = prefabFill;
        }
        else BuildRuntimeUI();

        hp.onHealthChanged.AddListener(OnHealthChanged);
        OnHealthChanged(hp.currentHP, hp.maxHP);

        if (canvas) canvas.enabled = true;
    }

    void OnDestroy()
    {
        if (hp) hp.onHealthChanged.RemoveListener(OnHealthChanged);
    }

    void LateUpdate()
    {
        if (!canvas) return;

        if (!cam) cam = Camera.main;

        if (root)
        {
            root.position = self.position + worldOffset;
            if (faceCamera && cam)
                root.rotation = Quaternion.LookRotation(root.position - cam.transform.position, Vector3.up);
        }

        // visibility
        bool show = forceAlwaysShow;
        if (!forceAlwaysShow)
        {
            float maxDist = (visibleDistanceOverride > 0f) ? visibleDistanceOverride
                         : (ai ? ai.interestRadius : 0f);

            if (player && maxDist > 0f)
            {
                float d2 = (player.position - self.position).sqrMagnitude;
                show = d2 <= maxDist * maxDist;
            }
            else show = true;

            if (hideWhenFull && Mathf.Approximately(targetFill, 1f))
                show = false;
        }
        canvas.enabled = show;

        // fill updates
        if (fillImg)
        {
            if (smoothFill)
            {
                float t = 1f - Mathf.Exp(-fillLerpSpeed * Time.unscaledDeltaTime);
                fillImg.fillAmount = Mathf.Lerp(fillImg.fillAmount, targetFill, t);
            }
            else fillImg.fillAmount = targetFill;

            if (useGradient)
                fillImg.color = barGradient.Evaluate(fillImg.fillAmount);
        }
    }

    void OnHealthChanged(float current, float max)
    {
        targetFill = (max > 0f) ? Mathf.Clamp01(current / max) : 0f;
        if (fillImg && !smoothFill)
        {
            fillImg.fillAmount = targetFill;
            if (useGradient) fillImg.color = barGradient.Evaluate(targetFill);
        }
    }

    void BuildRuntimeUI()
    {
        var go = new GameObject("HealthBar3D", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        root = go.GetComponent<RectTransform>();
        root.SetParent(transform, false);
        root.localPosition = worldOffset;
        root.localScale = Vector3.one * worldScale;
        root.sizeDelta = sizeWorld;

        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var scaler = go.GetComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        // --- Border ---
        var borderGO = new GameObject("Border", typeof(RectTransform), typeof(Image));
        var borderRT = borderGO.GetComponent<RectTransform>();
        borderRT.SetParent(root, false);
        borderRT.anchorMin = Vector2.zero;
        borderRT.anchorMax = Vector2.one;
        borderRT.offsetMin = Vector2.zero;
        borderRT.offsetMax = Vector2.zero;

        var borderImg = borderGO.GetComponent<Image>();
        borderImg.sprite = borderSprite;
        borderImg.type = Image.Type.Sliced;     // force sliced
        borderImg.fillCenter = false;           // disable center fill
        borderImg.pixelsPerUnitMultiplier = 175f; // your PPU override
        borderImg.raycastTarget = false;        // no need for raycast

        // --- Fill ---
        var fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        var fillRT = fillGO.GetComponent<RectTransform>();
        fillRT.SetParent(root, false);
        float inset = sizeWorld.y * 0.18f;
        fillRT.anchorMin = new Vector2(0f, 0f);
        fillRT.anchorMax = new Vector2(1f, 1f);
        fillRT.offsetMin = new Vector2(inset, inset);
        fillRT.offsetMax = new Vector2(-inset, -inset);

        fillImg = fillGO.GetComponent<Image>();
        fillImg.sprite = fillSprite;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 1f;
        fillImg.color = Color.white;
        fillImg.raycastTarget = false;

        // make sure the border renders above the fill
        borderGO.transform.SetAsLastSibling();
    }
}
