#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ComboNodeSO 전용 커스텀 에디터.
/// PhaseSOEditor와 동일한 양식을 적용하여 일관된 UI 경험 제공.
/// </summary>
[CustomEditor(typeof(ComboNodeSO))]
public class ComboNodeSOEditor : Editor
{
    private SerializedProperty _comboName;
    private SerializedProperty _allowedState;
    private SerializedProperty _comboSteps;

    private List<bool> foldouts = new List<bool>();

    private void OnEnable()
    {
        _comboName    = serializedObject.FindProperty("comboName");
        _allowedState = serializedObject.FindProperty("allowedState");
        _comboSteps   = serializedObject.FindProperty("comboSteps");

        SyncFoldouts();
    }

    private void SyncFoldouts()
    {
        while (foldouts.Count < _comboSteps.arraySize)
            foldouts.Add(false);
        while (foldouts.Count > _comboSteps.arraySize)
            foldouts.RemoveAt(foldouts.Count - 1);
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SyncFoldouts();

        // ── 콤보 공통 설정 ──
        ActionEditorStyles.DrawHeaderLayout("▶ 콤보 기본 설정");
        EditorGUILayout.PropertyField(_comboName,    new GUIContent("콤보 이름"));

        string[] stateOptions = { "지상 전용 (공중 불가)", "공중 전용 (지상 불가)", "지상 + 공중 모두" };
        _allowedState.enumValueIndex = EditorGUILayout.Popup("발동 조건",
            _allowedState.enumValueIndex, stateOptions);

        GUILayout.Space(6);
        ActionEditorStyles.DrawDividerLayout();
        GUILayout.Space(4);

        // ── 단계 리스트 헤더 + 일괄 버튼 ──
        ActionEditorStyles.DrawHeaderLayout("▶ 콤보 단계 목록");

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("모두 접기",  GUILayout.Width(80))) SetAllExpanded(false);
        if (GUILayout.Button("모두 펼치기", GUILayout.Width(80))) SetAllExpanded(true);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        int deleteIndex   = -1;
        int moveUpIndex   = -1;
        int moveDownIndex = -1;

        for (int i = 0; i < _comboSteps.arraySize; i++)
        {
            var stepProp = _comboSteps.GetArrayElementAtIndex(i);
            
            var displayNameProp    = stepProp.FindPropertyRelative("displayName");
            var showGizmosProp     = stepProp.FindPropertyRelative("showGizmos");
            var showWindowGizmosProp = stepProp.FindPropertyRelative("showWindowGizmos");
            var actionSequenceProp = stepProp.FindPropertyRelative("actionSequence");
            
            var wsStartProp = stepProp.FindPropertyRelative("inputWindowStart");

            string stepName = displayNameProp.stringValue;
            float wsStart   = wsStartProp.floatValue;

            string headerText = $"[{i + 1}타] {stepName} (연타 방지 대기: {wsStart:F2}s)"
                + (showGizmosProp.boolValue ? " [기즈모]" : "")
                + (showWindowGizmosProp.boolValue ? " [타이밍]" : "");

            EditorGUILayout.BeginHorizontal();

            foldouts[i] = EditorGUILayout.Foldout(foldouts[i], headerText, true, EditorStyles.foldoutHeader);

            // 👁 공격 기즈모 토글 (PhaseSO 양식 그대로)
            Color prevBg = GUI.backgroundColor;
            GUI.backgroundColor = showGizmosProp.boolValue ? ActionEditorStyles.AccentGreen : Color.white;
            bool newGizmo = GUILayout.Toggle(showGizmosProp.boolValue,
                new GUIContent("👁", "공격 범위 기즈모 표시"),
                EditorStyles.miniButton, GUILayout.Width(26));
            if (newGizmo != showGizmosProp.boolValue) showGizmosProp.boolValue = newGizmo;
            
            // ⏱ 타이밍 기즈모 토글
            GUI.backgroundColor = showWindowGizmosProp.boolValue ? ActionEditorStyles.AccentBlue : Color.white;
            bool newWinGizmo = GUILayout.Toggle(showWindowGizmosProp.boolValue,
                new GUIContent("⏱", "입력 윈도우 구간에 따른 색상 변화 표시"),
                EditorStyles.miniButton, GUILayout.Width(26));
            if (newWinGizmo != showWindowGizmosProp.boolValue) showWindowGizmosProp.boolValue = newWinGizmo;

            GUI.backgroundColor = prevBg;

            // 순서 이동 / 삭제 버튼
            GUI.enabled = i > 0;
            if (GUILayout.Button("▲", GUILayout.Width(25))) moveUpIndex = i;
            GUI.enabled = i < _comboSteps.arraySize - 1;
            if (GUILayout.Button("▼", GUILayout.Width(25))) moveDownIndex = i;
            GUI.enabled = true;
            if (GUILayout.Button("✕", GUILayout.Width(25))) deleteIndex = i;

            EditorGUILayout.EndHorizontal();

            // ── 상세 내용 ──
            if (!foldouts[i]) continue;

            EditorGUI.indentLevel++;

            // 기본 설정
            ActionEditorStyles.DrawHeaderLayout("  ▸ 기본 설정");
            EditorGUILayout.PropertyField(displayNameProp,    new GUIContent("단계 이름"));
            EditorGUILayout.PropertyField(actionSequenceProp, new GUIContent("액션 시퀀스"));
            EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("isLauncher"),    new GUIContent("런처 (공중 띄우기)"));
            EditorGUILayout.PropertyField(stepProp.FindPropertyRelative("isAirFinisher"), new GUIContent("에어 피니셔 (슬램 연출)"));

            GUILayout.Space(4);

            // 입력 윈도우 설정
            ActionEditorStyles.DrawHeaderLayout("  ▸ 다음 입력 윈도우 설정");
            EditorGUILayout.PropertyField(wsStartProp, new GUIContent("연타 방지 최소 대기(초)"));
            
            GUILayout.Space(4);
            Rect timelineRect = GUILayoutUtility.GetRect(0, 16f, GUILayout.ExpandWidth(true));
            timelineRect.x += 15f * EditorGUI.indentLevel;
            timelineRect.width -= 15f * EditorGUI.indentLevel;
            DrawWindowTimeline(timelineRect, wsStartProp.floatValue);
            GUILayout.Space(6);

            // 이동/물리 설정
            ActionEditorStyles.DrawHeaderLayout("  ▸ 이동 · 물리 제어 (플레이어 전용)");
            var physicsProp = stepProp.FindPropertyRelative("physics");
            if (physicsProp != null)
            {
                EditorGUILayout.PropertyField(physicsProp.FindPropertyRelative("allowMoveWhileAttacking"), new GUIContent("공격 중 이동 입력 허용"));
                if (physicsProp.FindPropertyRelative("allowMoveWhileAttacking").boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(physicsProp.FindPropertyRelative("forwardMoveOnly"), new GUIContent("전진 방향만 허용"));
                    EditorGUILayout.PropertyField(physicsProp.FindPropertyRelative("attackMoveSpeed"), new GUIContent("이동 속도"));
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.PropertyField(physicsProp.FindPropertyRelative("enableDynamicMovement"), new GUIContent("자동 이동 활성화"));
                if (physicsProp.FindPropertyRelative("enableDynamicMovement").boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(physicsProp.FindPropertyRelative("autoMoveSpeed"), new GUIContent("자동 이동 속도 (양=전진)"));
                    
                    EditorGUILayout.PropertyField(physicsProp.FindPropertyRelative("brakeOnOppositeInput"), new GUIContent("반대 입력 시 감속"));
                    if (physicsProp.FindPropertyRelative("brakeOnOppositeInput").boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(physicsProp.FindPropertyRelative("oppositeBrakeSpeed"), new GUIContent("감속 속도"));
                        EditorGUI.indentLevel--;
                    }
                    
                    EditorGUILayout.PropertyField(physicsProp.FindPropertyRelative("accelerateOnForwardInput"), new GUIContent("순방향 입력 시 가속"));
                    if (physicsProp.FindPropertyRelative("accelerateOnForwardInput").boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(physicsProp.FindPropertyRelative("forwardAccelerationSpeed"), new GUIContent("추가 가속 속도"));
                        EditorGUI.indentLevel--;
                    }

                    EditorGUILayout.PropertyField(physicsProp.FindPropertyRelative("useStartEase"), new GUIContent("출발 가속 이징 사용"));
                    if (physicsProp.FindPropertyRelative("useStartEase").boolValue)
                    {
                        EditorGUI.indentLevel++;
                        EditorGUILayout.PropertyField(physicsProp.FindPropertyRelative("startMoveSpeed"),    new GUIContent("출발 초기 속도"));
                        EditorGUILayout.PropertyField(physicsProp.FindPropertyRelative("startEaseType"),     new GUIContent("이징 타입"));
                        EditorGUILayout.PropertyField(physicsProp.FindPropertyRelative("startEaseExponent"), new GUIContent("곡선 기울기 강도"));
                        EditorGUILayout.PropertyField(physicsProp.FindPropertyRelative("startEaseDuration"), new GUIContent("가속 도달 시간(초)"));

                        EaseType et = (EaseType)physicsProp.FindPropertyRelative("startEaseType").enumValueIndex;
                        float exp = physicsProp.FindPropertyRelative("startEaseExponent").floatValue;

                        Rect curveRect = GUILayoutUtility.GetRect(0, 44f, GUILayout.ExpandWidth(true));
                        curveRect.x += 15f * EditorGUI.indentLevel;
                        curveRect.width -= 15f * EditorGUI.indentLevel;
                        ActionEditorStyles.DrawCurvePreview(ref curveRect, et, exp, "가속 곡선 미리보기");
                        EditorGUI.indentLevel--;
                    }
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.PropertyField(physicsProp.FindPropertyRelative("allowSlideAfterAction"), new GUIContent("행동 후 미끄러짐 허용"));
            }

            EditorGUI.indentLevel--;
            GUILayout.Space(3);
            ActionEditorStyles.DrawDividerLayout();
        }

        // ── 콤보 단계 추가 버튼 ──
        GUILayout.Space(4);
        if (GUILayout.Button("+ 콤보 단계 추가", GUILayout.Height(28)))
        {
            _comboSteps.InsertArrayElementAtIndex(_comboSteps.arraySize);
            foldouts.Add(true);
            
            var newStep = _comboSteps.GetArrayElementAtIndex(_comboSteps.arraySize - 1);
            newStep.FindPropertyRelative("displayName").stringValue     = $"공격 {_comboSteps.arraySize}타";
            newStep.FindPropertyRelative("inputWindowStart").floatValue = 0.2f;
        }

        // ── 삭제/이동 처리 ──
        if (deleteIndex >= 0)
        {
            _comboSteps.DeleteArrayElementAtIndex(deleteIndex);
            if (deleteIndex < foldouts.Count) foldouts.RemoveAt(deleteIndex);
        }
        if (moveUpIndex > 0)
        {
            _comboSteps.MoveArrayElement(moveUpIndex, moveUpIndex - 1);
            (foldouts[moveUpIndex], foldouts[moveUpIndex - 1]) = (foldouts[moveUpIndex - 1], foldouts[moveUpIndex]);
        }
        if (moveDownIndex >= 0 && moveDownIndex < _comboSteps.arraySize - 1)
        {
            _comboSteps.MoveArrayElement(moveDownIndex, moveDownIndex + 1);
            (foldouts[moveDownIndex], foldouts[moveDownIndex + 1]) = (foldouts[moveDownIndex + 1], foldouts[moveDownIndex]);
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void SetAllExpanded(bool value)
    {
        for (int i = 0; i < foldouts.Count; i++)
            foldouts[i] = value;
    }

    private void DrawWindowTimeline(Rect rect, float windowStart)
    {
        float maxTime = Mathf.Max(windowStart * 1.5f, 1.5f);

        // 기본 배경 (어두운 회색)
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.14f, 0.18f, 1f));

        // 입력 차단 구간 (연타 방지) - 붉은색
        float startX = rect.x + (windowStart / maxTime) * rect.width;
        EditorGUI.DrawRect(new Rect(rect.x, rect.y, startX - rect.x, rect.height),
            new Color(0.9f, 0.3f, 0.3f, 0.25f));

        // 입력 수용 가능 구간 - 녹색
        EditorGUI.DrawRect(new Rect(startX, rect.y, rect.xMax - startX, rect.height),
            new Color(0.3f, 0.9f, 0.4f, 0.25f));

        EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width, 1), ActionEditorStyles.DividerColor);
        EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 1, rect.width, 1), ActionEditorStyles.DividerColor);

        GUIStyle s = new GUIStyle(EditorStyles.miniLabel)
        { normal = { textColor = new Color(0.5f, 0.9f, 0.5f, 1f) }, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(rect.x, rect.y - 14f, rect.width, 14f),
            $"연타 방지 대기 ({windowStart:F2}s) | 이후 입력 수용 (애니메이션 완료 전까지)", s);
    }
}
#endif
