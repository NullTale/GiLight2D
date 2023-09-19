using System;
using UnityEditor;
using UnityEngine;

//  GiLight2D © NullTale - https://twitter.com/NullTale/
namespace GiLight2D.Editor
{
    [CustomPropertyDrawer(typeof(GiLight2D.NoiseOptions))]
    public class NoiseOptionsDrawer : PropertyDrawer
    {
        // =======================================================================
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var noiseMode = property.FindPropertyRelative(nameof(GiLight2D.NoiseOptions._noise));
            var linesCount = ((GiLight2D.NoiseSource)noiseMode.intValue) switch
            {
                GiLight2D.NoiseSource.Texture => property.FindPropertyRelative(nameof(GiLight2D.NoiseOptions._noise)).isExpanded ? 6 : 1,
                GiLight2D.NoiseSource.Shader => 1,
                GiLight2D.NoiseSource.None   => 1,
                _                                   => throw new ArgumentOutOfRangeException()
            };
            
            return EditorGUIUtility.singleLineHeight * linesCount;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var source        = property.FindPropertyRelative(nameof(GiLight2D.NoiseOptions._noise));
            var scale         = property.FindPropertyRelative(nameof(GiLight2D.NoiseOptions._scale));
            var velocity      = property.FindPropertyRelative(nameof(GiLight2D.NoiseOptions._velocity));
            var bilinear      = property.FindPropertyRelative(nameof(GiLight2D.NoiseOptions._bilinear));
            var orthoRelative = property.FindPropertyRelative(nameof(GiLight2D.NoiseOptions._orthoRelative));
            var pattern       = property.FindPropertyRelative(nameof(GiLight2D.NoiseOptions._pattern));
            var texture       = property.FindPropertyRelative(nameof(GiLight2D.NoiseOptions._texture));
            
            var index = 0;
            var mode  = (GiLight2D.NoiseSource)source.intValue;

            EditorGUI.PropertyField(_fieldRect(index ++), source);

            if (mode == GiLight2D.NoiseSource.Texture)
            {
                source.isExpanded = EditorGUI.Foldout(_fieldRect(index - 1), source.isExpanded, GUIContent.none);
                if (source.isExpanded == false)
                    return;
                
                EditorGUI.indentLevel ++;
                EditorGUI.PropertyField(_fieldRect(index ++), scale);
                EditorGUI.PropertyField(_fieldRect(index ++), velocity);
                EditorGUI.PropertyField(_fieldRect(index ++), pattern);
                if (((GiLight2D.NoiseTexture)pattern.intValue) == GiLight2D.NoiseTexture.Texture)
                {
                    EditorGUI.PropertyField(_fieldRect(index++), texture);
                }
                else
                {
                    EditorGUI.PropertyField(_fieldRect(index ++), bilinear);
                }
                
                EditorGUI.PropertyField(_fieldRect(index ++), orthoRelative);
                EditorGUI.indentLevel --;
            }
            else
            {
                GUI.enabled = false;
                EditorGUI.Foldout(_fieldRect(index - 1), false, GUIContent.none);
                GUI.enabled = true;
            }

            // -----------------------------------------------------------------------
            Rect _fieldRect(int line)
            {
                return new Rect(position.x, position.y + line * EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight);
            }
        }
    }
}