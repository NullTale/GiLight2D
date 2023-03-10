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
            var lines = ((GiLight2DFeature.FinalBlit)finalBlit.intValue) switch
            {
                GiLight2DFeature.FinalBlit.Texture => 4,
                GiLight2DFeature.FinalBlit.Camera  => 2,
                _                                  => throw new ArgumentOutOfRangeException()
            };
            
            return lines * EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var finalBlit = property.FindPropertyRelative(nameof(GiLight2DFeature.OutputOptions._finalBlit));
            var giTexture = property.FindPropertyRelative(nameof(GiLight2DFeature.OutputOptions._outputGlobalTexture));
            var fps       = property.FindPropertyRelative(nameof(GiLight2DFeature.OutputOptions._fps));
            var alpha     = property.FindPropertyRelative(nameof(GiLight2DFeature.OutputOptions._alpha));
            
            var index = 0;
            EditorGUI.PropertyField(_lineRect(index ++), finalBlit);
            EditorGUI.PropertyField(_lineRect(index ++), alpha);

            switch ((GiLight2DFeature.FinalBlit)finalBlit.intValue)
            {
                case GiLight2DFeature.FinalBlit.Texture:
                {
                    EditorGUI.PropertyField(_lineRect(index ++), fps);
                    EditorGUI.PropertyField(_lineRect(index ++), giTexture);
                } break;
                case GiLight2DFeature.FinalBlit.Camera:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            // -----------------------------------------------------------------------
            Rect _lineRect(int n)
            {
                return new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight * n, position.width, EditorGUIUtility.singleLineHeight);
            }
        }
    }
}