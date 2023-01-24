using UnityEditor;
using UnityEngine;

namespace GiLight2D.Editor
{
    [CustomPropertyDrawer(typeof(GiLight2DFeature.TraceOptions))]
    public class TraceOptionsDrawer : PropertyDrawer
    {
        // =======================================================================
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var bounces   = property.FindPropertyRelative(nameof(GiLight2DFeature.TraceOptions._bounces));
            var intencity = property.FindPropertyRelative(nameof(GiLight2DFeature.TraceOptions._intencity));
            
            var extraLines = intencity.isExpanded ? bounces.intValue : 0;   
            return (property.isExpanded == false ? 1 : (6 + extraLines)) * EditorGUIUtility.singleLineHeight; 
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var index = 0;
            property.isExpanded = EditorGUI.Foldout(_fieldRect(index ++), property.isExpanded, label, true);
            if (property.isExpanded == false)
                return;

            EditorGUI.indentLevel ++;
            
            var enable    = property.FindPropertyRelative(nameof(GiLight2DFeature.TraceOptions._enable));
            var scale     = property.FindPropertyRelative(nameof(GiLight2DFeature.TraceOptions._scale));
            var piercing  = property.FindPropertyRelative(nameof(GiLight2DFeature.TraceOptions._piercing));
            var bounces   = property.FindPropertyRelative(nameof(GiLight2DFeature.TraceOptions._bounces));
            var intencity = property.FindPropertyRelative(nameof(GiLight2DFeature.TraceOptions._intencity));
            var scales    = property.FindPropertyRelative(nameof(GiLight2DFeature.TraceOptions._scales));
            
            EditorGUI.PropertyField(_fieldRect(index ++), enable);
            EditorGUI.PropertyField(_fieldRect(index ++), scale);
            EditorGUI.PropertyField(_fieldRect(index ++), piercing);
            EditorGUI.PropertyField(_fieldRect(index ++), bounces);
            
            intencity.isExpanded = EditorGUI.Foldout(_fieldRect(index), intencity.isExpanded, GUIContent.none, false);
            if (intencity.isExpanded)
            {
                GUI.color = Color.Lerp(Color.gray, Color.white, 1.0f);
                GUI.Box(EditorGUI.IndentedRect(_fieldRect(index)), GUIContent.none);
                GUI.color = Color.white;
                
                EditorGUI.PropertyField(_fieldRect(index ++), intencity);
                
                GUI.color = Color.Lerp(Color.gray, Color.white, 0.9f); 
                for (var n = 0; n < bounces.intValue; n++)
                {
                    GUI.Box(EditorGUI.IndentedRect(_fieldRect(index + n)), GUIContent.none);
                }
                GUI.color = Color.white;
                
                GUI.color = Color.Lerp(Color.gray, Color.white, 1f); 
                for (var n = 0; n < bounces.intValue; n++)
                {
                    EditorGUI.PropertyField(_fieldRect(index + n), scales.GetArrayElementAtIndex(n), new GUIContent("Bounce " + ((char)(65 + n)).ToString()));
                }
                GUI.color = Color.white;
            }
            else
            {
                EditorGUI.PropertyField(_fieldRect(index ++), intencity);
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