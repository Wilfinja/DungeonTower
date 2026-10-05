using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DungeonTower.UI
{
    /// <summary>
    /// Put on any UI element with a raycast target to show a tooltip while
    /// the pointer is over it. Text comes from either the inspector field
    /// or, in code, a provider function that's called fresh on every
    /// hover (so the text reflects the game right now). Tooltips need
    /// mouse hover — there's no touch fallback yet.
    /// </summary>
    public sealed class TooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [TextArea, SerializeField] private string _staticText;

        private Func<string> _provider;
        private bool _hovering;

        // Adds a trigger to `target` (or reuses the one already there).
        public static TooltipTrigger Attach(GameObject target, Func<string> provider)
        {
            var trigger = target.GetComponent<TooltipTrigger>();
            if (trigger == null) trigger = target.AddComponent<TooltipTrigger>();
            trigger._provider = provider;
            return trigger;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hovering = true;
            string text = _provider != null ? _provider() : _staticText;
            if (TooltipView.Instance != null) TooltipView.Instance.Show(text);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovering = false;
            if (TooltipView.Instance != null) TooltipView.Instance.Hide();
        }

        // A panel closing under the cursor never sends PointerExit.
        private void OnDisable()
        {
            if (!_hovering) return;
            _hovering = false;
            if (TooltipView.Instance != null) TooltipView.Instance.Hide();
        }
    }
}
