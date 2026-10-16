using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DungeonTower.Items
{
    /// <summary>
    /// Greys a field out in the Inspector - for values the code calculates
    /// (anything typed into it is overwritten). Same trick as the debug
    /// inspector's read-only attribute, which lives in the UI assembly
    /// where Items can't reach it.
    /// </summary>
    public sealed class AutoFieldAttribute : PropertyAttribute { }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(AutoFieldAttribute))]
    public sealed class AutoFieldDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            bool prev = GUI.enabled;
            GUI.enabled = false;
            EditorGUI.PropertyField(position, property, label, true);
            GUI.enabled = prev;
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUI.GetPropertyHeight(property, label, true);
    }
#endif
}
