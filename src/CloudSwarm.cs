using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace DenseDarkCloud
{
    /// <summary>
    /// 管理某个单位身上由本模组克隆出来的乌云。
    /// 每朵乌云都是一个独立的 ComboEffect_DarkCloud 实例：各自持有 darkCloud 层数、
    /// 各自的计时器与视觉对象，数值全部沿用原版公式（MaxDarkCloud / 伤害常量 / 回复速度）。
    /// </summary>
    public static class CloudSwarm
    {
        private static readonly Dictionary<UnitAvatar, List<ComboEffect_DarkCloud>> Clouds =
            new Dictionary<UnitAvatar, List<ComboEffect_DarkCloud>>();

        private static List<ComboEffect_DarkCloud> ListOf(UnitAvatar avatar)
        {
            if (!Clouds.TryGetValue(avatar, out List<ComboEffect_DarkCloud> list) || list == null)
            {
                list = new List<ComboEffect_DarkCloud>();
                Clouds[avatar] = list;
            }
            list.RemoveAll(e => e == null);
            return list;
        }

        /// <summary>算上原版那一朵，当前一共有几朵乌云。</summary>
        public static int TotalCount(UnitAvatar avatar)
        {
            if (avatar == null)
            {
                return 0;
            }
            int count = ListOf(avatar).Count;
            ComboEffect_DarkCloud vanilla = FindVanilla(avatar);
            if (vanilla != null && vanilla.isEnabled)
            {
                count++;
            }
            return count;
        }

        public static ComboEffect_DarkCloud FindVanilla(UnitAvatar avatar)
        {
            if (avatar == null || avatar.Inventory == null)
            {
                return null;
            }
            return avatar.Inventory.FindComboEffect("DARKCLOUD") as ComboEffect_DarkCloud;
        }

        /// <summary>把乌云总数同步到 total：多了就全清重来，少了就补。</summary>
        public static void Sync(UnitAvatar avatar, int total)
        {
            if (!NetworkServer.active || avatar == null)
            {
                return;
            }
            if (total < TotalCount(avatar))
            {
                Clear(avatar);
            }
            Ensure(avatar, total);
        }

        /// <summary>把乌云总数调整到 total（原版那一朵计入其中）。</summary>
        public static void Ensure(UnitAvatar avatar, int total)
        {
            if (!NetworkServer.active || avatar == null || avatar.Inventory == null)
            {
                return;
            }
            if (Entry.CloudPrefab == null || avatar.IsDead)
            {
                return;
            }

            List<ComboEffect_DarkCloud> mine = ListOf(avatar);
            int have = mine.Count;
            ComboEffect_DarkCloud vanilla = FindVanilla(avatar);
            if (vanilla != null && vanilla.isEnabled)
            {
                have++;
            }

            int want = Mathf.Clamp(total, 0, 8);
            for (int i = have; i < want; i++)
            {
                ComboEffect_DarkCloud spawned = SpawnOne(avatar, vanilla);
                if (spawned == null)
                {
                    break;
                }
                mine.Add(spawned);
            }
            Debug.Log($"[DenseDarkCloud] Ensure total={want} vanilla={(vanilla != null && vanilla.isEnabled)} extra={mine.Count}");
        }

        private static ComboEffect_DarkCloud SpawnOne(UnitAvatar avatar, ComboEffect_DarkCloud vanilla)
        {
            GameObject go = Object.Instantiate(Entry.CloudPrefab);
            go.name = "DenseDarkCloud_Cloud";
            ComboEffect_DarkCloud effect = go.GetComponent<ComboEffect_DarkCloud>();
            if (effect == null)
            {
                Debug.LogError("[DenseDarkCloud] cloud prefab has no ComboEffect_DarkCloud");
                Object.Destroy(go);
                return null;
            }

            effect.SetAvatar("DARKCLOUD", avatar);

            // 和原版一样：挂在玩家身上生成，玩家作为 owner（客户端才能正确收到同步与特效）。
            NetworkServer.Spawn(go, avatar.gameObject);

            // 原版只有在 comboCount >= darkCloudComboCount 时才会把 isDarkCloudActive 置为 true。
            int combo = vanilla != null ? Mathf.Max(vanilla.comboCount, effect.darkCloudComboCount) : effect.darkCloudComboCount;
            effect.Enable(combo, out int applied);
            Debug.Log($"[DenseDarkCloud] spawned extra cloud combo={combo} applied={applied}");
            return effect;
        }

        /// <summary>移除本模组克隆的全部乌云（原版那朵不动）。</summary>
        public static void Clear(UnitAvatar avatar)
        {
            if (avatar == null)
            {
                return;
            }
            if (!Clouds.TryGetValue(avatar, out List<ComboEffect_DarkCloud> list) || list == null)
            {
                return;
            }
            foreach (ComboEffect_DarkCloud effect in list)
            {
                if (effect == null)
                {
                    continue;
                }
                if (NetworkServer.active)
                {
                    NetworkServer.Destroy(effect.gameObject);
                }
            }
            list.Clear();
            Clouds.Remove(avatar);
        }

        public static void ClearAll()
        {
            foreach (KeyValuePair<UnitAvatar, List<ComboEffect_DarkCloud>> pair in Clouds)
            {
                if (pair.Value == null)
                {
                    continue;
                }
                foreach (ComboEffect_DarkCloud effect in pair.Value)
                {
                    if (effect != null && NetworkServer.active)
                    {
                        NetworkServer.Destroy(effect.gameObject);
                    }
                }
            }
            Clouds.Clear();
        }
    }
}
