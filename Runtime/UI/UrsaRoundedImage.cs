using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Sprites;
using UnityEngine.UI;

namespace Ursa.UI
{
    /// <summary>
    /// Draws a uGUI Image with rounded corners by generating rounded geometry.
    /// </summary>
    [AddComponentMenu("UI/Ursa/Rounded Image")]
    public sealed class UrsaRoundedImage : Image
    {
        [SerializeField, Min(0f)] private float _cornerDistance = 18f;
        [SerializeField, Range(1f, 8f)] private float _cornerPower = 2f;
        [SerializeField, Range(1, 16)] private int _cornerSegments = 8;

        public float CornerDistance
        {
            get => _cornerDistance;
            set
            {
                _cornerDistance = Mathf.Max(0f, value);
                SetVerticesDirty();
            }
        }

        public float CornerPower
        {
            get => _cornerPower;
            set
            {
                _cornerPower = Mathf.Clamp(value, 1f, 8f);
                SetVerticesDirty();
            }
        }

        public int CornerSegments
        {
            get => _cornerSegments;
            set
            {
                _cornerSegments = Mathf.Clamp(value, 1, 16);
                SetVerticesDirty();
            }
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            var rect = GetPixelAdjustedRect();
            var radius = Mathf.Min(_cornerDistance, Mathf.Min(rect.width, rect.height) * 0.5f);

            if (radius <= 0.001f)
            {
                base.OnPopulateMesh(vh);
                return;
            }

            vh.Clear();

            var uv = overrideSprite != null ? DataUtility.GetOuterUV(overrideSprite) : new Vector4(0f, 0f, 1f, 1f);
            var points = new List<Vector2>((_cornerSegments + 1) * 4);
            var power = Mathf.Max(_cornerPower, 1f);
            var exponent = 2f / power;

            AddCorner(points, new Vector2(rect.xMax - radius, rect.yMax - radius), radius, 0f, 90f, exponent, _cornerSegments);
            AddCorner(points, new Vector2(rect.xMin + radius, rect.yMax - radius), radius, 90f, 180f, exponent, _cornerSegments);
            AddCorner(points, new Vector2(rect.xMin + radius, rect.yMin + radius), radius, 180f, 270f, exponent, _cornerSegments);
            AddCorner(points, new Vector2(rect.xMax - radius, rect.yMin + radius), radius, 270f, 360f, exponent, _cornerSegments);

            var center = rect.center;
            vh.AddVert(CreateVertex(center, rect, uv));

            for (var i = 0; i < points.Count; i++)
                vh.AddVert(CreateVertex(points[i], rect, uv));

            for (var i = 0; i < points.Count; i++)
            {
                var next = i + 1 == points.Count ? 1 : i + 2;
                vh.AddTriangle(0, i + 1, next);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _cornerDistance = Mathf.Max(0f, _cornerDistance);
            _cornerPower = Mathf.Clamp(_cornerPower, 1f, 8f);
            _cornerSegments = Mathf.Clamp(_cornerSegments, 1, 16);
            SetVerticesDirty();
        }
#endif

        private static void AddCorner(List<Vector2> points, Vector2 center, float radius, float startAngle, float endAngle, float exponent, int steps)
        {
            steps = Mathf.Max(1, steps);
            for (var i = 0; i <= steps; i++)
            {
                var angle = Mathf.Lerp(startAngle, endAngle, i / (float)steps) * Mathf.Deg2Rad;
                var cos = Mathf.Cos(angle);
                var sin = Mathf.Sin(angle);
                var x = Mathf.Sign(cos) * Mathf.Pow(Mathf.Abs(cos), exponent) * radius;
                var y = Mathf.Sign(sin) * Mathf.Pow(Mathf.Abs(sin), exponent) * radius;
                points.Add(center + new Vector2(x, y));
            }
        }

        private UIVertex CreateVertex(Vector2 position, Rect rect, Vector4 uv)
        {
            var normalizedX = Mathf.InverseLerp(rect.xMin, rect.xMax, position.x);
            var normalizedY = Mathf.InverseLerp(rect.yMin, rect.yMax, position.y);

            var vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = position;
            vertex.uv0 = new Vector2(
                Mathf.Lerp(uv.x, uv.z, normalizedX),
                Mathf.Lerp(uv.y, uv.w, normalizedY));
            return vertex;
        }
    }
}
