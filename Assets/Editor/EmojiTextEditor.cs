using UnityEditor;
using UnityEditor.UI;

[CustomEditor(typeof(EmojiText), true)]
[CanEditMultipleObjects]
public class EmojiTextEditor : TextEditor
{
    SerializedProperty m_EmojiAsset;
    SerializedProperty m_EmojiSpeed;

    protected override void OnEnable()
    {
        base.OnEnable();
        m_EmojiAsset = serializedObject.FindProperty("m_EmojiAsset");
        m_EmojiSpeed = serializedObject.FindProperty("m_EmojiSpeed");
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        serializedObject.Update();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Emoji Text", EditorStyles.boldLabel);
        EditorGUILayout.BeginVertical(" ");
        EditorGUILayout.PropertyField(m_EmojiAsset);
        EditorGUILayout.PropertyField(m_EmojiSpeed);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space();

        //AppearanceControlsGUI();
        //RaycastControlsGUI();
        serializedObject.ApplyModifiedProperties();
    }
}