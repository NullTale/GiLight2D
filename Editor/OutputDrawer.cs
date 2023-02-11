using System;
using UnityEditor;
using UnityEngine;

namespace GiLight2D.Editor
{
    [CustomPropertyDrawer(typeof(GiLight2DFeature.OutputOptions))]
    public class OutputDrawer : PropertyDrawer
    {
        // =======================================================================
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var finalBlit = property.FindPropertyRelative(nameof(GiLight2DFeature.OutputOptions._finalBlit));
            return finalBlit.intValue == (int)GiLight2DFeature.FinalBlit.Camera ? EditorGUIUtility.singleLineHeight : EditorGUIUtility.singleLineHeight * 2;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var finalBlit = property.FindPropertyRelative(nameof(GiLight2DFeature.OutputOptions._finalBlit));
            var giTexture = property.FindPropertyRelative(nameof(GiLight2DFeature.OutputOptions._outputGlobalTexture));
            
            EditorGUI.PropertyField(new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight), finalBlit);

            switch ((GiLight2DFeature.FinalBlit)finalBlit.intValue)
            {
                case GiLight2DFeature.FinalBlit.Texture:
                {
                    EditorGUI.PropertyField(
                        new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight),
                        giTexture);
                } break;
                case GiLight2DFeature.FinalBlit.Camera:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}