using System;
using UnityEditor;
using UnityEngine;

namespace GiLight2D.Editor
{
    [CustomPropertyDrawer(typeof(GiLight2DFeature.ScaleModeOptions))]
    public class ScaleModeOptionsDrawer : PropertyDrawer
    {
        // =======================================================================
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.isExpanded == false)
                return EditorGUIUtility.singleLineHeight;
            
            var scaleMode = (GiLight2DFeature.ScaleMode)property.FindPropertyRelative(nameof(GiLight2DFeature.ScaleModeOptions._scaleMode)).intValue;
            return scaleMode switch
            {
                GiLight2DFeature.ScaleMode.None  => EditorGUIUtility.singleLineHeight,
                GiLight2DFeature.ScaleMode.Scale => EditorGUIUtility.singleLineHeight * 2,
                GiLight2DFeature.ScaleMode.Fixed => EditorGUIUtility.singleLineHeight * 3,
                _                                => throw new ArgumentOutOfRangeException()
            };
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var scaleMode = property.FindPropertyRelative(nameof(GiLight2DFeature.ScaleModeOptions._scaleMode));
            var ratio     = property.FindPropertyRelative(nameof(GiLight2DFeature.ScaleModeOptions._ratio));
            var height    = property.FindPropertyRelative(nameof(GiLight2DFeature.ScaleModeOptions._height));
            
            scaleMode.intValue = (int)(GiLight2DFeature.ScaleMode)EditorGUI.EnumPopup(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), label, (GiLight2DFeature.ScaleMode)scaleMode.intValue);
            if (((GiLight2DFeature.ScaleMode)scaleMode.intValue) != GiLight2DFeature.ScaleMode.None)
            {
                property.isExpanded = EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), property.isExpanded, label, true);
                if (property.isExpanded == false)
                    return;
            }
            else
            {
                GUI.enabled = false;
                EditorGUI.Foldout(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), false, GUIContent.none, true);
                GUI.enabled = true;
                return;
            }
            
            
            EditorGUI.indentLevel ++;

            switch ((GiLight2DFeature.ScaleMode)scaleMode.intValue)
            {
                case GiLight2DFeature.ScaleMode.None:
                    break;
                case GiLight2DFeature.ScaleMode.Scale:
                {
                    EditorGUI.PropertyField(new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight), ratio);
                } break;
                case GiLight2DFeature.ScaleMode.Fixed:
                {
                    EditorGUI.PropertyField(new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight), ratio);
                    EditorGUI.PropertyField(new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight * 2, position.width, EditorGUIUtility.singleLineHeight), height);
                    height.intValue = Mathf.Max(height.intValue, 1);
                } break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            EditorGUI.indentLevel --;
        }
    }
}