using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ActionSequenceSO))]
public class ActionSequenceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        ActionSequenceSO so = (ActionSequenceSO)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("시퀀스 전체 설정", EditorStyles.boldLabel);

        // 무한 반복 체크박스
        so.isInfiniteLoop = EditorGUILayout.Toggle("무한 반복", so.isInfiniteLoop);

        // 무한 반복이 아닐 때만 반복 횟수 필드 표시
        if (!so.isInfiniteLoop)
        {
            so.repeatCount = EditorGUILayout.IntField("반복 횟수", so.repeatCount);
        }

        EditorGUILayout.Space();
        
        // Actions 리스트 표시 (기존에 작성한 PropertyDrawer가 적용됨)
        SerializedProperty actionsProperty = serializedObject.FindProperty("actions");
        EditorGUILayout.PropertyField(actionsProperty, new GUIContent("수행할 액션들"), true);

        serializedObject.ApplyModifiedProperties();
    }
}