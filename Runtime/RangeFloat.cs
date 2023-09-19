#nullable enable
using System;
using UnityEngine;

//  GiLight2D © NullTale - https://twitter.com/NullTale/
namespace GiLight2D
{
    [Serializable]
    public class RangeFloat
    {
        public Vector2 Range;
        public float   Value;
        
        public RangeFloat(Vector2 range, float value)
        {
            Range = range;
            Value = value;
        }
    }
}