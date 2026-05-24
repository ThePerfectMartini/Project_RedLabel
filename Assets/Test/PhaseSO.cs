using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 하나의 페이즈 안에서 여러 액션 시퀀스를 가중치 기반으로 선택하고,
/// 콤보 연쇄까지 지원하는 ScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "NewPhase", menuName = "Phase")]
public class PhaseSO : ScriptableObject
{
    [Header("페이즈 전역 설정")]
    [Tooltip("이 페이즈를 무한 반복할지 여부")]
    public bool isInfiniteLoop = true;
    [Tooltip("유한 반복 시 반복 횟수")]
    public int repeatCount = 3;

    [Tooltip("직전 선택 시퀀스에 적용할 가중치 패널티 비율 (0 = 완전 차단, 1 = 패널티 없음)")]
    [Range(0f, 1f)]
    public float repeatPenalty = 0.2f;

    [Tooltip("거리 계산에 사용할 타겟 태그 (보통 Player)")]
    public string targetTag = "Player";

    public List<PhaseEntry> entries = new List<PhaseEntry>();
}

// ═══════════════════════════════════════════════════════════════
// 거리 가중치 관련 열거형
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// 거리에 따른 가중치 변화 방향
/// </summary>
public enum DistanceWeightMode
{
    /// <summary>거리와 무관하게 항상 동일한 가중치</summary>
    Constant,
    /// <summary>가까울수록 가중치가 높음 (근접 공격용)</summary>
    CloseRange,
    /// <summary>멀수록 가중치가 높음 (원거리 공격용)</summary>
    FarRange,
    /// <summary>Z축이 정렬되었을 때 X축이 가까울수록 높음 (X축 돌진 근거리용)</summary>
    XAxis_Close,
    /// <summary>Z축이 정렬되었을 때 X축이 멀수록 높음 (X축 돌진 원거리용)</summary>
    XAxis_Far,
    /// <summary>X축이 정렬되었을 때 Z축이 가까울수록 높음 (Z축 기습 근거리용)</summary>
    ZAxis_Close,
    /// <summary>X축이 정렬되었을 때 Z축이 멀수록 높음 (Z축 기습 원거리용)</summary>
    ZAxis_Far,
}

/// <summary>
/// min ~ max 사이의 보간 형태
/// </summary>
public enum InterpolationShape
{
    /// <summary>직선 (일정한 기울기)</summary>
    Linear,
    /// <summary>완만하게 시작 → 급격히 변화 (가속형)</summary>
    EaseIn,
    /// <summary>급격히 시작 → 완만하게 마무리 (감속형)</summary>
    EaseOut,
    /// <summary>양쪽 완만, 중간 급격 (S자 곡선)</summary>
    EaseInOut
}

[System.Serializable]
public class PhaseEntry
{
    [Tooltip("실행할 액션 시퀀스")]
    public ActionSequenceSO actionSequence;

    [Tooltip("기본 가중치 (높을수록 선택 확률 증가)")]
    public float baseWeight = 1f;

    [Header("거리 가중치 설정")]
    [Tooltip("거리에 따른 가중치 변화 방향")]
    public DistanceWeightMode distanceMode = DistanceWeightMode.Constant;

    [Tooltip("보간 형태 (직선, 곡선 등)")]
    public InterpolationShape interpolation = InterpolationShape.Linear;

    [Tooltip("효과 시작 거리")]
    public float minDistance = 0f;

    [Tooltip("효과 종료 거리")]
    public float maxDistance = 10f;

    [Tooltip("정렬 허용 오차: 정렬 축 방향의 차이가 이 값 이하일 때만 가중치 작동 (축 정렬 모드 전용)")]
    public float alignThreshold = 0.5f;
    [Tooltip("정면 전용 판정: 플레이어가 이 캐릭터의 정면 방향에 있을 때만 가중치 작동 (축 정렬 모드 전용)")]
    public bool frontFacingOnly = false;

    [Header("콤보 설정")]
    [Tooltip("이 시퀀스가 콤보의 시작인지 여부 (타격 성공 시 후속 콤보 연쇄)")]
    public bool isComboStarter;

    [Tooltip("콤보 후속 시퀀스 목록 (타격 성공 시 순차 실행)")]
    public List<ActionSequenceSO> comboFollowUps = new List<ActionSequenceSO>();

    /// <summary>
    /// 현재 상대 위치에 대한 가중치 곱수를 반환.
    /// 축 정렬 모드일 경우 정렬 조건 미충족 시 0을 반환합니다.
    /// </summary>
    /// <param name="relativePos">몬스터 기준 플레이어의 상대 위치 (Y=0)</param>
    /// <param name="monsterForward">몬스터의 정면 방향 벡터</param>
    public float EvaluateDistanceMultiplier(Vector3 relativePos, Vector3 monsterForward)
    {
        if (distanceMode == DistanceWeightMode.Constant)
            return 1f;

        if (minDistance >= maxDistance)
            return 1f;

        // 기존 단순 거리 모드 (Magnitude 사용)
        if (distanceMode == DistanceWeightMode.CloseRange || distanceMode == DistanceWeightMode.FarRange)
        {
            float distance = relativePos.magnitude;
            float t = Mathf.Clamp01((distance - minDistance) / (maxDistance - minDistance));
            float shaped = ApplyShape(t);
            return distanceMode == DistanceWeightMode.CloseRange ? 1f - shaped : shaped;
        }

        // 축 정렬 모드
        float alignAxisDist, measureAxisDist;

        if (distanceMode == DistanceWeightMode.XAxis_Close || distanceMode == DistanceWeightMode.XAxis_Far)
        {
            alignAxisDist   = Mathf.Abs(relativePos.z); // Z축 정렬 판정
            measureAxisDist = Mathf.Abs(relativePos.x); // X축 거리 측정
        }
        else // ZAxis_Close / ZAxis_Far
        {
            alignAxisDist   = Mathf.Abs(relativePos.x); // X축 정렬 판정
            measureAxisDist = Mathf.Abs(relativePos.z); // Z축 거리 측정
        }

        // 정렬 허용 오차 초과 시 0 반환
        if (alignAxisDist > alignThreshold) return 0f;

        // 정면 전용 판정: 플레이어가 정면 방향에 없으면 0 반환
        if (frontFacingOnly && Vector3.Dot(relativePos, monsterForward) <= 0f) return 0f;

        // 측정 축 거리 → 가중치 보간
        float tAxis = Mathf.Clamp01((measureAxisDist - minDistance) / (maxDistance - minDistance));
        float shapedAxis = ApplyShape(tAxis);

        bool isClose = distanceMode == DistanceWeightMode.XAxis_Close || distanceMode == DistanceWeightMode.ZAxis_Close;
        return isClose ? 1f - shapedAxis : shapedAxis;
    }

    private float ApplyShape(float t)
    {
        switch (interpolation)
        {
            case InterpolationShape.Linear:
                return t;
            case InterpolationShape.EaseIn:
                return t * t;
            case InterpolationShape.EaseOut:
                return 1f - (1f - t) * (1f - t);
            case InterpolationShape.EaseInOut:
                return t < 0.5f
                    ? 2f * t * t
                    : 1f - 2f * (1f - t) * (1f - t);
            default:
                return t;
        }
    }
}

// ═══════════════════════════════════════════════════════════════
// 커스텀 에디터
// ═══════════════════════════════════════════════════════════════
#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(PhaseSO))]
public class PhaseSOEditor : UnityEditor.Editor
{
    private UnityEditor.SerializedProperty entriesProp;
    private UnityEditor.SerializedProperty isInfiniteLoopProp;
    private UnityEditor.SerializedProperty repeatCountProp;
    private UnityEditor.SerializedProperty repeatPenaltyProp;
    private UnityEditor.SerializedProperty targetTagProp;

    private List<bool> foldouts = new List<bool>();

    // 그래프 미리보기용
    private const int previewWidth = 200;
    private const int previewHeight = 60;

    private void OnEnable()
    {
        entriesProp = serializedObject.FindProperty("entries");
        isInfiniteLoopProp = serializedObject.FindProperty("isInfiniteLoop");
        repeatCountProp = serializedObject.FindProperty("repeatCount");
        repeatPenaltyProp = serializedObject.FindProperty("repeatPenalty");
        targetTagProp = serializedObject.FindProperty("targetTag");

        SyncFoldouts();
    }

    private void SyncFoldouts()
    {
        while (foldouts.Count < entriesProp.arraySize)
            foldouts.Add(false);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SyncFoldouts();

        // ── 페이즈 전역 설정 ──
        UnityEditor.EditorGUILayout.LabelField("▶ 페이즈 전역 설정", UnityEditor.EditorStyles.boldLabel);
        UnityEditor.EditorGUILayout.PropertyField(isInfiniteLoopProp, new GUIContent("무한 반복"));
        if (!isInfiniteLoopProp.boolValue)
            UnityEditor.EditorGUILayout.PropertyField(repeatCountProp, new GUIContent("반복 횟수"));

        UnityEditor.EditorGUILayout.PropertyField(repeatPenaltyProp, new GUIContent("연속 선택 패널티 비율"));
        UnityEditor.EditorGUILayout.PropertyField(targetTagProp, new GUIContent("타겟 태그"));

        UnityEditor.EditorGUILayout.Space(10);
        DrawDivider();
        UnityEditor.EditorGUILayout.Space(5);

        // ── 엔트리 리스트 ──
        UnityEditor.EditorGUILayout.LabelField("▶ 액션 시퀀스 엔트리 목록", UnityEditor.EditorStyles.boldLabel);
        UnityEditor.EditorGUILayout.Space(3);

        int deleteIndex = -1;
        int moveUpIndex = -1;
        int moveDownIndex = -1;

        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            var entryProp = entriesProp.GetArrayElementAtIndex(i);
            var seqProp = entryProp.FindPropertyRelative("actionSequence");
            var baseWeightProp = entryProp.FindPropertyRelative("baseWeight");
            var distModeProp = entryProp.FindPropertyRelative("distanceMode");
            var interpProp = entryProp.FindPropertyRelative("interpolation");
            var minDistProp = entryProp.FindPropertyRelative("minDistance");
            var maxDistProp           = entryProp.FindPropertyRelative("maxDistance");
            var alignThresholdProp    = entryProp.FindPropertyRelative("alignThreshold");
            var frontFacingOnlyProp   = entryProp.FindPropertyRelative("frontFacingOnly");
            var isComboProp           = entryProp.FindPropertyRelative("isComboStarter");
            var comboFollowUpsProp    = entryProp.FindPropertyRelative("comboFollowUps");

            // Foldout 헤더
            string seqName = seqProp.objectReferenceValue
                ? seqProp.objectReferenceValue.name
                : "(없음)";

            string comboTag = isComboProp.boolValue ? " [콤보]" : "";

            // 모드 태그
            DistanceWeightMode mode = (DistanceWeightMode)distModeProp.enumValueIndex;
            string modeTag = "";
            switch (mode)
            {
                case DistanceWeightMode.CloseRange:  modeTag = " [근거리]";  break;
                case DistanceWeightMode.FarRange:    modeTag = " [원거리]";  break;
                case DistanceWeightMode.XAxis_Close: modeTag = " [X돌진↓]"; break;
                case DistanceWeightMode.XAxis_Far:   modeTag = " [X돌진↑]"; break;
                case DistanceWeightMode.ZAxis_Close: modeTag = " [Z기습↓]"; break;
                case DistanceWeightMode.ZAxis_Far:   modeTag = " [Z기습↑]"; break;
            }

            UnityEditor.EditorGUILayout.BeginHorizontal();

            foldouts[i] = UnityEditor.EditorGUILayout.Foldout(foldouts[i],
                $"#{i}  {seqName}{modeTag}{comboTag}", true, UnityEditor.EditorStyles.foldoutHeader);

            GUI.enabled = i > 0;
            if (GUILayout.Button("▲", GUILayout.Width(25))) moveUpIndex = i;
            GUI.enabled = i < entriesProp.arraySize - 1;
            if (GUILayout.Button("▼", GUILayout.Width(25))) moveDownIndex = i;
            GUI.enabled = true;
            if (GUILayout.Button("✕", GUILayout.Width(25))) deleteIndex = i;

            UnityEditor.EditorGUILayout.EndHorizontal();

            if (foldouts[i])
            {
                UnityEditor.EditorGUI.indentLevel++;

                UnityEditor.EditorGUILayout.PropertyField(seqProp, new GUIContent("액션 시퀀스"));
                UnityEditor.EditorGUILayout.PropertyField(baseWeightProp, new GUIContent("기본 가중치"));

                UnityEditor.EditorGUILayout.Space(5);

                // ── 거리 가중치 설정 ──
                string[] modeNames =
                {
                    "항상 동일 (거리 무관)",
                    "근거리 우세 (가까울수록 ↑)",
                    "원거리 우세 (멀수록 ↑)",
                    "X축 돌진 – 근거리 (Z 정렬 + X 가까울수록 ↑)",
                    "X축 돌진 – 원거리 (Z 정렬 + X 멀수록 ↑)",
                    "Z축 기습 – 근거리 (X 정렬 + Z 가까울수록 ↑)",
                    "Z축 기습 – 원거리 (X 정렬 + Z 멀수록 ↑)",
                };
                distModeProp.enumValueIndex = UnityEditor.EditorGUILayout.Popup(
                    "거리 모드", distModeProp.enumValueIndex, modeNames);

                DistanceWeightMode currentMode = (DistanceWeightMode)distModeProp.enumValueIndex;
                bool isAxisAligned = currentMode == DistanceWeightMode.XAxis_Close
                    || currentMode == DistanceWeightMode.XAxis_Far
                    || currentMode == DistanceWeightMode.ZAxis_Close
                    || currentMode == DistanceWeightMode.ZAxis_Far;

                if (currentMode != DistanceWeightMode.Constant)
                {
                    string[] shapeNames = { "직선 (Linear)", "완만→급격 (Ease In)", "급격→완만 (Ease Out)", "S자 곡선 (Ease In-Out)" };
                    interpProp.enumValueIndex = UnityEditor.EditorGUILayout.Popup(
                        "보간 형태", interpProp.enumValueIndex, shapeNames);

                    UnityEditor.EditorGUILayout.PropertyField(minDistProp, new GUIContent("최소 거리"));
                    UnityEditor.EditorGUILayout.PropertyField(maxDistProp, new GUIContent("최대 거리"));

                    // 값 보정
                    if (minDistProp.floatValue < 0f) minDistProp.floatValue = 0f;
                    if (maxDistProp.floatValue <= minDistProp.floatValue)
                        maxDistProp.floatValue = minDistProp.floatValue + 1f;

                    // ── 미니 그래프 프리뷰 ──
                    UnityEditor.EditorGUILayout.Space(4);
                    DrawMiniGraphPreview(
                        currentMode,
                        (InterpolationShape)interpProp.enumValueIndex,
                        minDistProp.floatValue,
                        maxDistProp.floatValue);

                    // ── 축 정렬 전용 옵션 ──
                    if (isAxisAligned)
                    {
                        UnityEditor.EditorGUILayout.Space(4);
                        UnityEditor.EditorGUILayout.PropertyField(alignThresholdProp, new GUIContent("정렬 허용 오차"));
                        UnityEditor.EditorGUILayout.PropertyField(frontFacingOnlyProp, new GUIContent("정면 전용 판정"));
                    }
                }

                UnityEditor.EditorGUILayout.Space(5);
                UnityEditor.EditorGUILayout.PropertyField(isComboProp, new GUIContent("★ 콤보 시작 여부"));

                if (isComboProp.boolValue)
                {
                    UnityEditor.EditorGUILayout.HelpBox(
                        "이 시퀀스(공격)가 타격에 성공하면 아래 후속 콤보 시퀀스를 순차 실행합니다.\n" +
                        "타격 실패 시 콤보가 끊기고 다시 가중치 선택으로 돌아갑니다.",
                        UnityEditor.MessageType.Info);

                    UnityEditor.EditorGUILayout.PropertyField(comboFollowUpsProp, new GUIContent("콤보 후속 시퀀스"), true);
                }

                UnityEditor.EditorGUI.indentLevel--;
            }

            UnityEditor.EditorGUILayout.Space(2);
        }

        // 엔트리 추가 버튼
        UnityEditor.EditorGUILayout.Space(5);
        if (GUILayout.Button("+ 엔트리 추가", GUILayout.Height(28)))
        {
            entriesProp.InsertArrayElementAtIndex(entriesProp.arraySize);
            foldouts.Add(true);
        }

        // 삭제 / 이동 처리
        if (deleteIndex >= 0)
        {
            entriesProp.DeleteArrayElementAtIndex(deleteIndex);
            if (deleteIndex < foldouts.Count) foldouts.RemoveAt(deleteIndex);
        }
        if (moveUpIndex > 0)
        {
            entriesProp.MoveArrayElement(moveUpIndex, moveUpIndex - 1);
            (foldouts[moveUpIndex], foldouts[moveUpIndex - 1]) = (foldouts[moveUpIndex - 1], foldouts[moveUpIndex]);
        }
        if (moveDownIndex >= 0 && moveDownIndex < entriesProp.arraySize - 1)
        {
            entriesProp.MoveArrayElement(moveDownIndex, moveDownIndex + 1);
            (foldouts[moveDownIndex], foldouts[moveDownIndex + 1]) = (foldouts[moveDownIndex + 1], foldouts[moveDownIndex]);
        }

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// 인스펙터에 미니 그래프를 직접 그려서 현재 설정이 어떤 모양인지 미리보기
    /// </summary>
    private void DrawMiniGraphPreview(DistanceWeightMode mode, InterpolationShape shape, float min, float max)
    {
        UnityEditor.EditorGUILayout.LabelField("  미리보기:", UnityEditor.EditorStyles.miniLabel);
        Rect graphRect = GUILayoutUtility.GetRect(previewWidth, previewHeight, GUILayout.ExpandWidth(true));
        graphRect.x += 30f;
        graphRect.width -= 60f;

        // 배경
        UnityEditor.EditorGUI.DrawRect(graphRect, new Color(0.15f, 0.15f, 0.15f, 1f));

        // 테두리
        DrawRectBorder(graphRect, new Color(0.4f, 0.4f, 0.4f, 1f));

        // 그래프 그리기
        UnityEditor.Handles.BeginGUI();
        
        bool isCloseType = mode == DistanceWeightMode.CloseRange
            || mode == DistanceWeightMode.XAxis_Close
            || mode == DistanceWeightMode.ZAxis_Close;
        Color graphColor = isCloseType
            ? new Color(0.3f, 0.8f, 1f, 1f)
            : new Color(1f, 0.6f, 0.2f, 1f);

        // PhaseEntry의 EvaluateDistanceMultiplier 로직을 직접 재현
        Vector3 prevPoint = Vector3.zero;
        int segments = 80;
        float totalRange = max * 1.3f; // 그래프 여유 공간

        for (int s = 0; s <= segments; s++)
        {
            float normalized = (float)s / segments;
            float dist = normalized * totalRange;

            // 곱수 계산
            float multiplier;
            if (mode == DistanceWeightMode.Constant || min >= max)
            {
                multiplier = 1f;
            }
            else
            {
                float t = Mathf.Clamp01((dist - min) / (max - min));
                float shaped = ApplyShapeEditor(t, shape);
                multiplier = isCloseType ? 1f - shaped : shaped;
            }

            float px = graphRect.x + normalized * graphRect.width;
            float py = graphRect.yMax - multiplier * graphRect.height;

            Vector3 point = new Vector3(px, py, 0);

            if (s > 0)
            {
                UnityEditor.Handles.color = graphColor;
                UnityEditor.Handles.DrawLine(prevPoint, point);
            }
            prevPoint = point;
        }

        // min/max 마커 세로줄
        float minX = graphRect.x + (min / totalRange) * graphRect.width;
        float maxX = graphRect.x + (max / totalRange) * graphRect.width;

        UnityEditor.Handles.color = new Color(1f, 1f, 0f, 0.4f);
        UnityEditor.Handles.DrawLine(
            new Vector3(minX, graphRect.y, 0),
            new Vector3(minX, graphRect.yMax, 0));
        UnityEditor.Handles.DrawLine(
            new Vector3(maxX, graphRect.y, 0),
            new Vector3(maxX, graphRect.yMax, 0));

        UnityEditor.Handles.EndGUI();

        // 축 라벨
        GUIStyle labelStyle = new GUIStyle(UnityEditor.EditorStyles.miniLabel);
        labelStyle.normal.textColor = Color.gray;

        GUI.Label(new Rect(graphRect.x - 5, graphRect.y - 2, 30, 14), "1.0", labelStyle);
        GUI.Label(new Rect(graphRect.x - 5, graphRect.yMax - 12, 30, 14), "0.0", labelStyle);
        GUI.Label(new Rect(minX - 8, graphRect.yMax + 1, 40, 14), $"{min:F0}", labelStyle);
        GUI.Label(new Rect(maxX - 8, graphRect.yMax + 1, 40, 14), $"{max:F0}", labelStyle);
    }

    private float ApplyShapeEditor(float t, InterpolationShape shape)
    {
        switch (shape)
        {
            case InterpolationShape.Linear: return t;
            case InterpolationShape.EaseIn: return t * t;
            case InterpolationShape.EaseOut: return 1f - (1f - t) * (1f - t);
            case InterpolationShape.EaseInOut:
                return t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);
            default: return t;
        }
    }

    private void DrawRectBorder(Rect rect, Color color)
    {
        UnityEditor.EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), color);
        UnityEditor.EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), color);
        UnityEditor.EditorGUI.DrawRect(new Rect(rect.x, rect.y, 1, rect.height), color);
        UnityEditor.EditorGUI.DrawRect(new Rect(rect.xMax - 1, rect.y, 1, rect.height), color);
    }

    private void DrawDivider()
    {
        Rect r = UnityEditor.EditorGUILayout.GetControlRect(false, 1);
        UnityEditor.EditorGUI.DrawRect(r, new Color(0.5f, 0.5f, 0.5f, 1f));
    }
}
#endif
