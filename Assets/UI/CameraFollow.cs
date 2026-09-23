using UnityEngine;

namespace DungeonTower.UI
{
    /// <summary>
    /// Smoothly recenters the camera on the average world position of all
    /// living player units. Only X/Y move — Z stays whatever it was set
    /// to in the Editor, so this doesn't fight your existing depth/zoom.
    /// </summary>
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] private BattleController _battleController;
        [SerializeField] private float _followSpeed = 3f;

        private void LateUpdate()
        {
            var members = _battleController != null ? _battleController.PartyMembers : null;
            if (members == null || members.Count == 0) return;

            float sumX = 0f, sumY = 0f;
            int aliveCount = 0;
            foreach (var member in members)
            {
                if (!member.IsAlive) continue;
                var worldPos = GridToWorld.ToWorldPosition(member.Position);
                sumX += worldPos.x;
                sumY += worldPos.y;
                aliveCount++;
            }
            if (aliveCount == 0) return;

            var target = new Vector3(sumX / aliveCount, sumY / aliveCount, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, target, _followSpeed * Time.deltaTime);
        }
    }
}
