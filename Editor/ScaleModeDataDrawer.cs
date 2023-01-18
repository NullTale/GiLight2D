using System;
using UnityEditor;
using UnityEngine;

namespace GiLight2D.Editor
{
    [CustomPropertyDrawer(typeof(GiLight2DFeature.ScaleModeData))]
    public class ScaleModeDataDrawer : PropertyDrawer
    {
        // =======================================================================
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var scaleMode = property.FindPropertyRelative(nameof(GiLight2DFeature.ScaleModeData._scaleMode));
            return scaleMode.intValue == (int)GiLight2DFeature.ScaleMode.None ? EditorGUIUtility.singleLineHeight : EditorGUIUtility.singleLineHeight * 2;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var scaleMode = property.FindPropertyRelative(nameof(GiLight2DFeature.ScaleModeData._scaleMode));
            var ratio     = property.FindPropertyRelative(nameof(GiLight2DFeature.ScaleModeData._ratio));
            var height    = property.FindPropertyRelative(nameof(GiLight2DFeature.ScaleModeData._height));
            
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
                    ratio.floatValue = EditorGUI.Slider(
                        new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight),
                        new GUIContent("Ratio"),
                        ratio.floatValue, 0.1f, 2f);
                } break;
                case GiLight2DFeature.ScaleMode.Fixed:
                {
                    height.intValue = EditorGUI.IntField(
                        new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight),
                        new GUIContent("Height"),
                        height.intValue);
                    height.intValue = Mathf.Max(height.intValue, 1);
                } break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}