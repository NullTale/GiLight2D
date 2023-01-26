using System;
using UnityEditor;
using UnityEngine;

namespace GiLight2D.Editor
{
    [CustomPropertyDrawer(typeof(GiLight2DFeature.NoiseOptions))]
    public class NoiseOptionsDrawer : PropertyDrawer
    {
        // =======================================================================
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var noiseMode = property.FindPropertyRelative(nameof(GiLight2DFeature.NoiseOptions._noiseMode));
            var linesCount = ((GiLight2DFeature.NoiseMode)noiseMode.intValue) switch
            {
                GiLight2DFeature.NoiseMode.Dynamic => 3,
                GiLight2DFeature.NoiseMode.Static  => 2,
                GiLight2DFeature.NoiseMode.Shader  => 1,
                GiLight2DFeature.NoiseMode.None    => 1,
                _                                  => throw new ArgumentOutOfRangeException()
            };
            
            return EditorGUIUtility.singleLineHeight * linesCount;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var noiseMode   = property.FindPropertyRelative(nameof(GiLight2DFeature.NoiseOptions._noiseMode));
            var noiseScale  = property.FindPropertyRelative(nameof(GiLight2DFeature.NoiseOptions._noiseScale));
            var noisePeriod = property.FindPropertyRelative(nameof(GiLight2DFeature.NoiseOptions._noisePeriod));
            
            var index = 0;

            EditorGUI.PropertyField(_fieldRect(index ++), noiseMode);

            switch ((GiLight2DFeature.NoiseMode)noiseMode.intValue)
            {
                case GiLight2DFeature.NoiseMode.Dynamic:
                {
                    EditorGUI.PropertyField(_fieldRect(index ++), noiseScale);
                    EditorGUI.PropertyField(_fieldRect(index ++), noisePeriod);
                } break;
                case GiLight2DFeature.NoiseMode.Static:
                {
                    EditorGUI.PropertyField(_fieldRect(index ++), noiseScale);
                } break;
                case GiLight2DFeature.NoiseMode.Shader:
                case GiLight2DFeature.NoiseMode.None:
                {
                    // pass
                } break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            // -----------------------------------------------------------------------
            Rect _fieldRect(int line)
            {
                return new Rect(position.x, position.y + line * EditorGUIUtility.singleLineHeight, position.width, EditorGUIUtility.singleLineHeight);
            }
        }
    }
}