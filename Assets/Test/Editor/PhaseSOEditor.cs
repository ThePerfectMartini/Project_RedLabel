#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PhaseSO 전용 커스텀 에디터.
/// ActionEditorStyles 공유 스타일 시스템으로 ActionDataDrawer와 통일된 외관 유지.
/// </summary>
[CustomEditor(typeof(PhaseSO))]
public class PhaseSOEditor : Editor
{
    private SerializedProperty entriesProp;
    private SerializedProperty isInfiniteLoopProp;
    private SerializedProperty repeatCountProp;
    private SerializedProperty repeatPenaltyProp;
    private SerializedProperty targetTagProp;

    private List<bool> foldouts = new List<bool>();

    // 그래프 미리보기 크기
    private const int previewHeight = 60;

    private void OnEnable()
    {
        entriesProp        = serializedObject.FindProperty("entries");
        isInfiniteLoopProp = serializedObject.FindProperty("isInfiniteLoop");
        repeatCountProp    = serializedObject.FindProperty("repeatCount");
        repeatPenaltyProp  = serializedObject.FindProperty("repeatPenalty");
        targetTagProp      = serializedObject.FindProperty("targetTag");

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

        // ── 전역 설정 헤더 ──
        ActionEditorStyles.DrawHeaderLayout("▶ 페이즈 전역 설정");

        EditorGUILayout.PropertyField(isInfiniteLoopProp, new GUIContent("무한 반복"));
        if (!isInfiniteLoopProp.boolValue)
            EditorGUILayout.PropertyField(repeatCountProp, new GUIContent("반복 횟수"));

        EditorGUILayout.PropertyField(repeatPenaltyProp, new GUIContent("연속 선택 패널티 비율"));
        EditorGUILayout.PropertyField(targetTagProp,     new GUIContent("타겟 태그"));

        GUILayout.Space(6);
        ActionEditorStyles.DrawDividerLayout();
        GUILayout.Space(4);

        // ── 엔트리 목록 헤더 + 일괄 버튼 ──
        ActionEditorStyles.DrawHeaderLayout("▶ 액션 시퀀스 엔트리 목록");

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("모두 접기",  GUILayout.Width(80))) SetAllFoldouts(false);
        if (GUILayout.Button("모두 펼치기", GUILayout.Width(80))) SetAllFoldouts(true);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        int deleteIndex    = -1;
        int moveUpIndex    = -1;
        int moveDownIndex  = -1;

        for (int i = 0; i < entriesProp.arraySize; i++)
        {
            var entryProp          = entriesProp.GetArrayElementAtIndex(i);
            var seqProp            = entryProp.FindPropertyRelative("actionSequence");
            var baseWeightProp     = entryProp.FindPropertyRelative("baseWeight");
            var distModeProp       = entryProp.FindPropertyRelative("distanceMode");
            var interpProp         = entryProp.FindPropertyRelative("interpolation");
            var minDistProp        = entryProp.FindPropertyRelative("minDistance");
            var maxDistProp        = entryProp.FindPropertyRelative("maxDistance");
            var alignThresholdProp = entryProp.FindPropertyRelative("alignThreshold");
            var frontFacingProp    = entryProp.FindPropertyRelative("frontFacingOnly");
            var isComboProp        = entryProp.FindPropertyRelative("isComboStarter");
            var comboFollowUpsProp = entryProp.FindPropertyRelative("comboFollowUps");
            var comboGizmosProp    = entryProp.FindPropertyRelative("comboFollowUpGizmos");
            var showGizmosProp     = entryProp.FindPropertyRelative("showGizmos");
            var showRangeGizmoProp = entryProp.FindPropertyRelative("showRangeGizmo");

            // ── 헤더 행 ──
            string seqName  = seqProp.objectReferenceValue ? seqProp.objectReferenceValue.name : "(없음)";
            DistanceWeightMode mode = (DistanceWeightMode)distModeProp.enumValueIndex;
            string modeTag  = mode switch
            {
                DistanceWeightMode.CloseRange  => " [근거리]",
                DistanceWeightMode.FarRange    => " [원거리]",
                DistanceWeightMode.XAxis_Close => " [X돌진↓]",
                DistanceWeightMode.XAxis_Far   => " [X돌진↑]",
                DistanceWeightMode.ZAxis_Close => " [Z기습↓]",
                DistanceWeightMode.ZAxis_Far   => " [Z기습↑]",
                _                              => "",
            };

            string headerText = $"#{i}  {seqName}{modeTag}"
                + (isComboProp.boolValue ? " [콤보]" : "")
                + (showGizmosProp.boolValue ? " [기즈모]" : "")
                + (showRangeGizmoProp.boolValue ? " [범위]" : "");

            EditorGUILayout.BeginHorizontal();

            foldouts[i] = EditorGUILayout.Foldout(foldouts[i], headerText, true, EditorStyles.foldoutHeader);

            // 👁 공격 기즈모 토글
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = showGizmosProp.boolValue ? ActionEditorStyles.AccentGreen : Color.white;
            bool newGizmo = GUILayout.Toggle(showGizmosProp.boolValue,
                new GUIContent("👁", "공격 범위 기즈모 표시"),
                EditorStyles.miniButton, GUILayout.Width(26));
            if (newGizmo != showGizmosProp.boolValue) showGizmosProp.boolValue = newGizmo;

            // ◎ 거리 범위 토글
            GUI.backgroundColor = showRangeGizmoProp.boolValue ? ActionEditorStyles.AccentBlue : Color.white;
            bool newRange = GUILayout.Toggle(showRangeGizmoProp.boolValue,
                new GUIContent("◎", "거리 Min/Max 범위 기즈모 표시"),
                EditorStyles.miniButton, GUILayout.Width(26));
            if (newRange != showRangeGizmoProp.boolValue) showRangeGizmoProp.boolValue = newRange;
            GUI.backgroundColor = prevBg;

            // 순서 이동 / 삭제 버튼
            GUI.enabled = i > 0;
            if (GUILayout.Button("▲", GUILayout.Width(25))) moveUpIndex = i;
            GUI.enabled = i < entriesProp.arraySize - 1;
            if (GUILayout.Button("▼", GUILayout.Width(25))) moveDownIndex = i;
            GUI.enabled = true;
            if (GUILayout.Button("✕", GUILayout.Width(25))) deleteIndex = i;

            EditorGUILayout.EndHorizontal();

            // ── 상세 내용 ──
            if (!foldouts[i]) continue;

            EditorGUI.indentLevel++;

            // 기본 설정
            ActionEditorStyles.DrawHeaderLayout("  ▸ 기본 설정");
            EditorGUILayout.PropertyField(seqProp,        new GUIContent("액션 시퀀스"));
            EditorGUILayout.PropertyField(baseWeightProp, new GUIContent("기본 가중치"));

            GUILayout.Space(4);

            // 거리 가중치 설정
            ActionEditorStyles.DrawHeaderLayout("  ▸ 거리 가중치 설정");
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
            distModeProp.enumValueIndex = EditorGUILayout.Popup("거리 모드", distModeProp.enumValueIndex, modeNames);

            DistanceWeightMode currentMode = (DistanceWeightMode)distModeProp.enumValueIndex;
            bool isAxisAligned = currentMode == DistanceWeightMode.XAxis_Close
                || currentMode == DistanceWeightMode.XAxis_Far
                || currentMode == DistanceWeightMode.ZAxis_Close
                || currentMode == DistanceWeightMode.ZAxis_Far;

            if (currentMode != DistanceWeightMode.Constant)
            {
                string[] shapeNames = { "직선 (Linear)", "완만→급격 (Ease In)", "급격→완만 (Ease Out)", "S자 곡선 (Ease In-Out)" };
                interpProp.enumValueIndex = EditorGUILayout.Popup("보간 형태", interpProp.enumValueIndex, shapeNames);

                EditorGUILayout.PropertyField(minDistProp, new GUIContent("최소 거리"));
                EditorGUILayout.PropertyField(maxDistProp, new GUIContent("최대 거리"));

                // 값 보정
                if (minDistProp.floatValue < 0f)  minDistProp.floatValue = 0f;
                if (maxDistProp.floatValue <= minDistProp.floatValue)
                    maxDistProp.floatValue = minDistProp.floatValue + 1f;

                // 미니 그래프 미리보기
                GUILayout.Space(4);
                DrawMiniGraphPreview(currentMode, (InterpolationShape)interpProp.enumValueIndex,
                    minDistProp.floatValue, maxDistProp.floatValue);

                if (isAxisAligned)
                {
                    GUILayout.Space(4);
                    EditorGUILayout.PropertyField(alignThresholdProp, new GUIContent("정렬 허용 오차"));
                    EditorGUILayout.PropertyField(frontFacingProp,    new GUIContent("정면 전용 판정"));
                }
            }

            GUILayout.Space(4);

            // 콤보 설정
            ActionEditorStyles.DrawHeaderLayout("  ▸ 콤보 설정");
            EditorGUILayout.PropertyField(isComboProp, new GUIContent("★ 콤보 시작 여부"));

            if (isComboProp.boolValue)
            {
                EditorGUILayout.HelpBox(
                    "이 시퀀스(공격)가 타격에 성공하면 아래 후속 콤보 시퀀스를 순차 실행합니다.\n" +
                    "타격 실패 시 콤보가 끊기고 다시 가중치 선택으로 돌아갑니다.",
                    MessageType.Info);

                // 기즈모 배열 크기 동기화
                while (comboGizmosProp.arraySize < comboFollowUpsProp.arraySize)
                    comboGizmosProp.InsertArrayElementAtIndex(comboGizmosProp.arraySize);
                while (comboGizmosProp.arraySize > comboFollowUpsProp.arraySize)
                    comboGizmosProp.DeleteArrayElementAtIndex(comboGizmosProp.arraySize - 1);

                EditorGUILayout.LabelField("콤보 후속 시퀀스", EditorStyles.boldLabel);

                int comboDeleteIndex = -1;
                for (int j = 0; j < comboFollowUpsProp.arraySize; j++)
                {
                    var elemProp  = comboFollowUpsProp.GetArrayElementAtIndex(j);
                    var gizmoProp = comboGizmosProp.GetArrayElementAtIndex(j);

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(elemProp, new GUIContent($"  후속 [{j}]"), GUILayout.MinWidth(80));

                    Color prevBg2 = GUI.backgroundColor;
                    GUI.backgroundColor = gizmoProp.boolValue ? ActionEditorStyles.AccentGreen : Color.white;
                    bool ng = GUILayout.Toggle(gizmoProp.boolValue,
                        new GUIContent("👁", "콤보 후속 기즈모 표시"),
                        EditorStyles.miniButton, GUILayout.Width(26));
                    if (ng != gizmoProp.boolValue) gizmoProp.boolValue = ng;
                    GUI.backgroundColor = prevBg2;

                    if (GUILayout.Button("✕", GUILayout.Width(22))) comboDeleteIndex = j;
                    EditorGUILayout.EndHorizontal();
                }

                if (comboDeleteIndex >= 0)
                {
                    var targetProp = comboFollowUpsProp.GetArrayElementAtIndex(comboDeleteIndex);
                    if (targetProp.objectReferenceValue != null)
                        targetProp.objectReferenceValue = null;
                    else
                        comboFollowUpsProp.DeleteArrayElementAtIndex(comboDeleteIndex);

                    if (comboDeleteIndex < comboGizmosProp.arraySize)
                        comboGizmosProp.DeleteArrayElementAtIndex(comboDeleteIndex);
                }

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("+ 후속 추가", GUILayout.Width(90)))
                {
                    comboFollowUpsProp.InsertArrayElementAtIndex(comboFollowUpsProp.arraySize);
                    comboFollowUpsProp.GetArrayElementAtIndex(comboFollowUpsProp.arraySize - 1).objectReferenceValue = null;
                    comboGizmosProp.InsertArrayElementAtIndex(comboGizmosProp.arraySize);
                    comboGizmosProp.GetArrayElementAtIndex(comboGizmosProp.arraySize - 1).boolValue = false;
                }
                EditorGUILayout.EndHorizontal();
            }

            EditorGUI.indentLevel--;
            GUILayout.Space(3);
            ActionEditorStyles.DrawDividerLayout();
        }

        // ── 엔트리 추가 버튼 ──
        GUILayout.Space(4);
        if (GUILayout.Button("+ 엔트리 추가", GUILayout.Height(28)))
        {
            entriesProp.InsertArrayElementAtIndex(entriesProp.arraySize);
            foldouts.Add(true);
        }

        // ── 삭제/이동 처리 ──
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

    // ═══════════════════════════════════════════════════════════
    // 미니 그래프 미리보기
    // ═══════════════════════════════════════════════════════════

    private void DrawMiniGraphPreview(DistanceWeightMode mode, InterpolationShape shape, float min, float max)
    {
        EditorGUILayout.LabelField("  미리보기:", ActionEditorStyles.MiniLabel);
        Rect graphRect = GUILayoutUtility.GetRect(0, previewHeight, GUILayout.ExpandWidth(true));
        graphRect.x     += 30f;
        graphRect.width -= 60f;

        // 배경 + 테두리
        EditorGUI.DrawRect(graphRect, new Color(0.12f, 0.14f, 0.18f, 1f));
        DrawRectBorder(graphRect, ActionEditorStyles.DividerColor);

        // 그래프 라인
        Handles.BeginGUI();
        bool isCloseType = mode == DistanceWeightMode.CloseRange
            || mode == DistanceWeightMode.XAxis_Close
            || mode == DistanceWeightMode.ZAxis_Close;

        Color graphColor = isCloseType
            ? ActionEditorStyles.AccentBlue
            : new Color(1f, 0.55f, 0.1f, 1f);

        float totalRange = max * 1.3f;
        Vector3 prevPoint = Vector3.zero;
        const int segments = 80;

        for (int s = 0; s <= segments; s++)
        {
            float normalized = (float)s / segments;
            float dist = normalized * totalRange;

            float multiplier;
            if (min >= max)
            {
                multiplier = 1f;
            }
            else
            {
                float t       = Mathf.Clamp01((dist - min) / (max - min));
                float shaped  = ApplyShapePreview(t, shape);
                multiplier    = isCloseType ? 1f - shaped : shaped;
            }

            float px = graphRect.x + normalized * graphRect.width;
            float py = graphRect.yMax - multiplier * graphRect.height;
            Vector3 point = new Vector3(px, py, 0);

            if (s > 0) { Handles.color = graphColor; Handles.DrawLine(prevPoint, point); }
            prevPoint = point;
        }

        // min/max 세로 마커
        float minX = graphRect.x + (min / totalRange) * graphRect.width;
        float maxX = graphRect.x + (max / totalRange) * graphRect.width;
        Handles.color = ActionEditorStyles.WarningYellow * new Color(1,1,1,0.5f);
        Handles.DrawLine(new Vector3(minX, graphRect.y, 0), new Vector3(minX, graphRect.yMax, 0));
        Handles.DrawLine(new Vector3(maxX, graphRect.y, 0), new Vector3(maxX, graphRect.yMax, 0));
        Handles.EndGUI();

        // 축 라벨
        GUIStyle labelStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
        GUI.Label(new Rect(graphRect.x - 28, graphRect.y - 2,    28, 14), "1.0", labelStyle);
        GUI.Label(new Rect(graphRect.x - 28, graphRect.yMax - 12, 28, 14), "0.0", labelStyle);
        GUI.Label(new Rect(minX - 8, graphRect.yMax + 1, 40, 14), $"{min:F0}m", labelStyle);
        GUI.Label(new Rect(maxX - 8, graphRect.yMax + 1, 40, 14), $"{max:F0}m", labelStyle);
    }

    private float ApplyShapePreview(float t, InterpolationShape shape)
    {
        switch (shape)
        {
            case InterpolationShape.Linear:    return t;
            case InterpolationShape.EaseIn:    return t * t;
            case InterpolationShape.EaseOut:   return 1f - (1f - t) * (1f - t);
            case InterpolationShape.EaseInOut: return t < 0.5f ? 2f * t * t : 1f - 2f * (1f - t) * (1f - t);
            default: return t;
        }
    }

    private void DrawRectBorder(Rect rect, Color color)
    {
        EditorGUI.DrawRect(new Rect(rect.x,          rect.y,          rect.width, 1),          color);
        EditorGUI.DrawRect(new Rect(rect.x,          rect.yMax - 1,   rect.width, 1),          color);
        EditorGUI.DrawRect(new Rect(rect.x,          rect.y,          1,          rect.height), color);
        EditorGUI.DrawRect(new Rect(rect.xMax - 1,   rect.y,          1,          rect.height), color);
    }

    private void SetAllFoldouts(bool value)
    {
        for (int i = 0; i < foldouts.Count; i++)
            foldouts[i] = value;
    }
}
#endif
