using System;
using UnityEngine;
using UnityEngine.UI;

namespace Ursa.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    internal sealed class TapRingGraphic : MaskableGraphic
    {
        private static readonly int RingThicknessId = Shader.PropertyToID("_RingThickness");

        private Material _runtimeMaterial;
        private int _sourceMaterialId;

        internal void Configure(Material sourceMaterial, float ringThickness)
        {
            var sourceMaterialId = sourceMaterial == null ? 0 : sourceMaterial.GetInstanceID();
            if (_runtimeMaterial == null || _sourceMaterialId != sourceMaterialId)
            {
                ReleaseMaterial();
                if (sourceMaterial != null)
                {
                    _runtimeMaterial = new Material(sourceMaterial);
                }
                else
                {
                    var shader = Shader.Find("Ursa/UI/TapRing");
                    if (shader == null)
                        throw new InvalidOperationException("[Ursa] Tap ring shader was not found: Ursa/UI/TapRing");
                    _runtimeMaterial = new Material(shader);
                }

                _runtimeMaterial.name = "[Ursa] Tap Ring Material";
                _runtimeMaterial.hideFlags = HideFlags.HideAndDontSave;
                _sourceMaterialId = sourceMaterialId;
            }
            else if (sourceMaterial != null)
            {
                _runtimeMaterial.CopyPropertiesFromMaterial(sourceMaterial);
            }

            if (_runtimeMaterial.HasProperty(RingThicknessId))
                _runtimeMaterial.SetFloat(RingThicknessId, ringThickness);
            material = _runtimeMaterial;
            raycastTarget = false;
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            var rect = GetPixelAdjustedRect();
            var vertexColor = color;

            vertexHelper.AddVert(new Vector3(rect.xMin, rect.yMin), vertexColor, new Vector2(0f, 0f));
            vertexHelper.AddVert(new Vector3(rect.xMin, rect.yMax), vertexColor, new Vector2(0f, 1f));
            vertexHelper.AddVert(new Vector3(rect.xMax, rect.yMax), vertexColor, new Vector2(1f, 1f));
            vertexHelper.AddVert(new Vector3(rect.xMax, rect.yMin), vertexColor, new Vector2(1f, 0f));
            vertexHelper.AddTriangle(0, 1, 2);
            vertexHelper.AddTriangle(2, 3, 0);
        }

        protected override void OnDestroy()
        {
            ReleaseMaterial();
            base.OnDestroy();
        }

        private void ReleaseMaterial()
        {
            if (_runtimeMaterial == null)
                return;

            if (Application.isPlaying)
                Destroy(_runtimeMaterial);
            else
                DestroyImmediate(_runtimeMaterial);
            _runtimeMaterial = null;
            _sourceMaterialId = 0;
            material = null;
        }
    }
}
