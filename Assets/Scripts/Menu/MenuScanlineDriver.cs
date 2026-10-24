using UnityEngine;
using UnityEngine.UI;

namespace TheRacer
{
    /// <summary>Scrolls the code-generated scanline overlay with unscaled time.</summary>
    public sealed class MenuScanlineDriver : MonoBehaviour
    {
        private RawImage _image;
        public void SetTexture(RawImage image) { _image = image; }
        private void Update()
        {
            if (_image == null) return;
            Rect rect = _image.uvRect;
            rect.y -= Time.unscaledDeltaTime * .018f;
            _image.uvRect = rect;
        }
    }
}
