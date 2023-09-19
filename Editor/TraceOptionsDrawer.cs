using UnityEditor;
using UnityEngine;

//  GiLight2D © NullTale - https://twitter.com/NullTale/
namespace GiLight2D.Editor
{
    [CustomPropertyDrawer(typeof(GiLight2D.TraceOptions))]
    public class TraceOptionsDrawer : PropertyDrawer
    {
        // =======================================================================
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // var bounces   = property.FindPropertyRelative(nameof(GiLight2DFeature.TraceOptions._bounces));
            // var intencity = property.FindPropertyRelative(nameof(GiLight2DFeature.TraceOptions._intencity));
            
            return (property.isExpanded == false ? 1 : 9) * EditorGUIUtility.singleLineHeight; 
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var index = 0;
            property.isExpanded = EditorGUI.Foldout(_fieldRect(index ++), property.isExpanded, label, true);
            if (property.isExpanded == false)
                return;

            EditorGUI.indentLevel ++;
            
            var enable    = property.FindPropertyRelative(nameof(GiLight2D.TraceOptions._enable));
            var scale     = property.FindPropertyRelative(nameof(GiLight2D.TraceOptions._scale));
            var piercing  = property.FindPropertyRelative(nameof(GiLight2D.TraceOptions._piercing));
            var bounces   = property.FindPropertyRelative(nameof(GiLight2D.TraceOptions._bounces));
            var intencity = property.FindPropertyRelative(nameof(GiLight2D.TraceOptions._intencity));
            var scales    = property.FindPropertyRelative(nameof(GiLight2D.TraceOptions._scales));
            
            EditorGUI.PropertyField(_fieldRect(index ++), enable);
            EditorGUI.PropertyField(_fieldRect(index ++), scale);
            EditorGUI.PropertyField(_fieldRect(index ++), piercing);
            EditorGUI.PropertyField(_fieldRect(index ++), bounces);
            EditorGUI.PropertyField(_fieldRect(index ++), intencity);
            if (intencity.floatValue < 0f)
                intencity.floatValue = 0f;

            for (var n = 0; n < scales.arraySize; n++)
            {
                EditorGUI.PropertyField(_fieldRect(index ++), scales.GetArrayElementAtIndex(n), new GUIContent("Bounce " + ((char)(65 + n)).ToString()));
            }
            

            EditorGUI.indentLevel --;
            
            // -----------------------------------------------------------------------
            Rect _fieldRect(int line)
            {
                return new Rect(position.x, position.y + line * EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight);
            }
        }
    }
}