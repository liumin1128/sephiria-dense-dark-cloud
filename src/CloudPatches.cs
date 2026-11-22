using HarmonyLib;
using UnityEngine;

namespace DenseDarkCloud
{
    /// <summary>
    /// 原版 ComboEffect_DarkCloud.OnStartClient 里会 spawn 云朵的视觉对象（DarkCloudObject）。
    /// 克隆出来的云需要给这个视觉对象挂上 CloudOrbitDriver，按槽位错开位置。
    /// </summary>
    [HarmonyPatch(typeof(ComboEffect_DarkCloud), "OnStartClient")]
    public static class CloudPatches
    {
        private static readonly AccessTools.FieldRef<ComboEffect_DarkCloud, DarkCloudObject> DarkCloudField =
            AccessTools.FieldRefAccess<ComboEffect_DarkCloud, DarkCloudObject>("darkCloudObject");

        [HarmonyPostfix]
        public static void Postfix(ComboEffect_DarkCloud __instance)
        {
            if (__instance == null)
            {
                return;
            }
            // 只处理本模组克隆出来的云，原版那朵保持原样。
            if (__instance.GetComponent<DenseCloudMarker>() == null)
            {
                return;
            }

            DarkCloudObject cloud = DarkCloudField(__instance);
            if (cloud == null)
            {
                return;
            }
            if (cloud.GetComponent<CloudOrbitDriver>() != null)
            {
                return;
            }

            int slot = Object.FindObjectsOfType<CloudOrbitDriver>().Length;
            CloudOrbitDriver driver = cloud.gameObject.AddComponent<CloudOrbitDriver>();
            driver.effect = __instance;
            driver.slot = slot;
            Debug.Log($"[DenseDarkCloud] cloud visual slot={slot}");
        }
    }
}
