using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace DenseDarkCloud
{
    /// <summary>用来跑协程的常驻对象。</summary>
    public class ModRunner : MonoBehaviour
    {
        public static ModRunner Instance;

        public static ModRunner Get()
        {
            if (Instance == null)
            {
                GameObject go = new GameObject("DenseDarkCloud_Runner");
                UnityEngine.Object.DontDestroyOnLoad(go);
                Instance = go.AddComponent<ModRunner>();
            }
            return Instance;
        }
    }

    /// <summary>
    /// 模组入口：注册神器物品、网络预制体、本地化文本，并把神器写进许愿池候选列表。
    /// </summary>
    public class Entry : WorkshopModBase
    {
        public const int ItemId = 19050;
        public const string ModId = "liumin.dense-dark-cloud";
        public const string CharmPrefabKey = "densedarkcloud:charm";
        public const string CloudPrefabKey = "densedarkcloud:cloud";
        public const int DebugCloudCount = 4;

        public const string NameKey = "Item_DenseDarkCloud_Name";
        public const string FlavorKey = "Item_DenseDarkCloud_FlavorText";
        public const string EffectKey = "Charm_DenseDarkCloud_Effect";

        public static GameObject CharmPrefab;
        public static GameObject CloudPrefab;
        public static ItemEntity ModItem;

        /// <summary>固定 7 级上限。</summary>
        public const int MaxArtifactLevel = 7;

        private static ItemCategoryEntity _cloudCategory;

        private static GameObject _holder;
        private static bool _patched;
        private static bool _assetsBuilt;
        private static string _packageRoot;
        private static FieldInfo _hasSpawnedField;

        protected override void OnModLoaded()
        {
            _packageRoot = Context != null ? Context.PackageRoot : null;
            Debug.Log($"[DenseDarkCloud] OnModLoaded root={_packageRoot}");

            HorayModAPI.OnAllDatabasesReady += Build;
            HorayModAPI.OnLocalizationReady += OnLocalizationReady;
            HorayModAPI.OnPlayerAvatarSpawnedServerside += OnPlayerSpawnedServerside;
            HorayModAPI.OnUnitDespawnedServerside += OnUnitDespawnedServerside;

            InstallPatches();
        }

        protected override void OnModUnloaded()
        {
            HorayModAPI.OnAllDatabasesReady -= Build;
            HorayModAPI.OnLocalizationReady -= OnLocalizationReady;
            HorayModAPI.OnPlayerAvatarSpawnedServerside -= OnPlayerSpawnedServerside;
            HorayModAPI.OnUnitDespawnedServerside -= OnUnitDespawnedServerside;
            if (NetworkServer.active)
            {
                CloudSwarm.ClearAll();
            }
        }

        // ---------------------------------------------------------------- building

        private static void Build()
        {
            if (_assetsBuilt && ItemDatabase.FindItemById(ItemId) != null)
            {
                return;
            }
            try
            {
                if (!_assetsBuilt)
                {
                    BuildCloudPrefab();
                    BuildCharmPrefab();
                    _assetsBuilt = CharmPrefab != null && CloudPrefab != null;
                }
                if (!_assetsBuilt)
                {
                    Debug.LogError("[DenseDarkCloud] asset build failed");
                    return;
                }
                RegisterItem();
            }
            catch (Exception e)
            {
                Debug.LogError("[DenseDarkCloud] Build failed: " + e);
            }
        }

        private static GameObject Holder()
        {
            if (_holder == null)
            {
                _holder = new GameObject("DenseDarkCloud_PrefabHolder");
                _holder.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(_holder);
            }
            return _holder;
        }

        /// <summary>克隆一个模板预制体，并重置 NetworkIdentity.hasSpawned（否则会被当成已生成过）。</summary>
        private static GameObject InstantiateTemplate(GameObject original)
        {
            if (original == null)
            {
                return null;
            }
            GameObject go = UnityEngine.Object.Instantiate(original, Holder().transform);
            foreach (NetworkIdentity ni in go.GetComponentsInChildren<NetworkIdentity>(true))
            {
                try
                {
                    if (_hasSpawnedField == null)
                    {
                        _hasSpawnedField = typeof(NetworkIdentity).GetField("hasSpawned", BindingFlags.Instance | BindingFlags.NonPublic);
                    }
                    if (_hasSpawnedField != null)
                    {
                        _hasSpawnedField.SetValue(ni, false);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[DenseDarkCloud] reset hasSpawned failed: " + e.Message);
                }
            }
            return go;
        }

        /// <summary>
        /// 找"乌云"连击类别：以组合效果预制体上挂的是 ComboEffect_DarkCloud 为准，
        /// 不靠硬编码字符串（游戏里 "DARKCLOUD" 与 "DarkCloud" 两种大小写都出现过）。
        /// </summary>
        private static ItemCategoryEntity CloudCategory()
        {
            if (_cloudCategory != null)
            {
                return _cloudCategory;
            }
            ItemCategoryEntity[] all = ItemDatabase.GetAllItemCategory();
            if (all != null)
            {
                foreach (ItemCategoryEntity category in all)
                {
                    if (category == null || category.comboEffectPrefab == null)
                    {
                        continue;
                    }
                    if (category.comboEffectPrefab.GetComponent<ComboEffect_DarkCloud>() != null)
                    {
                        _cloudCategory = category;
                        Debug.Log($"[DenseDarkCloud] cloud combo category id='{category.id}'");
                        return _cloudCategory;
                    }
                }
            }
            _cloudCategory = ItemDatabase.FindItemCategory("DARKCLOUD");
            Debug.LogWarning($"[DenseDarkCloud] cloud category fallback: {(_cloudCategory != null ? _cloudCategory.id : "null")}");
            return _cloudCategory;
        }

        private static void BuildCloudPrefab()
        {
            if (CloudPrefab != null)
            {
                return;
            }
            ItemCategoryEntity category = CloudCategory();
            if (category == null || category.comboEffectPrefab == null)
            {
                Debug.LogError("[DenseDarkCloud] DARKCLOUD category or comboEffectPrefab missing");
                return;
            }
            GameObject go = InstantiateTemplate(category.comboEffectPrefab);
            if (go == null)
            {
                return;
            }
            go.name = "DenseDarkCloud_CloudPrefab";
            if (go.GetComponent<DenseCloudMarker>() == null)
            {
                go.AddComponent<DenseCloudMarker>();
            }
            // 注意：保持预制体自身 activeSelf = true，只让它待在未激活的 holder 下（不会 tick）。
            // 如果这里 SetActive(false)，Mirror 生成出来的副本也会是未激活的，云的 Update / OnStartClient 就不会跑。
            CloudPrefab = go;
            Debug.Log("[DenseDarkCloud] cloud prefab ready");
        }

        private static void BuildCharmPrefab()
        {
            if (CharmPrefab != null)
            {
                return;
            }
            GameObject template = FindCharmTemplate();
            if (template == null)
            {
                Debug.LogError("[DenseDarkCloud] no charm template found");
                return;
            }
            GameObject go = InstantiateTemplate(template);
            if (go == null)
            {
                return;
            }
            go.name = "DenseDarkCloud_CharmPrefab";

            // 把模板自带的神器效果摘掉，换成我们自己的（干净的默认值，不残留别的神器效果）。
            Charm_Basic old = go.GetComponent<Charm_Basic>();
            if (old != null)
            {
                UnityEngine.Object.DestroyImmediate(old);
            }

            Charm_DenseDarkCloud charm = go.AddComponent<Charm_DenseDarkCloud>();
            charm.maxLevel = MaxArtifactLevel;
            charm.isUniqueEffect = false;
            charm.isWeaponRelatedCharm = false;
            charm.darkCloud = false;
            charm.flameGround = false;
            charm.criteria = null;
            charm.advancedComboRequirements = new Charm_Basic.AdvancedComboRequirement[0];
            charm.connectedUniqueItems.Clear();
            charm.arrangementBonusIDs.Clear();
            charm.effectsString = new LocalizedString[1] { new LocalizedString(EffectKey) };

            CharmPrefab = go;
            Debug.Log($"[DenseDarkCloud] charm prefab ready (template={template.name})");
        }

        /// <summary>
        /// 找一个"最普通"的神器预制体当模板：优先使用只挂了原生 Charm_Basic 的。
        /// 注意：GetAllCharm() 在本版本是 Deprecated 并会抛 NotImplementedException，只能走 GetAllItemID()。
        /// </summary>
        private static GameObject FindCharmTemplate()
        {
            GameObject fallback = null;
            int[] ids = ItemDatabase.GetAllItemID();
            if (ids == null)
            {
                return null;
            }
            foreach (int id in ids)
            {
                ItemEntity entity = ItemDatabase.FindItemById(id);
                if (entity == null || entity.type != EItemType.Charm || entity.resourcePrefab == null)
                {
                    continue;
                }
                Charm_Basic basic = entity.resourcePrefab.GetComponent<Charm_Basic>();
                if (basic == null)
                {
                    continue;
                }
                if (fallback == null)
                {
                    fallback = entity.resourcePrefab;
                }
                if (basic.GetType() == typeof(Charm_Basic))
                {
                    return entity.resourcePrefab;
                }
            }
            return fallback;
        }

        private static void RegisterItem()
        {
            if (ItemDatabase.FindItemById(ItemId) != null)
            {
                return;
            }
            ItemCategoryEntity cloudCategory = CloudCategory();
            Sprite icon = cloudCategory != null ? cloudCategory.categoryIcon : null;
            if (icon == null)
            {
                icon = ItemDatabase.GetDummyIconSprite();
            }

            ItemEntity item = ScriptableObject.CreateInstance<ItemEntity>();
            item.name = "DenseDarkCloud_Item";
            item.id = ItemId;
            item.type = EItemType.Charm;
            item.aName = new LocalizedString(NameKey);
            item.aFlavorText = new LocalizedString(FlavorKey);
            // 连击 = 乌云：和参考神器「雷文泥板」(Item_DarkCloud_Critical) 一样计入乌云连击类别。
            item.categories = new List<string>();
            if (cloudCategory != null && !string.IsNullOrEmpty(cloudCategory.id))
            {
                item.categories.Add(cloudCategory.id);
            }
            item.cost = 250;
            item.customCost = false;
            // 红色传说级。
            item.rarity = EItemRarity.Legend;
            item.activeType = EItemActiveType.Default;
            item.resourcePrefab = CharmPrefab;
            // 这两个必须是 false，否则 StartingFountain 会把它过滤掉。
            item.cannotBeReward = false;
            item.isDual = false;
            item.cannotSelectInDimensionPocket = false;
            item.icon = icon;
            item.iconInWorld = icon;

            HorayModAPI.RegisterNetworkPrefab(CharmPrefab, CharmPrefabKey);
            HorayModAPI.RegisterNetworkPrefab(CloudPrefab, CloudPrefabKey);
            ItemDatabase.Register(item);
            ModItem = item;
            Debug.Log($"[DenseDarkCloud] item registered id={ItemId} icon={(icon != null)}");
            Debug.Log($"[DenseDarkCloud] fountain check: cannotBeReward={item.cannotBeReward} cannotSelectInDimensionPocket={item.cannotSelectInDimensionPocket} isDual={item.isDual} rarity={item.rarity} capacityNeeded={UI_DimensionPocketPanel.GetCapacity(item.rarity)}");
        }

        // ------------------------------------------------------------ localization

        private static void OnLocalizationReady(HorayModLocalizationContext ctx)
        {
            if (ctx == null)
            {
                return;
            }
            ctx.AddText("zh-CN", NameKey, "乌云密布");
            ctx.AddText("zh-CN", FlavorKey, "乌云不再形单影只。神器每提升一级，便多聚起一朵乌云，最多七朵。它们各自积蓄雷电，各自落下雷击。");
            ctx.AddText("zh-CN", EffectKey, "乌云数量随神器等级提升（1→7 朵），每朵独立蓄雷、独立落雷");

            ctx.AddText("zh-TW", NameKey, "烏雲密佈");
            ctx.AddText("zh-TW", FlavorKey, "烏雲不再形單影隻。神器每提升一級，便多聚起一朵烏雲，最多七朵。它們各自積蓄雷電，各自落下雷擊。");
            ctx.AddText("zh-TW", EffectKey, "烏雲數量隨神器等級提升（1→7 朵），每朵獨立蓄雷、獨立落雷");

            ctx.AddText("en-US", NameKey, "Dense Dark Cloud");
            ctx.AddText("en-US", FlavorKey, "No longer alone in the sky. Each artifact level gathers one more dark cloud, up to seven. Each cloud gathers and spends its own lightning.");
            ctx.AddText("en-US", EffectKey, "Dark clouds rise with artifact level (1→7); each gathers and spends its own lightning");

            Debug.Log("[DenseDarkCloud] localization added");
        }

        // ------------------------------------------------------------- fountain grant

        private static void OnPlayerSpawnedServerside(PlayerAvatar avatar)
        {
            if (avatar == null || !NetworkServer.active)
            {
                return;
            }
            ModRunner.Get().StartCoroutine(UnlockAndMaybeDebug(avatar));
        }

        private static IEnumerator UnlockAndMaybeDebug(PlayerAvatar avatar)
        {
            for (int i = 0; i < 600; i++)
            {
                if (avatar == null)
                {
                    yield break;
                }
                if (avatar.spawner != null && avatar.Inventory != null)
                {
                    break;
                }
                yield return null;
            }
            if (avatar == null || avatar.spawner == null)
            {
                Debug.LogWarning("[DenseDarkCloud] spawner never ready");
                yield break;
            }

            // 等 PlayerSpawner.Initialize 彻底结束再动 unlockedCharms，避免它正在遍历时被改。
            for (int i = 0; i < 120; i++)
            {
                yield return null;
            }

            try
            {
                // 重要：原版 PlayerSpawner.Initialize 会遍历 unlockedCharms 并对 FindItemById 的结果
                // 直接解引用、不做空检查。所以只有确认物品真的注册成功后才允许写入，否则会崩掉整个存档。
                if (ItemDatabase.FindItemById(ItemId) == null)
                {
                    Debug.LogError($"[DenseDarkCloud] item {ItemId} not registered, skip unlock");
                    yield break;
                }
                if (!avatar.spawner.unlockedCharms.Contains(ItemId))
                {
                    avatar.spawner.unlockedCharms.Add(ItemId);
                    Debug.Log($"[DenseDarkCloud] unlocked {ItemId} for the wishing fountain");
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[DenseDarkCloud] unlock failed: " + e);
            }

            if (HasFlag("debug-give-item.txt") && ModItem != null)
            {
                for (int i = 0; i < 120; i++)
                {
                    yield return null;
                }
                try
                {
                    int instanceId = ItemDatabase.GenerateInstanceID(new System.Random());
                    avatar.Inventory.AddItem(new ItemMetadata(instanceId, ModItem, 1), 0, true, true);
                    Debug.Log($"[DenseDarkCloud] DEBUG gave artifact id={ItemId} instance={instanceId}");
                }
                catch (Exception e)
                {
                    Debug.LogError("[DenseDarkCloud] DEBUG give artifact failed: " + e);
                }

                if (HasFlag("debug-level-max.txt"))
                {
                    for (int i = 0; i < 180; i++)
                    {
                        yield return null;
                    }
                    try
                    {
                        foreach (KeyValuePair<ItemPosition, Charm_Basic> pair in avatar.Inventory.charms)
                        {
                            if (!(pair.Value is Charm_DenseDarkCloud charm))
                            {
                                continue;
                            }
                            avatar.Inventory.levelMatrix[pair.Key] = MaxArtifactLevel;
                            charm.RefreshCharm();
                            Debug.Log($"[DenseDarkCloud] DEBUG set level {MaxArtifactLevel} at {pair.Key.x},{pair.Key.y} displayed={charm.DisplayedLevel}");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("[DenseDarkCloud] DEBUG max level failed: " + e);
                    }
                }
            }

            if (!HasFlag("debug-grant.txt"))
            {
                yield break;
            }

            if (CloudPrefab == null || ModItem == null)
            {
                Debug.LogWarning("[DenseDarkCloud] DEBUG skipped: assets not ready");
                yield break;
            }

            for (int i = 0; i < 120; i++)
            {
                yield return null;
            }
            Debug.Log($"[DenseDarkCloud] DEBUG grant {DebugCloudCount} clouds");
            CloudSwarm.Sync(avatar, DebugCloudCount);
        }

        private static bool HasFlag(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(_packageRoot))
                {
                    return false;
                }
                return File.Exists(Path.Combine(_packageRoot, fileName));
            }
            catch
            {
                return false;
            }
        }

        private static void OnUnitDespawnedServerside(UnitAvatar unit)
        {
            if (unit is PlayerAvatar && NetworkServer.active)
            {
                CloudSwarm.Clear(unit);
            }
        }

        // ----------------------------------------------------------------- harmony

        private static void InstallPatches()
        {
            if (_patched)
            {
                return;
            }
            try
            {
                Harmony harmony = new Harmony(ModId);
                harmony.PatchAll(typeof(CloudPatches).Assembly);
                _patched = true;
                Debug.Log("[DenseDarkCloud] harmony patched");
            }
            catch (Exception e)
            {
                Debug.LogError("[DenseDarkCloud] harmony patch failed: " + e);
            }
        }
    }
}
