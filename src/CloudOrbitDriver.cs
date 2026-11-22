using UnityEngine;

namespace DenseDarkCloud
{
    /// <summary>标记：这个 GameObject 是本模组复制的乌云，位置由 CloudOrbitDriver 接管。</summary>
    public class DenseCloudMarker : MonoBehaviour
    {
    }

    /// <summary>
    /// 让本模组额外的乌云各自绕在玩家周围的不同方位上，同时保留原版"缓慢跟随"的手感。
    ///
    /// 原版 DarkCloudObject.Update 的移动逻辑是：
    ///   vector = avatar.position - transform.position
    ///   num    = |vector（y 方向 ×2）| - 1
    ///   num > 20 → 直接瞬移到玩家身上
    ///   否则    → position += vector.normalized * num * dt * 1.2
    /// 也就是"离得越远追得越快"的迟缓跟随。
    ///
    /// 这里用同一套公式，只把目标点从"玩家脚下"换成"玩家身边属于自己槽位的那个点"，
    /// 所以云会像原版一样拖在身后慢慢追上来，而不是硬贴在玩家身上。
    ///
    /// 位置由本组件自己积分维护（_pos），因为原版 Update 每帧也会写 transform.position，
    /// 如果只是每帧赋值会和原版的拉拽力互相抵消。
    /// </summary>
    public class CloudOrbitDriver : MonoBehaviour
    {
        public ComboEffect_DarkCloud effect;
        public int slot;

        private const float RingRadius = 1.6f;
        private const float RadiusStep = 0.42f;
        // 黄金角：任意朵数（1~7）都能均匀散开，不会有两个槽位重合。
        private const float AngleStep = 137.508f;
        // 与原版 DarkCloudObject.Update 完全相同的跟随参数。
        private const float FollowMultiplier = 1.2f;
        private const float TeleportDistance = 20f;
        // 原版用 1 作为"停下来"的死区；这里缩小到 0.35，云才会停在自己槽位的落点上
        // （否则每朵都要比原版多退 1 个单位，7 朵时最外圈会离玩家太远）。
        private const float DistanceOffset = 0.35f;

        private Vector3 _pos;
        private bool _initialized;
        private float _phase;

        private void Start()
        {
            _phase = slot * 1.37f;
        }

        private void LateUpdate()
        {
            if (effect == null)
            {
                return;
            }
            UnitAvatar avatar = effect.Networkavatar;
            if (avatar == null || avatar.transform == null)
            {
                if (Time.frameCount % 120 == 0)
                {
                    Debug.Log($"[DenseDarkCloud] orbit slot={slot} avatar=NULL pos={transform.position}");
                }
                return;
            }

            if (!_initialized)
            {
                _pos = transform.position;
                _initialized = true;
            }

            Vector3 avatarPos = avatar.transform.position;
            Vector3 target = avatarPos + Offset();

            // 与原版一致：用 y 方向拉长一倍的距离作为判定量。
            Vector3 vector = target - _pos;
            Vector3 shaped = vector;
            shaped.y *= 2f;
            float distance = shaped.magnitude - DistanceOffset;
            if (distance < 0f)
            {
                distance = 0f;
            }

            if (distance > TeleportDistance)
            {
                _pos = avatarPos;
            }
            else if (vector.sqrMagnitude > 1e-6f)
            {
                _pos += vector.normalized * distance * Time.deltaTime * FollowMultiplier;
            }

            transform.position = _pos;
        }

        /// <summary>本槽位相对玩家的落点偏移（带轻微漂浮摆动）。</summary>
        private Vector3 Offset()
        {
            float t = Time.time;
            float angle = (slot * AngleStep + Mathf.Sin(t * 0.55f + _phase) * 14f) * Mathf.Deg2Rad;
            float radius = RingRadius + slot * RadiusStep + Mathf.Sin(t * 0.87f + _phase) * 0.14f;
            return new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius * 0.55f,
                0f);
        }
    }
}
