using UnityEngine;
using UnityEngine.UI;

namespace TheRacer
{
    /// <summary>
    /// Shears a UI graphic horizontally so panels become parallelograms and text becomes italic.
    /// The shear is measured against a shared reference rect so every element in a cluster leans identically.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkewEffect : BaseMeshEffect
    {
        [SerializeField, Tooltip("Horizontal shift per unit of height. Positive leans the top to the right.")]
        private float _skew = 0.14f;

        private RectTransform _reference;
        private RectTransform _self;

        /// <summary>Sets the shear amount and the cluster rect the shear is measured against.</summary>
        public void Configure(float skew, RectTransform reference)
        {
            _skew = skew;
            _reference = reference;
            _self = transform as RectTransform;
            if (graphic != null) graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            UIVertex vertex = default;
            float centerY = _reference != null ? _reference.rect.center.y : 0f;
            int count = vh.currentVertCount;
            for (int i = 0; i < count; i++)
            {
                vh.PopulateUIVertex(ref vertex, i);
                float y = vertex.position.y;
                if (_reference != null && _self != null)
                {
                    Vector3 world = _self.TransformPoint(vertex.position);
                    y = _reference.InverseTransformPoint(world).y - centerY;
                }
                vertex.position.x += y * _skew;
                vh.SetUIVertex(vertex, i);
            }
        }
    }
}
