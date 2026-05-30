#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// ActionDataDrawer, PhaseSOEditor, ComboNodeSOEditor 가 공유하는
/// 통일된 비주얼 스타일 시스템.
///
/// Lazy 초기화(??=)를 사용하므로 GUIStyle 은 처음 접근할 때 한 번만 생성됩니다.
/// 커브 미리보기는 설정 변경 시에만 AnimationCurve 를 재생성해 GC 부담을 줄입니다.
/// </summary>
public static class ActionEditorStyles
{
    // ═══════════════════════════════════════════════════════════
    // 공용 색상 팔레트
    // ═══════════════════════════════════════════════════════════

    public static readonly Color HeaderBg       = new Color(0.18f, 0.22f, 0.28f, 1f);
    public static readonly Color HeaderTextColor = new Color(0.95f, 0.88f, 0.45f, 1f);
    public static readonly Color DividerColor   = new Color(0.4f, 0.45f, 0.5f, 0.8f);
    public static readonly Color SectionBg      = new Color(0.22f, 0.22f, 0.25f, 0.25f);
    public static readonly Color AccentBlue     = new Color(0.3f, 0.72f, 1f, 1f);
    public static readonly Color AccentGreen    = new Color(0.38f, 1f, 0.5f, 1f);
    public static readonly Color WarningYellow  = new Color(1f, 0.88f, 0.3f, 1f);

    // ═══════════════════════════════════════════════════════════
    // 공용 GUIStyle (Lazy 초기화)
    // ═══════════════════════════════════════════════════════════

    private static GUIStyle _headerLabel;
    public static GUIStyle HeaderLabel => _headerLabel ??= new GUIStyle(EditorStyles.boldLabel)
    {
        fontSize  = 11,
        normal    = { textColor = HeaderTextColor },
        padding   = new RectOffset(6, 0, 2, 2),
    };

    private static GUIStyle _sectionBox;
    public static GUIStyle SectionBox => _sectionBox ??= new GUIStyle("helpbox")
    {
        padding = new RectOffset(8, 8, 5, 5),
        margin  = new RectOffset(0, 0, 2, 2),
    };

    private static GUIStyle _miniLabel;
    public static GUIStyle MiniLabel => _miniLabel ??= new GUIStyle(EditorStyles.centeredGreyMiniLabel)
    {
        alignment = TextAnchor.MiddleLeft,
        normal    = { textColor = new Color(0.65f, 0.65f, 0.65f, 1f) },
    };

    // ═══════════════════════════════════════════════════════════
    // 공용 그리기 높이 상수
    // ═══════════════════════════════════════════════════════════

    public static float LineH => EditorGUIUtility.singleLineHeight;
    public static float LineStep => LineH + 2f; // 18px
    public static float HeaderHeight => LineH + 13f; // 29px
    public static float DividerHeight => 10f; // 10px
    public static float CurvePreviewHeight => 44f; // 44px

    // ═══════════════════════════════════════════════════════════
    // IMGUI Rect 기반 드로잉 유틸 (PropertyDrawer 용)
    // ═══════════════════════════════════════════════════════════

    /// <summary>현재 rect 위치에 섹션 헤더를 그리고 rect.y를 전진시킵니다.</summary>
    public static void DrawHeader(ref Rect rect, string title)
    {
        rect.y += 5f;
        Rect bgRect = new Rect(rect.x + 14f, rect.y - 1f, rect.width - 14f, LineH + 4f);
        EditorGUI.DrawRect(bgRect, HeaderBg);

        Rect labelRect = bgRect;
        labelRect.x     += 4f;
        labelRect.height = LineH;
        labelRect.y     += 2f;
        EditorGUI.LabelField(labelRect, title, HeaderLabel);

        rect.y += LineH + 8f; // 총 소모: HeaderHeight
    }

    /// <summary>현재 rect 위치에 1px 구분선을 그리고 rect.y를 전진시킵니다.</summary>
    public static void DrawDivider(ref Rect rect)
    {
        rect.y += 4f;
        Rect divRect = new Rect(rect.x + 15f, rect.y, rect.width - 15f, 1f);
        EditorGUI.DrawRect(divRect, DividerColor);
        rect.y += 5f; // 총 소모: DividerHeight
    }

    /// <summary>이징 곡선 미리보기를 그립니다. 설정이 변경된 경우에만 커브를 재생성합니다.</summary>
    public static void DrawCurvePreview(ref Rect rect, EaseType easeType, float exponent, string label)
    {
        Rect r = rect;
        r.height = 42f;
        EditorGUI.CurveField(r, label, GetCachedCurve(easeType, exponent));
        rect.y += r.height + 2f; // 총 소모: CurvePreviewHeight
    }

    // ═══════════════════════════════════════════════════════════
    // GUILayout 기반 드로잉 유틸 (커스텀 에디터 용)
    // ═══════════════════════════════════════════════════════════

    /// <summary>GUILayout 기반 섹션 헤더.</summary>
    public static void DrawHeaderLayout(string title)
    {
        GUILayout.Space(4);
        Rect bgRect = GUILayoutUtility.GetRect(0f, EditorGUIUtility.singleLineHeight + 6f, GUILayout.ExpandWidth(true));
        bgRect.x    += 14f;
        bgRect.width -= 28f;
        EditorGUI.DrawRect(bgRect, HeaderBg);

        Rect labelRect = bgRect;
        labelRect.x     += 4f;
        labelRect.height = EditorGUIUtility.singleLineHeight;
        labelRect.y     += 3f;
        GUI.Label(labelRect, title, HeaderLabel);
        GUILayout.Space(2);
    }

    /// <summary>GUILayout 기반 1px 구분선.</summary>
    public static void DrawDividerLayout()
    {
        GUILayout.Space(3);
        Rect r = GUILayoutUtility.GetRect(0f, 1f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, DividerColor);
        GUILayout.Space(3);
    }

    // ═══════════════════════════════════════════════════════════
    // 커브 캐싱 (매 프레임 재생성 방지)
    // ═══════════════════════════════════════════════════════════

    private static AnimationCurve _cachedCurve;
    private static EaseType       _cachedCurveType;
    private static float          _cachedCurveExponent = -1f;

    private static AnimationCurve GetCachedCurve(EaseType easeType, float exponent)
    {
        if (_cachedCurve != null
            && _cachedCurveType == easeType
            && Mathf.Approximately(_cachedCurveExponent, exponent))
        {
            return _cachedCurve;
        }

        _cachedCurve = new AnimationCurve();
        const int sampleCount = 20;
        for (int i = 0; i <= sampleCount; i++)
        {
            float t   = (float)i / sampleCount;
            float val = easeType == EaseType.EaseIn
                ? Mathf.Pow(t, exponent)
                : 1f - Mathf.Pow(1f - t, exponent);
            _cachedCurve.AddKey(t, val);
        }

        _cachedCurveType     = easeType;
        _cachedCurveExponent = exponent;
        return _cachedCurve;
    }

    // ═══════════════════════════════════════════════════════════
    // 스타일 초기화 강제 (도메인 리로드 대응)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// 도메인 리로드 후 캐싱된 GUIStyle이 null이 되는 현상을 방지하기 위해
    /// 에디터가 시작될 때 스타일 캐시를 초기화합니다.
    /// </summary>
    [InitializeOnLoadMethod]
    private static void OnDomainReload()
    {
        _headerLabel      = null;
        _sectionBox       = null;
        _miniLabel        = null;
        _cachedCurve      = null;
        _cachedCurveExponent = -1f;
    }
}
#endif
