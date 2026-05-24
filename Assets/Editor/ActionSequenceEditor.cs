using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ActionSequenceSO))]
public class ActionSequenceEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space();
        
        // Actions 리스트 표시 (기존에 작성한 PropertyDrawer가 적용됨)
        SerializedProperty actionsProperty = serializedObject.FindProperty("actions");
        EditorGUILayout.PropertyField(actionsProperty, new GUIContent("수행할 액션들"), true);

        serializedObject.ApplyModifiedProperties();
    }
}