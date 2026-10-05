using UnityEngine;

namespace DungeonTower.UI
{
    /// <summary>
    /// Adds a short, decaying random offset to the camera. Put it on the
    /// Main Camera. It runs after CameraFollow, and it removes its own
    /// previous offset only if nothing else moved the camera in between,
    /// so it works whether the follow script sets the position outright
    /// or eases toward a target. Uses unscaled time so the shake keeps
    /// going through a hit stop.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public sealed class CameraShake : MonoBehaviour
    {
        private float _strength;   // world units, at the start of the shake
        private float _duration;
        private float _elapsed;
        private Vector3 _lastOffset;
        private Vector3 _lastFinal;
        private bool _hasLast;

        /// <summary>Starts a shake. A weaker shake never cuts off a stronger one in progress.</summary>
        public void Shake(float strengthWorldUnits, float seconds)
        {
            if (strengthWorldUnits <= 0f || seconds <= 0f) return;
            if (_elapsed < _duration && strengthWorldUnits < CurrentStrength()) return;

            _strength = strengthWorldUnits;
            _duration = seconds;
            _elapsed = 0f;
        }

        private float CurrentStrength()
        {
            if (_duration <= 0f || _elapsed >= _duration) return 0f;
            float remaining = 1f - _elapsed / _duration;
            return _strength * remaining * remaining;
        }

        private void LateUpdate()
        {
            var position = transform.position;

            // If nobody moved the camera since we last offset it, undo our offset.
            // If something did (a follow script), its position is the new base.
            if (_hasLast && (position - _lastFinal).sqrMagnitude < 1e-10f)
                position -= _lastOffset;

            var offset = Vector3.zero;
            if (_elapsed < _duration)
            {
                _elapsed += Time.unscaledDeltaTime;
                Vector2 jolt = Random.insideUnitCircle * CurrentStrength();
                offset = new Vector3(jolt.x, jolt.y, 0f);
            }

            transform.position = position + offset;
            _lastOffset = offset;
            _lastFinal = transform.position;
            _hasLast = true;
        }
    }
}
