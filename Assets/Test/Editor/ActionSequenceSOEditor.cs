#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// ActionSequenceSO의 [SerializeReference] 액션 리스트를 편집하는 커스텀 에디터.
/// 타입 추가 드롭다운, 드래그 순서 변경, 일괄 접기/펼치기를 지원합니다.
/// </summary>
[CustomEditor(typeof(ActionSequenceSO))]
public class ActionSequenceSOEditor : Editor
{
    private ReorderableList _list;

    private void OnEnable()
    {
        var actions = serializedObject.FindProperty("actions");
        _list = new ReorderableList(serializedObject, actions, true, true, false, true)
        {
            drawHeaderCallback  = DrawHeader,
            drawElementCallback = DrawElement,
            elementHeightCallback = GetElementHeight,
            onRemoveCallback    = OnRemove,
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        // ── 헤더 버튼 영역 ──
        ActionEditorStyles.DrawHeaderLayout("▶ 액션 시퀀스 편집기");

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("모두 접기",  GUILayout.Width(80))) SetAllExpanded(false);
        if (GUILayout.Button("모두 펼치기", GUILayout.Width(80))) SetAllExpanded(true);
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(4);

        // ── 리스트 ──
        _list.DoLayoutList();

        // ── 액션 추가 버튼 (각 액션별 독립적인 단독 버튼) ──
        GUILayout.Space(6);
        ActionEditorStyles.DrawDividerLayout();

        EditorGUILayout.BeginVertical(ActionEditorStyles.SectionBox);
        EditorGUILayout.LabelField("▶ 새 액션 추가", EditorStyles.boldLabel);
        GUILayout.Space(4);

        if (GUILayout.Button("+ 이동 액션 (Move Action) 추가", GUILayout.Height(26))) 
            AddAction(new MoveActionData());
        GUILayout.Space(2);
        if (GUILayout.Button("+ 대기 액션 (Wait Action) 추가", GUILayout.Height(26))) 
            AddAction(new WaitActionData());
        GUILayout.Space(2);
        if (GUILayout.Button("+ 근접 타격 공격 (Melee Attack) 추가", GUILayout.Height(26))) 
            AddAction(new AttackActionData());
        GUILayout.Space(2);
        if (GUILayout.Button("+ 원거리 투사체 공격 (Ranged Attack) 추가", GUILayout.Height(26))) 
            AddAction(new RangedAttackActionData());

        EditorGUILayout.EndVertical();

        serializedObject.ApplyModifiedProperties();
    }

    // ── 리스트 콜백 ──

    private void DrawHeader(Rect rect)
    {
        EditorGUI.LabelField(rect, $"액션 목록  ({_list.count}개)", ActionEditorStyles.HeaderLabel);
    }

    private void DrawElement(Rect rect, int index, bool isActive, bool isFocused)
    {
        var element = _list.serializedProperty.GetArrayElementAtIndex(index);
        rect.y     += 2f;

        float btnWidth  = 55f;
        float btnHeight = EditorGUIUtility.singleLineHeight;
        
        // ── Rect 분할: PropertyField와 삭제 버튼 영역의 이벤트 중복 간섭 방지 ──
        // PropertyField가 전체 rect 너비를 차지하면 클릭 이벤트가 Foldout 토글로 소비되므로,
        // 삭제 버튼의 가로 너비(55f)와 여백(5f)을 제외한 영역에만 속성 필드를 그립니다.
        Rect fieldRect  = new Rect(rect.x, rect.y, rect.width - btnWidth - 5f, rect.height);
        Rect removeRect = new Rect(rect.xMax - btnWidth, rect.y, btnWidth, btnHeight);

        // 분리된 영역에 속성 필드 그리기
        EditorGUI.PropertyField(fieldRect, element, new GUIContent($"Action {index}"), true);

        // 빨간색 "삭제" 버튼 그리기
        Color prevColor = GUI.backgroundColor;
        GUI.backgroundColor = new Color(1f, 0.35f, 0.35f, 1f);
        if (GUI.Button(removeRect, "삭제", EditorStyles.miniButton))
        {
            // GUI 렌더링 도중 컬렉션 수정으로 인한 에러 방지를 위해 delayCall로 안전하게 삭제
            EditorApplication.delayCall += () =>
            {
                if (serializedObject != null && serializedObject.targetObject != null)
                {
                    serializedObject.Update();
                    var actions = serializedObject.FindProperty("actions");
                    if (index >= 0 && index < actions.arraySize)
                    {
                        actions.DeleteArrayElementAtIndex(index);
                        serializedObject.ApplyModifiedProperties();
                    }
                }
            };
        }
        GUI.backgroundColor = prevColor;
    }

    private float GetElementHeight(int index)
    {
        var element = _list.serializedProperty.GetArrayElementAtIndex(index);
        // 접혀있을 때는 기본 한 줄 높이만 제공
        if (!element.isExpanded) return EditorGUIUtility.singleLineHeight + 6f;
        
        // 펼쳐져 있을 때 유니티 기본 인스펙터 패딩 부족(1개 반 분량)을 해결하기 위해 26px 여백 추가
        return EditorGUI.GetPropertyHeight(element, true) + 26f;
    }

    private void OnRemove(ReorderableList list)
    {
        list.serializedProperty.DeleteArrayElementAtIndex(list.index);
    }

    // ── 헬퍼 ──

    private void AddAction(ActionData newAction)
    {
        serializedObject.Update();
        var actions = serializedObject.FindProperty("actions");
        int idx = actions.arraySize;
        actions.InsertArrayElementAtIndex(idx);
        actions.GetArrayElementAtIndex(idx).managedReferenceValue = newAction;
        serializedObject.ApplyModifiedProperties();
    }

    private void SetAllExpanded(bool expanded)
    {
        serializedObject.Update();
        var actions = serializedObject.FindProperty("actions");
        for (int i = 0; i < actions.arraySize; i++)
            actions.GetArrayElementAtIndex(i).isExpanded = expanded;
        serializedObject.ApplyModifiedProperties();
    }
}
#endif
