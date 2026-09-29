using System.Collections.Generic;
using UnityEngine;
using TMPro;
using DungeonTower.Combat;
using DungeonTower.Core;

namespace DungeonTower.UI
{
    /// <summary>
    /// The row of status icons under a unit's health bar. HealthBarView
    /// calls Refresh(unit) every LateUpdate; this only touches the UI when
    /// something visible actually changed (a status was added/removed, or
    /// a displayed number ticked), so idle frames cost almost nothing.
    ///
    /// Lives as a child of the HealthBarView root (NOT inside the Slider,
    /// which is hidden at full HP — statuses must show at full HP too).
    /// Needs a HorizontalLayoutGroup on the same object.
    /// </summary>
    public sealed class StatusRowView : MonoBehaviour
    {
        [SerializeField] private StatusIconView _iconPrefab;
        [SerializeField] private StatusVisualsSO _visuals;
        [SerializeField, Min(1)] private int _maxIcons = 6;
        [Tooltip("Optional. Shows '+N' when a unit has more statuses than _maxIcons.")]
        [SerializeField] private TextMeshProUGUI _overflowLabel;

        private readonly List<StatusIconView> _icons = new List<StatusIconView>();
        private bool _hasDrawn;
        private int _lastSignature;

        private void Awake()
        {
            if (_visuals == null)
                Debug.LogWarning("StatusRowView has no StatusVisualsSO assigned — no status icons will show.", this);
            if (_overflowLabel != null) _overflowLabel.raycastTarget = false;
        }

        /// <summary>Pass null (dead / unbound unit) to hide the row.</summary>
        public void Refresh(CombatUnit unit)
        {
            if (unit == null || _visuals == null || _iconPrefab == null)
            {
                Hide();
                return;
            }

            var active = unit.Status.Active;
            int shown = Mathf.Min(active.Count, _maxIcons);
            int signature = ComputeSignature(active, shown);

            if (_hasDrawn && signature == _lastSignature) return;

            Rebuild(active, shown);
            _lastSignature = signature;
            _hasDrawn = true;
        }

        private void Hide()
        {
            if (!_hasDrawn) return;
            foreach (var icon in _icons)
                if (icon != null) icon.gameObject.SetActive(false);
            if (_overflowLabel != null) _overflowLabel.gameObject.SetActive(false);
            _hasDrawn = false;
        }

        private void Rebuild(IReadOnlyList<StatusEffectInstance> active, int shown)
        {
            // Pool: grow as needed, never destroy — just toggle active.
            while (_icons.Count < shown)
                _icons.Add(Instantiate(_iconPrefab, transform));

            for (int i = 0; i < _icons.Count; i++)
            {
                var icon = _icons[i];
                if (i >= shown)
                {
                    icon.gameObject.SetActive(false);
                    continue;
                }

                var instance = active[i];
                var rule = StatusRules.Get(instance.Id);
                var visual = _visuals.Get(instance.Id);
                icon.Bind(visual, _visuals.BorderColor(rule.Polarity), ResolveCount(instance, rule, visual.CountMode));
                icon.gameObject.SetActive(true);
            }

            if (_overflowLabel != null)
            {
                int extra = active.Count - shown;
                _overflowLabel.gameObject.SetActive(extra > 0);
                if (extra > 0)
                {
                    _overflowLabel.text = "+" + extra;
                    _overflowLabel.transform.SetAsLastSibling();
                }
            }
        }

        // Cheap fingerprint of "what's on screen": ids + displayed numbers.
        private int ComputeSignature(IReadOnlyList<StatusEffectInstance> active, int shown)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + active.Count;
                for (int i = 0; i < shown; i++)
                {
                    var instance = active[i];
                    var rule = StatusRules.Get(instance.Id);
                    var visual = _visuals.Get(instance.Id);
                    h = h * 31 + (int)instance.Id;
                    h = h * 31 + ResolveCount(instance, rule, visual.CountMode);
                }
                return h;
            }
        }

        // 0 means "don't show a number".
        private static int ResolveCount(StatusEffectInstance instance, StatusRule rule, StatusCountMode mode)
        {
            if (mode == StatusCountMode.Auto)
            {
                if (rule.StackDecayPerTurn > 0) mode = StatusCountMode.Stacks;
                else if (rule.MagnitudeDecayPerTurn > 0f) mode = StatusCountMode.Magnitude;
                else if (rule.UsesDuration) mode = StatusCountMode.Duration;
                else mode = StatusCountMode.None;
            }

            switch (mode)
            {
                case StatusCountMode.Stacks:    return Mathf.Max(0, instance.Stacks);
                case StatusCountMode.Duration:  return Mathf.Max(0, instance.Duration);
                case StatusCountMode.Magnitude: return Mathf.Max(0, Mathf.RoundToInt(instance.Magnitude));
                default:                        return 0;
            }
        }
    }
}
