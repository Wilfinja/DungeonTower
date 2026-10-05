using System.Collections;
using System.Collections.Generic;
using DungeonTower.Core;
using DungeonTower.Items;
using TMPro;
using UnityEngine;

namespace DungeonTower.UI
{
    /// <summary>
    /// One unit affected by the ability that just resolved. BattleController
    /// collects these while the ability applies, then hands the batch to
    /// HitFeedback so the "crunch" plays once per ability, not once per target.
    /// </summary>
    public readonly struct HitRecord
    {
        public UnitView View { get; }
        public GridPosition Tile { get; }
        public int Amount { get; }      // HP lost (damage) or HP restored (heal)
        public int Absorbed { get; }    // soaked up by Ward
        public bool IsCrit { get; }
        public bool IsKill { get; }
        public bool IsHeal { get; }

        private HitRecord(UnitView view, GridPosition tile, int amount, int absorbed, bool crit, bool kill, bool heal)
        {
            View = view; Tile = tile; Amount = amount; Absorbed = absorbed;
            IsCrit = crit; IsKill = kill; IsHeal = heal;
        }

        public static HitRecord Damage(UnitView view, GridPosition tile, int hpLost, int absorbed, bool crit, bool kill)
            => new HitRecord(view, tile, hpLost, absorbed, crit, kill, false);

        public static HitRecord Heal(UnitView view, GridPosition tile, int healed)
            => new HitRecord(view, tile, healed, 0, false, false, true);
    }

    /// <summary>
    /// Plays the feedback for one ability's hits: hit stop (a brief
    /// timeScale freeze), camera shake, white flash + recoil on survivors,
    /// a pop-and-fade on kills, floating damage/heal numbers, and a hit
    /// sound. How strong each is comes from the ability's AbilityVisuals.
    /// Everything is optional - with nothing assigned it just does less.
    ///
    /// Hit stop works by setting Time.timeScale to 0 for a few frames.
    /// The reaction/number coroutines use scaled time on purpose, so they
    /// freeze mid-flash. Don't use scaled time for anything that must keep
    /// moving through a hit stop.
    /// </summary>
    public sealed class HitFeedback : MonoBehaviour
    {
        [Header("References (both optional)")]
        [SerializeField] private CameraShake _cameraShake;
        [SerializeField] private AudioSource _audio;

        [Header("Impact feel")]
        [Tooltip("Crits and kills multiply hit stop, shake, and sound volume by this.")]
        [SerializeField, Min(1f)] private float _bigHitMultiplier = 2f;
        [Tooltip("Hard cap on any single hit stop, in real seconds.")]
        [SerializeField, Min(0f)] private float _maxHitStop = 0.2f;
        [SerializeField] private Color _flashColor = Color.white;
        [SerializeField, Min(0.01f)] private float _flashSeconds = 0.12f;

        [Header("Floating numbers")]
        [Tooltip("Number size relative to the unit glyph's font size.")]
        [SerializeField, Min(0.1f)] private float _numberScale = 0.6f;
        [SerializeField] private Color _damageColor = new Color(1f, 0.92f, 0.8f);
        [SerializeField] private Color _critColor = new Color(1f, 0.82f, 0.1f);
        [SerializeField] private Color _healColor = new Color(0.4f, 1f, 0.5f);
        [SerializeField] private Color _absorbColor = new Color(0.5f, 0.8f, 1f);

        private bool _stopping;
        private float _savedTimeScale = 1f;

        public IEnumerator Play(AbilityVisuals v, IReadOnlyList<HitRecord> hits, Vector3 attackerWorld)
        {
            float tile = UnitView.TileWorldSize();
            bool anyDamage = false;
            bool big = false;

            foreach (var h in hits)
            {
                var view = h.View;
                if (view == null) continue;
                Vector3 pos = view.transform.position;

                if (h.IsHeal)
                {
                    if (h.Amount > 0) SpawnNumber(view, "+" + h.Amount, _healColor, 1f, 0.9f);
                    continue;
                }

                anyDamage = true;
                big |= h.IsCrit || h.IsKill;

                if (h.Amount > 0)
                    SpawnNumber(view, h.IsCrit ? h.Amount + "!" : h.Amount.ToString(),
                        h.IsCrit ? _critColor : _damageColor, h.IsCrit ? 1.5f : 1f, h.IsCrit ? 1.2f : 0.9f);
                else if (h.Absorbed > 0)
                    SpawnNumber(view, "(" + h.Absorbed + ")", _absorbColor, 1f, 0.9f);

                Vector3 away = pos - attackerWorld;
                if (h.IsKill)
                    StartCoroutine(DeathPop(view, away));
                else if (view.isActiveAndEnabled)
                    view.StartCoroutine(view.PlayHitReaction(away, v.RecoilTiles, _flashColor, _flashSeconds));
            }

            if (!anyDamage) yield break;   // heals get numbers only: no freeze, no shake

            PlaySound(v, big);

            if (_cameraShake != null && v.ShakeStrengthTiles > 0f)
                _cameraShake.Shake(v.ShakeStrengthTiles * tile * (big ? _bigHitMultiplier : 1f), v.ShakeSeconds);

            float stop = Mathf.Min(v.HitStopSeconds * (big ? _bigHitMultiplier : 1f), _maxHitStop);
            if (stop > 0f) yield return HitStop(stop);
        }

        private IEnumerator HitStop(float seconds)
        {
            if (_stopping) yield break;
            _stopping = true;
            _savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = _savedTimeScale;
            _stopping = false;
        }

        // Never leave the game frozen if this object is disabled mid-stop.
        private void OnDisable()
        {
            if (_stopping)
            {
                Time.timeScale = _savedTimeScale;
                _stopping = false;
            }
        }

        private void PlaySound(AbilityVisuals v, bool big)
        {
            if (_audio == null || v.HitSounds == null || v.HitSounds.Length == 0) return;
            var clip = v.HitSounds[Random.Range(0, v.HitSounds.Length)];
            if (clip == null) return;

            _audio.pitch = Random.Range(0.92f, 1.08f) * (big ? 0.92f : 1f);   // heavier hits sound lower
            _audio.PlayOneShot(clip, Mathf.Clamp01(v.HitVolume * (big ? 1.25f : 1f)));
        }

        // ---- floating text -------------------------------------------------

        private void SpawnNumber(UnitView view, string text, Color color, float sizeMultiplier, float riseTiles)
        {
            float tile = UnitView.TileWorldSize();
            float jitter = Random.Range(-0.15f, 0.15f) * tile;
            var start = view.transform.position + new Vector3(jitter, 0.4f * tile, 0f);

            var glyph = view.Glyph;
            float size = (glyph != null ? glyph.fontSize : 36f) * _numberScale * sizeMultiplier;
            var label = SpawnText(glyph, text, color, size, start);
            StartCoroutine(FloatText(label, start, riseTiles * tile, 0.8f));
        }

        private static TextMeshPro SpawnText(TextMeshPro match, string text, Color color, float size, Vector3 world)
        {
            var go = new GameObject("FloatingText");
            go.transform.position = world;
            var t = go.AddComponent<TextMeshPro>();
            if (match != null) t.font = match.font;
            t.text = text;
            t.color = color;
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.sortingOrder = 100;
            return t;
        }

        // Quick pop (overshoot), rise with ease-out, fade over the last third.
        private IEnumerator FloatText(TextMeshPro t, Vector3 start, float rise, float seconds)
        {
            for (float e = 0f; e < seconds; e += Time.deltaTime)
            {
                if (t == null) yield break;
                float k = e / seconds;
                float pop = k < 0.15f
                    ? Mathf.Lerp(0.4f, 1.3f, k / 0.15f)
                    : Mathf.Lerp(1.3f, 1f, Mathf.Min(1f, (k - 0.15f) / 0.15f));
                t.transform.localScale = Vector3.one * pop;
                t.transform.position = start + Vector3.up * (rise * (1f - (1f - k) * (1f - k)));

                var c = t.color;
                c.a = k > 0.65f ? 1f - (k - 0.65f) / 0.35f : 1f;
                t.color = c;
                yield return null;
            }
            if (t != null) Destroy(t.gameObject);
        }

        // A killed unit's view is hidden the instant it dies, so this draws
        // a temporary white copy of its glyph that swells, drifts away from
        // the attacker, and fades.
        private IEnumerator DeathPop(UnitView view, Vector3 away)
        {
            var glyph = view.Glyph;
            if (glyph == null) yield break;

            float tile = UnitView.TileWorldSize();
            var start = view.transform.position;
            var drift = away.sqrMagnitude > 0.0001f ? away.normalized * tile * 0.5f : Vector3.zero;
            var ghost = SpawnText(glyph, glyph.text, _flashColor, glyph.fontSize, start);

            const float seconds = 0.28f;
            for (float e = 0f; e < seconds; e += Time.deltaTime)
            {
                if (ghost == null) yield break;
                float k = e / seconds;
                ghost.transform.localScale = Vector3.one * Mathf.Lerp(1f, 1.6f, k);
                ghost.transform.position = start + drift * k;
                var c = ghost.color;
                c.a = 1f - k;
                ghost.color = c;
                yield return null;
            }
            if (ghost != null) Destroy(ghost.gameObject);
        }
    }
}
