using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TheRacer
{
    /// <summary>Unscaled hover and press feedback for menu buttons.</summary>
    public sealed class CyberButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private RectTransform _rect;
        private Image _frame;
        private Vector3 _targetScale = Vector3.one;
        private bool _hover;
        private bool _startPulse;
        private UnityEngine.Events.UnityAction _onClick;

        public void Configure(Image frame, bool startPulse, UnityEngine.Events.UnityAction onClick)
        {
            _rect = transform as RectTransform;
            _frame = frame;
            _startPulse = startPulse;
            _onClick = onClick;
        }

        private void Update()
        {
            float pulse = _startPulse ? 1f + .02f * Mathf.Sin(Time.unscaledTime * 1.7f) : 1f;
            _targetScale = Vector3.one * (_hover ? 1.08f : pulse);
            if (_rect != null) _rect.localScale = Vector3.Lerp(_rect.localScale, _targetScale, 12f * Time.unscaledDeltaTime);
            if (_frame != null) _frame.color = Color.Lerp(MenuColors.Magenta, MenuColors.Cyan, _hover ? 1f : .12f);
        }

        public void OnPointerEnter(PointerEventData eventData) { _hover = true; }
        public void OnPointerExit(PointerEventData eventData) { _hover = false; }
        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) _onClick?.Invoke();
        }
    }
}
