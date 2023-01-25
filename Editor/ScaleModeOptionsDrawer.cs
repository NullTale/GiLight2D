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
            var scaleMode = property.FindPropertyRelative(nameof(GiLight2DFeature.ScaleModeOptions._scaleMode));
            return scaleMode.intValue == (int)GiLight2DFeature.ScaleMode.None ? EditorGUIUtility.singleLineHeight : EditorGUIUtility.singleLineHeight * 2;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var scaleMode = property.FindPropertyRelative(nameof(GiLight2DFeature.ScaleModeOptions._scaleMode));
            var ratio     = property.FindPropertyRelative(nameof(GiLight2DFeature.ScaleModeOptions._ratio));
            var height    = property.FindPropertyRelative(nameof(GiLight2DFeature.ScaleModeOptions._height));
            
            scaleMode.intValue = (int)(GiLight2DFeature.ScaleMode)EditorGUI.EnumPopup(
                new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), 
                label,
                (GiLight2DFeature.ScaleMode)scaleMode.intValue);

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
                    EditorGUI.PropertyField(new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight), height);
                    height.intValue = Mathf.Max(height.intValue, 1);
                } break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}