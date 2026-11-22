using Mirror;
using UnityEngine;

namespace DenseDarkCloud
{
    /// <summary>
    /// 神器「乌云密布」。继承原版 Charm_Basic（神器基类），等级上限 7。
    /// 效果：保证玩家身上恰好有「神器等级」朵乌云，超出原版那一朵的部分由本模组克隆生成。
    /// </summary>
    public class Charm_DenseDarkCloud : Charm_Basic
    {
        /// <summary>当前应当存在的乌云总朵数 = 神器等级（1~5）。</summary>
        public int DesiredCloudCount()
        {
            int level = DisplayedLevel;
            if (level < 1)
            {
                level = 1;
            }
            if (level > maxLevel)
            {
                level = maxLevel;
            }
            return level;
        }

        private void ApplyClouds()
        {
            if (!NetworkServer.active || NetworkAvatar == null)
            {
                return;
            }
            int want = DesiredCloudCount();
            Debug.Log($"[DenseDarkCloud] charm apply level={DisplayedLevel} want={want}");
            CloudSwarm.Sync(NetworkAvatar, want);
        }

        protected override void OnEnabledEffect()
        {
            base.OnEnabledEffect();
            ApplyClouds();
        }

        protected override void OnUpdatedLevel(int oldLevel, int newLevel)
        {
            base.OnUpdatedLevel(oldLevel, newLevel);
            ApplyClouds();
        }

        protected override void OnDisabledEffect()
        {
            CloudSwarm.Clear(NetworkAvatar);
            base.OnDisabledEffect();
        }
    }
}
