using System;
using UnityEditor;
using UnityEngine;

//  GiLight2D © NullTale - https://twitter.com/NullTale/
namespace GiLight2D.Editor
{
    [CustomPropertyDrawer(typeof(GiLight2D.OutputOptions))]
    public class OutputDrawer : PropertyDrawer
    {
        // =======================================================================
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            if (property.isExpanded == false)
                return EditorGUIUtility.singleLineHeight;
            
            var finalBlit = property.FindPropertyRelative(nameof(GiLight2D.OutputOptions._output));
            var lines = ((GiLight2D.FinalBlit)finalBlit.intValue) switch
            {
                GiLight2D.FinalBlit.Texture => 3,
                GiLight2D.FinalBlit.Camera  => 2,
                _                                  => throw new ArgumentOutOfRangeException()
            };
            
            return lines * EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var finalBlit = property.FindPropertyRelative(nameof(GiLight2D.OutputOptions._output));
            var giTexture = property.FindPropertyRelative(nameof(GiLight2D.OutputOptions._globalTexture));
            var alpha     = property.FindPropertyRelative(nameof(GiLight2D.OutputOptions._alpha));
            
            var index = 0;
            EditorGUI.PropertyField(_lineRect(index ++), finalBlit);
            property.isExpanded = EditorGUI.Foldout(_lineRect(index - 1), property.isExpanded, GUIContent.none, true);
            if (property.isExpanded == false)
                return;
            
            EditorGUI.indentLevel ++;
            switch ((GiLight2D.FinalBlit)finalBlit.intValue)
            {
                case GiLight2D.FinalBlit.Texture:
                {
                    EditorGUI.PropertyField(_lineRect(index ++), giTexture);
                } break;
                case GiLight2D.FinalBlit.Camera:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            EditorGUI.PropertyField(_lineRect(index ++), alpha);
            EditorGUI.indentLevel --;


            // -----------------------------------------------------------------------
            Rect _lineRect(int n)
            {
                return new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight * n, position.width, EditorGUIUtility.singleLineHeight);
            }
        }
    }
}