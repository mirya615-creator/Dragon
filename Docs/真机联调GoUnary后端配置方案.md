# 真机联调 GoUnary 后端配置方案（方案 B）

> 目标：让 Android **真机**上的包走通 GoUnary 在线后端（游客登录 → Main → 对局 → 结算）。
> 定位：**仅开发/联调用途**。正式包必须回退为 HTTPS 域名 + 关闭明文放行（见 Step 7）。

---

## 0. 现状核实（本方案的事实前提）

| 项 | 实测值 | 来源 |
|---|---|---|
| 后端模式 | `backendMode: 1`（GoUnary） | `Assets/Resources/Configuration/ClientServiceConfig.asset:15` |
| API 地址 | `apiBaseUrl: http://192.168.31.221:8081` | 同上 `:16` |
| 网络日志 | `enableNetworkLogging: 0`（关闭） | 同上 `:24` |
| Unity 版本 | `2022.3.62f3c1` | `ProjectSettings/ProjectVersion.txt` |
| minSdk / targetSdk | **24 / 35** | 合并后 manifest |
| 架构 / 后端 | ARM64 / IL2CPP | `DragonBoundAndroidBuild.cs:335-336` |
| 包名 | `com.drakeforge.mergedefense` | 合并后 manifest |
| 自定义主 manifest | **不存在** | `Assets/Plugins/Android/` 下无 `AndroidManifest.xml` |
| 已接入 manifest 合并的插件 | `FirebaseApp.androidlib`、`DragonBoundAnalytics.androidlib` | `Assets/Plugins/Android/` |
| 主 manifest 承载方 | `unityLibrary` 模块（Activity、INTERNET 权限都在这里） | `Library/Bee/.../unityLibrary/src/main/AndroidManifest.xml` |
| 传输层失败表现 | `ClientServiceException(code=NETWORK_ERROR, message="Unable to reach the game server.")`，`responseCode=0` | `UnityWebRequestUnaryTransport.cs:163-165` |

> **【2026-09-10 现场复核】** 本次实测有 4 处偏差，直接影响 B-LAN 步骤：
> 1. 本机局域网 IP 已变为 **`192.168.31.34`**（配置里的 `192.168.31.221` 已失效）——B-LAN 必须改地址。
> 2. **8081 入站防火墙已被现有规则放行**（端口段 `8081-8112`，作用于全部配置文件，已启用）→ **B-LAN 无需新建防火墙规则**。
> 3. 当前活动防火墙配置文件是 **「公用」**，不是「专用」→ 若自建规则写成 `-Profile Private` 会**静默不生效**。
> 4. 复核时 **8081 无任何进程监听** → 后端服务当前**未启动**，B-LAN 第一步必须先把它跑起来。

### 两个硬性阻断条件

1. **路由不可达**：`192.168.31.221` 是开发机局域网地址，手机不在该网段 / 服务未监听对外地址 / 防火墙拦截 → 直接连不上。
2. **明文 HTTP 被系统拦截**：`targetSdk=35` ≥ 28，Android 默认 `cleartextTrafficPermitted=false`。合并后的 manifest **完全没有** `usesCleartextTraffic` 或 `networkSecurityConfig` → 即便同 Wi-Fi 可达，请求也会被系统在 socket 层拒掉，同样表现为 `NETWORK_ERROR`。

> ⚠️ 这两条必须**同时**解决。只改 `apiBaseUrl` 或只开明文，任一单独动作都不会让真机跑通。
> 这也是为什么同样的地址在 **Unity Editor 里能通**（Editor 不受 Android 明文策略约束），一到真机就卡登录页。

---

## 1. 子路径选择（先定这一件事）

| 路径 | 地址写法 | 优点 | 代价 | 适用 |
|---|---|---|---|---|
| **B-ADB**（推荐先试） | `http://127.0.0.1:8081` | 不依赖 Wi-Fi 同网段、不受 Windows 防火墙影响、无需改服务监听地址 | 需 USB 连接 + adb；**拔线/重启 adb 后需重跑 `adb reverse`** | 有 USB 数据线的日常联调 |
| **B-LAN** | `http://<PC局域网IP>:8081` | 一次配置长期有效，手机可脱离 USB | 需同 Wi-Fi、需服务监听 `0.0.0.0`、需放行防火墙 | 手机自由走动测试 |
| **B-TUNNEL** | `https://<隧道域名>` | 脱离局域网、且是 HTTPS（**无需改 manifest**）、最接近正式形态 | 需装内网穿透工具、有额外延迟 | 远程/异地联调、想顺带验证 HTTPS 链路 |

**决策建议**：先用 **B-ADB** 打通（变量最少），确认链路无误后，如果你需要脱离 USB，再切 **B-LAN**。B-TUNNEL 可直接跳过 Step 2（明文放行）。

---

## 2. Step 1 — 让后端在真机上可达

### 2.1（通用）确认 Go 服务确实在跑，且监听地址正确

服务端源码不在本工作区，需在服务端机器上确认：

- 监听必须是 `0.0.0.0:8081`（或 `:8081`），**不能是 `127.0.0.1:8081`**——后者只有本机能连。
- 验证（服务端机器上执行）：
  ```bash
  netstat -ano | findstr :8081
  ```
  期望看到 `0.0.0.0:8081` 处于 `LISTENING`。若只看到 `127.0.0.1:8081`，改服务启动参数。

### 2.2（B-ADB）建立 USB 反向端口映射

> **本机实测（2026-09-10）**：`adb` 不在 PATH，可直接用 Unity 自带：
> `D:\2022.3.62f3c1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe`。
> 检测到设备 `9B051FFAZ00F5M` 处于 **`unauthorized`**——先在手机上确认「允许 USB 调试」授权弹窗，状态变为 `device` 后才能继续。

手机 USB 连上并开启「USB 调试」后，在 PC 上执行：

```bash
adb devices                      # 确认设备状态为 device（不是 unauthorized / offline）
adb reverse tcp:8081 tcp:8081    # 把手机的 8081 反向映射到 PC 的 127.0.0.1:8081
adb reverse --list               # 应输出 127.0.0.1:8081 tcp:8081
```

要点：
- `adb reverse` 的好处是**服务端只监听 127.0.0.1 也能通**，且完全绕开局域网与防火墙。
- 它是**会话级**的：拔线、`adb kill-server`、手机重启后失效，需重新执行。
- 因此在 `ClientServiceConfig.asset` 里写 `http://127.0.0.1:8081`。

### 2.3（B-LAN）确认手机能路由到 PC

> **本机实测（2026-09-10）**：PC 局域网 IP = **`192.168.31.34`**（`192.168.31.0/24` 网段）；当前活动防火墙配置文件 = **「公用」**；**8081 入站已被现有规则放行**（见第 2 步）；复核时 **8081 无监听**（后端未启动）。

1. 确认 PC 当前局域网 IP（**不要照抄旧的 `192.168.31.221`，它已失效**）：
   ```bash
   ipconfig
   ```
   取手机所连 Wi-Fi **同网段**网卡的 `IPv4 地址`。本次实测为 `192.168.31.34`。
2. **防火墙（本次实测无需操作）**：本机已存在一条**已启用**的入站 TCP 规则，放行本地端口 **`8081-8112`**，作用于`域 / 专用 / 公用`**全部**配置文件 —— 端口 `8081` 已经开了，**不必新建规则**。

   仅当你需要自建一条**项目专属**规则时，注意：
   - ❌ `-Profile Private` 在当前「公用」网络下**不会生效**（规则存在但不匹配当前网络，等于没开）。
   - ✅ 改用 `-Profile Any`（或与你实际网络配置文件一致的值）：
     ```powershell
     New-NetFirewallRule -DisplayName "DragonBound Dev API 8081" `
       -Direction Inbound -Protocol TCP -LocalPort 8081 -Action Allow -Profile Any
     ```
   - 自建规则请记在 Step 7 一并删除。
3. **连通性前置验证（关键，先于改代码）**：手机与 PC 连同一 Wi-Fi，用**手机浏览器**打开：
   ```
   http://192.168.31.34:8081/
   ```
   - 有响应（哪怕是 404 / JSON 报错）→ 路由与端口已通，继续。
   - 转圈/超时 → 先解决网络（换 Wi-Fi、**关闭路由器的 AP 隔离 / 客户端隔离**、确认同网段、检查防火墙），**不要往下走**。
4. 若手机浏览器能打开但游戏内仍失败，基本就只剩明文 HTTP 问题 → 去 Step 2。
5. **稳定性建议**：PC 走 DHCP 时 IP 会变（本项目的 `192.168.31.221 → .34` 就是这么发生的）。B-LAN 想长期可用，请给 PC 网卡设**静态 IP** 或在路由器做 **DHCP 静态绑定**，否则每次 IP 变化都要重新改配置 + 重编 APK。

---

## 3. Step 2 — 允许明文 HTTP（B-ADB / B-LAN 必须；B-TUNNEL 可跳过）

### 3.1 新建自定义主 manifest

**新建文件**：`Assets/Plugins/Android/AndroidManifest.xml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android"
          xmlns:tools="http://schemas.android.com/tools"
          android:installLocation="preferExternal">

  <supports-screens
      android:smallScreens="true"
      android:normalScreens="true"
      android:largeScreens="true"
      android:xlargeScreens="true"
      android:anyDensity="true" />

  <!-- 仅开发联调：允许明文 HTTP。正式包必须删除本属性并改用 HTTPS。 -->
  <application
      android:label="@string/app_name"
      android:icon="@mipmap/app_icon"
      android:usesCleartextTraffic="true"
      tools:targetApi="28" />
</manifest>
```

**为什么内容要写这么多**：Unity 的「Custom Main Manifest」是**替换**而非叠加 `launcher` 模块的默认 manifest（默认那份只含 label/icon/supports-screens/installLocation）。上面这份把默认项原样保留、只加 `usesCleartextTraffic`，因此不会丢任何现有配置。

**不需要写进这份 manifest 的东西**（它们来自 `unityLibrary` 与各 androidlib，合并时会自动带进来）：
- `UnityPlayerActivity` 及 LAUNCHER intent-filter
- `<uses-permission android:name="android.permission.INTERNET" />`
- Firebase / Google Play Services 的 receiver、service、provider、permission
- `versionCode` / `versionName`（由 `mainTemplate.gradle` 的 `defaultConfig` 注入）

**不要写 `package="..."`**：包名由 `mainTemplate.gradle` 的 `namespace` 与 Player Settings 管理，手写容易触发 AGP 的 namespace 一致性报错。

### 3.2 确认 Player Settings 开关已启用

`File → Build Settings → Player Settings → Publishing Settings`：
- 应看到 **Custom Main Manifest** 已被勾选（Unity 检测到该文件存在后会自动勾上；若未勾选，手动勾选，Unity 会用自己的模板覆盖你刚写的文件，**届时需要把上面的内容重新贴回去**）。
- 顺带确认 **Custom Gradle Template** 已勾选（本项目已经在用 `mainTemplate.gradle`，属正常现状）。

### 3.3 重新构建后验证明文确实生效（客观判定，别靠猜）

构建一次后，检查 Unity 生成的**合并后 manifest**：

```
Library/Bee/Android/Prj/IL2CPP/Gradle/launcher/build/intermediates/merged_manifest/debug/AndroidManifest.xml
```

在 `<application ...>` 标签上应出现：
```xml
android:usesCleartextTraffic="true"
```
- 出现 → Step 2 生效，继续。
- 没出现 → 自定义 manifest 未被采用（多为 3.2 的开关未生效），回到 3.2 重做。

### 3.4（备选）精确放行方案

如果不想全局开明文，可改用 network security config 只放行开发域名：

`Assets/Plugins/Android/res/xml/network_security_config.xml`
```xml
<?xml version="1.0" encoding="utf-8"?>
<network-security-config>
  <domain-config cleartextTrafficPermitted="true">
    <domain includeSubdomains="false">192.168.31.34</domain>
    <domain includeSubdomains="false">127.0.0.1</domain>
  </domain-config>
</network-security-config>
```
然后在 manifest 的 `<application>` 上加 `android:networkSecurityConfig="@xml/network_security_config"`（替代 `usesCleartextTraffic`）。

> 注意：`Assets/Plugins/Android/res/` 是否被 Unity 收进 launcher 模块的资源目录，需在构建后用「解包 APK / 看 merged manifest 是否含该属性」实测确认。若不生效，改用 3.1 的全局 `usesCleartextTraffic`，或仿照现有 `DragonBoundAnalytics.androidlib` 建一个 androidlib 插件来承载该属性与资源。**联调不必纠结，3.1 更省事。**

---

## 4. Step 3 — 配置 `apiBaseUrl`

编辑 `Assets/Resources/Configuration/ClientServiceConfig.asset`：

```yaml
  backendMode: 1                                        # 保持 GoUnary
  apiBaseUrl: http://127.0.0.1:8081                     # B-ADB
                                                        # B-LAN 改为 http://192.168.31.34:8081（本次实测 IP）
                                                        # B-TUNNEL 改为 https://<隧道域名>
  timeoutSeconds: 15
  enableNetworkLogging: 1                               # ★ 联调期务必打开，见 Step 4
  requireAuthoritativeRunContract: 1                    # 保持
```

约束（来自 `UnityWebRequestUnaryTransport` 构造函数，`cs:37-48`）：
- 必须是**绝对** HTTP(S) URL，不能是相对路径；
- 末尾斜杠会被自动裁掉，`.../api` 这类子路径可以；
- 写空 → `GoUnaryServiceModule.Validate` 抛 `Go backend mode requires an API base URL.`（`cs:64-65`），且异常发生在 `BeforeSceneLoad`，会导致**整个服务层构建失败**，表现比"连不上"更严重。

> ⚠️ 该文件在 `Resources/` 下，是**打进包里的**：改完**必须重新构建 APK**，热重启 App 不生效。

---

## 5. Step 4 — 打开网络日志（强烈建议）

`enableNetworkLogging: 1` 后，`UnityWebRequestUnaryTransport` 会输出（`cs:91,106`）：
```
[API] POST /v1/...         ← 请求发出
[API] 200 POST /v1/...     ← 状态码
```
失败时状态码为 `0`，配合 `ClientServiceException` 的 `NETWORK_ERROR` 可直接区分"没发出去"和"发出去了被拒"。

---

## 6. Step 5 — 构建与安装

### 方式一：Unity 编辑器 GUI
`File → Build Settings → Android → Build`，输出 `Builds/Android/DragonBound-Greybox.apk`（场景顺序：`Login` → `Main` → `Greybox_Main`）。

### 方式二：命令行（工程内已有构建方法）
`Assets/DragonBound/Editor/DragonBoundAndroidBuild.cs:320` 的 `BuildApk()` 不是菜单项，需用 `-executeMethod` 调用：
```bash
"<Unity编辑器路径>/Unity.exe" -quit -batchmode -projectPath "D:\Codex project\Dragon" \
  -executeMethod DragonBound.Editor.DragonBoundAndroidBuild.BuildApk -logFile -
```
它会强制：包名 `com.drakeforge.mergedefense`、IL2CPP、ARM64、竖屏、`buildAppBundle=false`。

> 注意该方法用的是 `BuildOptions.None`（非 Development Build）。IL2CPP 非开发包同样会把 `Debug.Log` 输出到 logcat，联调够用；若需要更完整的堆栈，手工在 Build Settings 里勾上 `Development Build`。

### 安装
```bash
adb install -r "Builds/Android/DragonBound-Greybox.apk"
```
**若走 B-ADB：装完务必再确认一次反向前映射仍在**：
```bash
adb reverse --list
```
（`adb install` 一般不影响，但重插过线就要重跑。）

---

## 7. Step 6 — 验证与判定

清日志、启动、抓 Unity 输出：

**Git Bash**
```bash
adb logcat -c && adb logcat -s Unity:V | grep -iE "API |NETWORK_ERROR|Cleartext|Exception"
```
**CMD**
```bat
adb logcat -c && adb logcat -s Unity:V | findstr /i "API NETWORK_ERROR Cleartext Exception"
```

### 判定表

| 现象 | 含义 | 处理 |
|---|---|---|
| `[API] POST /v1/...` 后接 `[API] 200 ...` | ✅ 链路通 | 进入游戏验证登录/对局 |
| 无任何 `[API]` 日志 + 服务层未初始化报错 | `apiBaseUrl` 为空或非法 | 回到 Step 3 |
| `[API] 0 POST ...` + 系统日志 `Cleartext HTTP traffic to ... not permitted` | ❌ 明文未放行 | 回到 Step 2 / 3.3 |
| `[API] 0 POST ...`，系统日志无 Cleartext 字样 | ❌ 路由不可达 | 回到 Step 1（浏览器验证那一步） |
| `NETWORK_ERROR` 且浏览器能打开 | 多半是明文，或 `adb reverse` 失效 | 查 3.3 / `adb reverse --list` |
| HTTP 4xx / 5xx 有响应 | ✅ 网络已通，属业务/服务端问题 | 见 §8 风险 |

---

## 8. 已知连带风险（网络打通后会遇到，提前知道不慌）

1. **`409 Settlement conflicts with run state`**
   `requireAuthoritativeRunContract: 1` 时，客户端结算会与服务端局状态对账；此前「WAVE 1 直接 Victory」已触发过该 409。这是服务端的波数/状态校验规则问题，属独立议题。

2. **`expectedStageSnapshotDigest` 不匹配**
   `backendMode=1` 时该值（当前 `a555903b...d05eb`）必须与服务端该 stage 的快照摘要一致，否则随机流/Stage 契约校验会失败。切服务端版本后要同步更新。

3. **商人面板"两次对局后自动弹出"在 GoUnary 下不生效**
   这是**已知未修问题**：`GoUnaryGameplayRunGateway` 硬编码 `CountCompletedRun=false`，`GameSettlementCoordinator` 永不调用 `Merchant.RecordCompletedRunAsync`，因此 `MainMerchantController` 的 `TryConsumePending` 恒为 false。切到 GoUnary 后若发现该面板不弹，属预期，不是网络问题。

4. **`ClientServiceCompositionValidation`（菜单 `DragonBound/Validation/Validate Client Services`）会失败**
   它硬断言 `BackendMode == Local`（`Assets/Editor/ClientServiceCompositionValidation.cs:15`）。**联调期跑它必然报 "Development backend must remain Local."**，属预期，不要据此判断配置错误。

---

## 9. Step 7 — 收尾（上线前必须回退）

联调结束后逐项还原，**任何一项遗漏都会带上线**：

| 项 | 动作 |
|---|---|
| 明文放行 | **删除** `Assets/Plugins/Android/AndroidManifest.xml`（并取消 Publishing Settings 里的 Custom Main Manifest 勾选）；防火墙规则一并删除 |
| `apiBaseUrl` | 改为正式 `https://` 域名 |
| `enableNetworkLogging` | 改回 `0` |
| `backendMode` | 正式包保持 `1`，但地址必须是线上域名 |
| 构建期校验（建议新增） | 在 `DragonBoundAndroidBuild.BuildApk()` 开头加断言：非 Development Build 时禁止 `apiBaseUrl` 为 `http://` 或 `192.168.`/`127.0.0.1`，一条断言即可永久杜绝"开发配置误打到真包" |

---

## 附录：最小执行清单（照着敲）

**B-ADB（推荐；本机实测前提已核实，可直接照做）**

> **本机实测（2026-09-10 16:5x）**：
> - `adb` **不在 PATH**，用 Unity 自带的一份即可：`D:\2022.3.62f3c1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe`（1.0.41 / 32.0.0）。
> - 已检测到设备 `9B051FFAZ00F5M`，但状态为 **`unauthorized`** → 「第 0 步」先解决授权，否则所有 `adb reverse / install` 都会报 `device unauthorized`。
> - 复核时 **8081 无进程监听**（后端未启动），**第 1 步必须先起后端**。
> - `ClientServiceConfig.asset` 当前为 `backendMode: 1` + `apiBaseUrl: http://192.168.31.221:8081`。

0. **解决 USB 调试授权（新增前置）**：手机插上后，在手机上确认「允许 USB 调试」弹窗（勾选「始终允许」）。若没弹：
   ```bash
   adb kill-server && adb start-server && adb devices
   ```
   直到状态从 `unauthorized` 变为 `device`。仍为 `unauthorized` → 手机「开发者选项」里撤销 USB 调试授权后重插。
1. **后端**：在服务端机器确认 Go 服务监听 `0.0.0.0:8081`（复核时未启动，先跑起来）。
   ```bash
   netstat -ano | findstr :8081      # 期望看到 0.0.0.0:8081  LISTENING
   ```
2. **建立反向映射**（PC 执行）：
   ```bash
   adb devices                       # 必须为 device，不是 unauthorized/offline
   adb reverse tcp:8081 tcp:8081     # 手机 8081 → PC 127.0.0.1:8081
   adb reverse --list                # 期望输出 … tcp:8081 tcp:8081
   ```
3. **工程 → 新建明文放行 manifest**：`Assets/Plugins/Android/AndroidManifest.xml`，内容照抄 §3.1（**不要写 `package`**）。
4. **工程 → Player Settings**：`Publishing Settings` 里确认 `Custom Main Manifest` 已勾选（§3.2）。
5. **工程 → 改配置**：编辑 `Assets/Resources/Configuration/ClientServiceConfig.asset`：
   ```yaml
   backendMode: 1
   apiBaseUrl: http://127.0.0.1:8081     # ← B-ADB 固定写 127.0.0.1
   enableNetworkLogging: 1               # ← 联调期打开
   ```
   > 该文件在 `Resources/` 下**打进包**，改完必须重新构建 APK。
6. **构建**：Unity `File → Build Settings → Android → Build`，或命令行 `-executeMethod DragonBound.Editor.DragonBoundAndroidBuild.BuildApk`（§6）。
7. **验证明文生效**：构建后查 `Library/Bee/Android/Prj/IL2CPP/Gradle/launcher/build/intermediates/merged_manifest/debug/AndroidManifest.xml`，`<application>` 上须出现 `usesCleartextTraffic="true"`（§3.3）。
8. **安装 + 观察**：`adb install -r "Builds/Android/DragonBound-Greybox.apk"` → `adb reverse --list` 复核映射仍在 → `adb logcat -s Unity:V`，看到 `[API] POST ... 200` 即链路打通（判定表见 §7）。
9. **收尾**：删除 `Assets/Plugins/Android/AndroidManifest.xml`（并取消勾选 Custom Main Manifest）；`enableNetworkLogging` 改回 `0`；`apiBaseUrl` 换回正式 HTTPS 域名（§9）。

**B-ADB 与 B-LAN 的唯一分叉**：`apiBaseUrl` 写法（`127.0.0.1` vs PC 局域网 IP）+ 是否需要 `adb reverse`（B-LAN 用 Wi-Fi 直连，不做反向映射）。**第 3 步起的工程改动两者完全相同。**

**B-LAN（脱离 USB；前提已核实，可直接照做）**

> 已核实：PC IP = `192.168.31.34`；8081 入站防火墙**已放行**（无需新建规则）；当前网络配置文件 = 「公用」。
> 与 B-ADB 的唯一差异集中在 **第 1–2 步**（不再用 `adb reverse`）；**第 3 步起的工程改动与 B-ADB 完全相同**。

1. **后端**：在服务端机器确认 Go 服务监听 `0.0.0.0:8081`（复核时未启动，先跑起来）。
   ```
   netstat -ano | findstr :8081     # 期望看到 0.0.0.0:8081  LISTENING
   ```
2. **路由**：手机与 PC 连**同一 Wi-Fi**（同一 `192.168.31.x` 网段）；手机浏览器打开 `http://192.168.31.34:8081/`，**必须有响应**才继续。防火墙本次无需改动（§2.3）。
3. **工程 → 新建明文放行 manifest**：`Assets/Plugins/Android/AndroidManifest.xml`，内容照抄 §3.1。
4. **工程 → Player Settings**：`Publishing Settings` 里确认 `Custom Main Manifest` 已勾选（§3.2）。
5. **工程 → 改配置**：编辑 `Assets/Resources/Configuration/ClientServiceConfig.asset`：
   ```yaml
   backendMode: 1
   apiBaseUrl: http://192.168.31.34:8081     # ← 本次实测 IP（原 192.168.31.221 已失效）
   enableNetworkLogging: 1
   ```
6. **构建**：Unity `File → Build Settings → Android → Build`，或命令行 `-executeMethod DragonBound.Editor.DragonBoundAndroidBuild.BuildApk`（§6）。
7. **验证明文生效**：构建后查 `Library/Bee/Android/Prj/IL2CPP/Gradle/launcher/build/intermediates/merged_manifest/debug/AndroidManifest.xml`，`<application>` 上须出现 `usesCleartextTraffic="true"`（§3.3）。
8. **安装 + 观察**：`adb install -r "Builds/Android/DragonBound-Greybox.apk"` → `adb logcat -s Unity:V`，看到 `[API] POST ... 200` 即链路打通（判定表见 §7）。
9. **收尾**：删除 `Assets/Plugins/Android/AndroidManifest.xml`（并取消勾选 Custom Main Manifest）；`enableNetworkLogging` 改回 `0`；`apiBaseUrl` 换回正式 HTTPS 域名；若自建过防火墙规则一并删除（§9）。

---

## 附录二：B-LAN 与 B-ADB 的差异一览

| 维度 | B-ADB | B-LAN |
|---|---|---|
| `apiBaseUrl` | `http://127.0.0.1:8081` | `http://192.168.31.34:8081`（随 PC IP 变） |
| 手机↔PC 连接 | USB 数据线 | 同一 Wi-Fi |
| 额外 PC 动作 | `adb reverse tcp:8081 tcp:8081`（**每次插拔/重启都要重跑**） | 无（防火墙已放行） |
| 服务端监听要求 | 可只监听 `127.0.0.1` | **必须监听 `0.0.0.0`** |
| 房间/网段要求 | 无 | 同网段、关 AP 隔离 |
| 明文 manifest | **必须** | **必须** |
| 稳定性 | 高（不受网络环境影响） | 受 DHCP IP 变化影响，建议静态 IP |
| 适用 | 日常联调 | 手机需自由走动 / 不便接线 |
