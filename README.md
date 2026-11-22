# 乌云密布 Dense Dark Cloud

Sephiria 神器模组：新增**传说（红色）级**神器「乌云密布」，**连击类别 = 乌云**，按神器等级召唤 1~7 朵**相互独立**的乌云。

- modId：`liumin.dense-dark-cloud`
- 物品 id：`19050`
- 版本：0.2.2（游戏 1.1.4 实测）
- 参考神器：**雷文泥板**（`Item_DarkCloud_Critical`，同属乌云连击类别）
- 创意工坊：<https://steamcommunity.com/sharedfiles/filedetails/?id=3843983717>

## 安装

把本仓库的 `metadata.json`、`thumbnail.png` 和编译好的 `bin/DenseDarkCloud.dll` 放进：

```
<游戏安装目录>\WorkshopLocal\dense-dark-cloud\
├── metadata.json
├── thumbnail.png
└── bin\
    └── DenseDarkCloud.dll
```

游戏启动时会自动加载 `WorkshopLocal` 下的本地模组（默认启用，无需手动勾选）。
注意：**只要 `WorkshopLocal` 里有模组就无法多人联机**（这是游戏限制，不是本模组的问题）。

## 效果

| 神器等级 | 乌云数量 |
|---|---|
| 1 | 1 |
| 2 | 2 |
| 3 | 3 |
| 4 | 4 |
| 5 | 5 |
| 6 | 6 |
| 7 | 7（`Charm_Basic.maxLevel = 7`） |

- 稀有度 `EItemRarity.Legend`（红色传说）
- `item.categories` 写入乌云连击类别 id（运行时从组合效果预制体反查，实测为 `DARKCLOUD`），因此它会像雷文泥板一样**计入乌云连击**
- 每一朵乌云都是一个**独立克隆的 `ComboEffect_DarkCloud` 实例**：自己的层数 SyncVar、自己的计时器、自己的 `DarkCloudObject` 视觉对象
- 伤害 / 上限 / 回复速度全部走原版公式，模组没有改写任何数值
- **原版那一朵计入总数**：因为连击已改成乌云，物品本身就会让原版乌云组合效果成立，所以 1 级时是「原版 1 朵 + 克隆 0 朵」，7 级时是「原版 1 朵 + 克隆 6 朵」
- 位置：克隆云在玩家周围按**黄金角 137.5°**、半径 1.6 + 0.42×序号 分配自己的"落点"，任意朵数都不会有两条重合
- **跟随手感与原版一致**：用原版 `DarkCloudObject.Update` 同一套公式（`距离 × dt × 1.2`，越远追得越快，>20 单位瞬移），
  只是把目标点从"玩家脚下"换成"自己槽位的落点"，所以玩家跑起来时云会像原版一样拖在身后慢慢追上，而不是硬贴在身上

## 中文支持（游戏内 + 创意工坊）

`metadata.json` 的 `info.title` / `info.description` 是**语言映射**，游戏用 `WorkshopLocalizedText.Resolve()` 按这个顺序取：

```
当前语言（如 zh-CN） → 短代码（zh） → "default" → "en-US" → 第一条
```

两个不同的取值点：

| 取值点 | 用什么 | 结果 |
|---|---|---|
| 游戏内模组列表 / 创意工坊面板 | `.Resolve(language)` | 中文玩家看 `zh-CN`，英文玩家看 `en-US` |
| **上传到 Steam 创意工坊的标题/描述** | `.Default` = `Resolve(null)` | 命中 **`default`** → 中文 |

所以 `default` 一定要填中文，否则上传出去的创意工坊页面会是 `en-US`（英文）。当前配置：

```jsonc
"title": { "default": "乌云密布", "zh-CN": "乌云密布", "zh-TW": "烏雲密佈", "en-US": "Dense Dark Cloud" }
```

物品名 / 风味文本 / 效果文本走代码注册（`HorayModAPI.OnLocalizationReady` + `AddText`），已提供 `zh-CN`、`zh-TW`、`en-US` 三种。

## 获取方式

进局后在**许愿池（StartingFountain / 次元口袋）**里选「乌云密布」带入。模组会把物品 id 写进 `spawner.unlockedCharms`（许愿池候选来源）。

⚠️ 传说级的代价：许愿池按 `UI_DimensionPocketPanel.GetCapacity(rarity)` 判断容量，`Legend` 需要 **6 格**次元口袋容量。如果许愿池里看不到它，通常是你的 pocket 容量不够（换 Common 只要 3 格）。

## 已验证（游戏内实测 + 日志）

```
[DenseDarkCloud] cloud combo category id='DARKCLOUD'
[DenseDarkCloud] item registered id=19050 icon=True
[DenseDarkCloud] fountain check: cannotBeReward=False cannotSelectInDimensionPocket=False
                 isDual=False rarity=Legend capacityNeeded=6
[DenseDarkCloud] charm apply level=0 want=1  → Ensure total=1 vanilla=True extra=0
[DenseDarkCloud] charm apply level=7 want=7  → Ensure total=7 vanilla=True extra=6
[DenseDarkCloud] DEBUG set level 7 at 0,0 displayed=7
```

- 连击生效：`vanilla=True` 说明物品自带的乌云连击已经把原版组合效果拉起来了
- 7 级 → 7 朵（1 原版 + 6 克隆），各朵独立计数、位置错开

## 尚未实测

- 战斗中 7 朵云同时落雷的实际表现与 DPS
- 多人模式（声明 `everyone`，克隆对象用 `NetworkServer.Spawn(go, player)` 生成）

## 源码与构建

```
D:\dsahwork\dense-dark-cloud\
├── src\
│   ├── DenseDarkCloud.csproj      # GameDir 已指向你的安装目录
│   ├── Entry.cs                   # 注册神器、网络预制体、本地化、许愿池解锁、调试开关
│   ├── Charm_DenseDarkCloud.cs    # 神器本体，继承 Charm_Basic，等级上限 7
│   ├── CloudSwarm.cs              # 克隆 / 维护 / 清理乌云实例（原版那朵计入总数）
│   ├── CloudOrbitDriver.cs        # 黄金角错位驱动
│   └── CloudPatches.cs            # Harmony 补丁：给克隆云的视觉对象挂驱动
└── make_thumbnail.py
```

```powershell
cd D:\dsahwork\dense-dark-cloud\src
dotnet build -c Release      # 产物直接落到 WorkshopLocal\dense-dark-cloud\bin\
```

## 调试开关

在模组文件夹里放对应文件，重启游戏进局后生效（测完删掉）：

| 文件 | 作用 |
|---|---|
| `debug-grant.txt` | 进局直接召唤 4 朵云（不需要神器） |
| `debug-give-item.txt` | 进局直接获得一件「乌云密布」 |
| `debug-level-max.txt` | 配合上一个，把神器等级设为 7（最大级） |

## 实现上的坑（记下来免得重踩）

1. **`ItemDatabase.GetAllCharm()` 是废弃桩**：只会 `LogError("Deprecated")` 然后抛 `NotImplementedException`。找神器模板必须走 `GetAllItemID()` + `FindItemById()` 再筛 `type == EItemType.Charm`。
2. **`unlockedCharms` 里塞不存在的 id 会让存档直接崩**：原版 `PlayerSpawner.Initialize` 遍历时对 `FindItemById` 结果**不做空检查**。必须先确认 `FindItemById(19050) != null`，并等 `Initialize` 跑完（延迟约 2 秒）再写。
3. **连击类别 id 不要硬编码**：游戏里 `"DARKCLOUD"` 和 `"DarkCloud"` 两种大小写都出现过，直接用 `GetAllItemCategory()` 找「comboEffectPrefab 上挂 ComboEffect_DarkCloud」的那个类别最稳。
4. **克隆预制体要重置 `NetworkIdentity.hasSpawned = false`**，否则 Mirror 认为它已生成过。
5. **预制体本身必须 `activeSelf = true`**（只挂到未激活的 holder 下）：否则 Mirror 实例化出来的副本也是未激活的，`Update`/`OnStartClient` 不跑，云就「看不见也不动」。
6. **克隆云 `Enable()` 时必须传 `comboCount >= darkCloudComboCount`（默认 2）**，否则 `isDarkCloudActive` 不置 true。
7. 原版 `DarkCloudObject.Update` 会把云拉向玩家直到约 1 单位，多朵会挤成一点，所以需要用 `LateUpdate` 接管位置。
8. **接管位置时不能每帧硬贴**：原版是积分式跟踪（每帧 `+= 方向 * (距离-1) * dt * 1.2`），硬赋值会让云完全没有惯性、看起来很僵。正确做法是自己维护一个 `_pos` 做同样的积分，再写回 `transform.position`——因为原版 `Update` 每帧也会写这个属性，直接叠加两个力会互相抵消。
