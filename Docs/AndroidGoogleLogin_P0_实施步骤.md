# Android Google Login 接入 — 第一阶段（P0：Android Google 身份获取）实施步骤

> 性质：**只出方案，不改代码**。
> 输入：图1（Google Login 配置）、图2（接入要求）、图3（项目现状）、图4（第一阶段修改内容）。
> 已逐条到本仓库核实代码/配置现状（核查时间 2026-09-14，分支 `codex/ui-v1-v2-isolation`）。

---

## 一、结论速览

| 项 | 结论 |
| --- | --- |
| P0 唯一缺口 | 线上模式（`BackendMode.GoUnary`）注入的是 `UnavailableGoogleOAuthProvider`，点 Google 按钮必抛 `GOOGLE_PROVIDER_NOT_CONFIGURED` |
| P0 改动面 | 新增 3 个文件 + 1 个 EDM 依赖 XML + 1 个配置字段 + `GoUnaryServiceModule.cs` 1 处替换 |
| 明确不动 | `IGoogleOAuthProvider` 契约、`/v1/auth/*` 三个网关实现、Login 场景 UI 层级、Local 模式与 Guest 登录 |
| 需要后台配合 | Google Cloud/Firebase 里补 Android OAuth 客户端（包名 + SHA-1），重下 `google-services.json` |
| 不在 P0 | Guest→Google 绑定入口、启动优先 refresh、Token 安全存储、错误态全覆盖（P1/P2，见 §六） |
| 关键架构约束 | **Java 源码必须进 `unityLibrary` 模块**；EDM4U 1.2.188 不会把依赖注入 `*.androidlib`（已从程序集核实），放错位置会编译期找不到 androidx.credentials |

---

## 二、已核实的现状基线

### 2.1 代码侧

| 位置 | 现状 | 说明 |
| --- | --- | --- |
| `Assets/Scripts/Services/Composition/GoUnaryServiceModule.cs:22` | `var googleOAuth = new UnavailableGoogleOAuthProvider();` | **图4 指的「当前需要替换的位置」**。全工程仅此一处线上注入 |
| `Assets/Scripts/Auth/Infrastructure/UnavailableGoogleOAuthProvider.cs:8-21` | fail-closed 占位，`SignInAsync` 直接返回 `Task.FromException(new AuthException("GOOGLE_PROVIDER_NOT_CONFIGURED", ...))` | 与图3「线上注入的是不可用占位实现」完全对应 |
| `Assets/Scripts/Auth/Contracts/IGoogleOAuthProvider.cs:8-24` | `Task<PendingGoogleIdentity> SignInAsync(CancellationToken)` + `void CancelPendingSignIn()`；`PendingGoogleIdentity` 已含 `Subject/Email/EmailVerified/PictureUrl/IdToken/AvatarSprite/OwnsAvatarSprite` | **契约已够用，P0 不需要改契约** |
| `Assets/Scripts/Services/Composition/LocalServiceModule.cs:8` | `new MockGoogleOAuthProvider()` | 仅 Local 模式；P0 不动 |
| `Assets/Scripts/Auth/UI/LoginController.cs:247` | `pendingGoogleIdentity = await googleOAuthProvider.SignInAsync(...)` | 身份获取调用点，P0 后自动走真实现 |
| `LoginController.cs:248-255` | 校验 `IdToken` 非空、`Email` 非空、`EmailVerified == true`，否则抛 `INVALID_CREDENTIALS` | ⚠️ **P0 必须把 Email / EmailVerified 填对**，否则登录会被自己判失败（见步骤 3.3） |
| `LoginController.cs:292` | `authGateway.GoogleLoginAsync(idToken, deviceInfo, ...)` → `POST /v1/auth/google` | 已实现 |
| `Assets/Scripts/Auth/Infrastructure/GoAuthGateway.cs:42-65 / 67-87` | `/v1/auth/google`（匿名 transport）、`/v1/auth/link/google`（带鉴权 transport）已实现 | link 接口**全工程无调用方**（图3「未完成」属实） |
| `Assets/Scripts/Services/Transport/RefreshingUnaryTransport.cs:31-51` | 仅 **401 被动刷新**，`SemaphoreSlim` 串行 + 同 token 去重 | 对应图3「当前只是接口遇到 401 后被动刷新」 |
| `Assets/Scripts/Auth/Session/PersistentAuthSessionStore.cs:89-91` | `PlayerPrefs.SetString` 明文存 `player_id/access_token/refresh_token` | 对应图3「已保存，但存储不安全」 |
| 全工程 | **零 `AndroidJavaClass` / JNI 用法**（grep 无命中） | P0 是本项目**第一处**原生桥接，需自成体系 |

### 2.2 构建/依赖侧

| 位置 | 现状 |
| --- | --- |
| `Assets/ExternalDependencyManager/Editor/1.2.188/` | EDM4U **1.2.188** 已就位 |
| `Assets/Firebase/Editor/AppDependencies.xml:14-26` | 现有 Android 依赖：`play-services-base:18.10.0`、`firebase-common:22.1.0`、`firebase-analytics:23.2.0`、`firebase-app-unity:13.14.0` |
| `Assets/Plugins/Android/mainTemplate.gradle:6-12` | EDM 已注入 `// Android Resolver Dependencies Start/End` 块；**无** androidx.credentials / googleid |
| `ProjectSettings/AndroidResolverDependencies.xml:17` | `bundleId = com.drakeforge.mergedefense` ✅ 与图1 一致 |
| `ProjectSettings/ProjectSettings.asset:166-167` | `applicationIdentifier.Android = com.drakeforge.mergedefense` ✅ |
| `ProjectSettings.asset:176-177,268` | `minSdk 24` / `targetSdk 0(auto)` / `ARM64 only` |
| `ProjectSettings.asset:262,265,266` | ⚠️ `useCustomMainGradleTemplate=0`、`useCustomGradlePropertiesTemplate=0`、`useCustomGradleSettingsTemplate=0`，但 `Assets/Plugins/Android/` 下三个模板文件都存在 → **状态矛盾，必须先人工确认**（见步骤 0） |
| `Assets/Plugins/Android/` | 已有 3 个 `*.androidlib`（`FirebaseApp` / `DragonBoundNetwork` / `DragonBoundAnalytics`），都是**纯清单式**（只有 `AndroidManifest.xml` + `project.properties[+res/]`），无 `build.gradle`、无 Java 源码 |
| `Assets/Plugins/Android/FirebaseApp.androidlib.meta:14-18` | `.androidlib` 的 PluginImporter 仅对 `Android: enabled 1`、`Editor: enabled 0` |
| `Assets/google-services.json:12-15` | `package_name = com.drakeforge.mergedefense` ✅；`oauth_client: []` ⚠️ 与图3 一致 |
| `Assets/Resources/Configuration/ClientServiceConfig.asset` | `backendMode: 1`（= `GoUnary`）→ **线上路径就是 `UnavailableGoogleOAuthProvider`** |

### 2.3 图1 参数落位

| 参数 | 值 | 在代码里的角色 |
| --- | --- | --- |
| Package | `com.drakeforge.mergedefense` | 已一致 ✅ |
| Web Client ID | `82952977839-ui220r26h7t4cpepl9cq7r4q3vkoqf1d.apps.googleusercontent.com` | **作为 `serverClientId` 传给 `GetGoogleIdOption`**，配置化落位见步骤 4 |
| Android Client ID | `82952977839-15c37858or2t5q4csjsp37k3tmfaqmtt.apps.googleusercontent.com` | **代码里不引用**，只用于 Google Cloud 的 Android 端身份登记（包名 + SHA-1） |

---

## 三、实施步骤

### 步骤 0：前置确认（不做这步，后面大概率白做）

1. **模板勾选项对齐**：Unity → Project Settings → Player → Android → Publishing Settings，确认这三个勾选为**开启**（当前 ProjectSettings 读到 0，但文件已存在）：
   - Custom Main Gradle Template（`Assets/Plugins/Android/mainTemplate.gradle`）
   - Custom Gradle Properties Template（`gradleTemplate.properties`）
   - Custom Gradle Settings Template（`settingsTemplate.gradle`）
   若为关闭：勾上让 Unity 重新序列化，否则 EDM 注入的依赖会丢、`*.androidlib` 不会进 `settings.gradle` 的 `include`。
2. **设备要求**：P0 走 Credential Manager 的 Play Services 实现（`credentials-play-services-auth`），测试机/模拟器必须有 **Google Play 服务且可更新**；纯 AOSP 设备会直接失败（属预期，错误态见步骤 6）。
3. **签名指纹登记**：把 Android 构建用的 SHA-1（此前已取到 debug 指纹 `43:D3:90:9C:...`）登记到 Google Cloud 项目 `drakeforge`（project_number `82952977839`）的 Android OAuth 客户端，包名 `com.drakeforge.mergedefense`。Release 包需再登记 release 指纹。
4. **编译版本底线**：`androidx.credentials:1.6.0` 要求 `compileSdk ≥ 34`。本项目 `targetSdk 0(auto)`，Unity 2022.3.62 默认 compileSdk 35 → 满足；若后续锁死低版本会报 AAR metadata 校验失败。

### 步骤 1：新增 Android 依赖（走 EDM XML，不写死进生成的 Gradle 文件）

新增文件：`Assets/Plugins/Android/DragonBoundGoogleAuthDependencies.xml`

> 命名必须匹配 `*Dependencies.xml` —— EDM4U 1.2.188 的 README §Integrate into mainTemplate.gradle 明确写「Collect the set of Android dependencies specified by a project's `*Dependencies.xml` files」，它**只**按此模式收集。

```xml
<dependencies>
  <androidPackages>
    <androidPackage spec="androidx.credentials:credentials:1.6.0" />
    <androidPackage spec="androidx.credentials:credentials-play-services-auth:1.6.0" />
    <androidPackage spec="com.google.android.libraries.identity.googleid:googleid:1.1.0" />
  </androidPackages>
</dependencies>
```

版本选择依据（2026-09 官方 release notes）：
- `androidx.credentials` 最新 **稳定版 1.6.0**（2026-04-08）；`1.7.0-alpha03` 为 alpha，不上生产。
- `credentials` 与 `credentials-play-services-auth` **必须同版本号**。
- `googleid` 当前稳定为 `1.1.0`（提供 `GoogleIdTokenCredential`）。
- 若之后升级：以 `Assets > External Dependency Manager > Android Resolver > Force Resolve` 解析通过为准。

执行与验收：
1. 菜单 `Assets → External Dependency Manager → Android Resolver → Force Resolve`。
2. 验收 A：`Assets/Plugins/Android/mainTemplate.gradle` 的 `// Android Resolver Dependencies Start/End` 块内出现上述 3 行 `implementation '...'` 注释着本 XML 的行号。
3. 验收 B：`ProjectSettings/AndroidResolverDependencies.xml` 的 `<packages>` 内出现这 3 个坐标。

⚠️ **本方案最重要的架构约束**：EDM 只把依赖注入 `mainTemplate.gradle`（= Gradle 的 `unityLibrary` 模块），**不处理 `*.androidlib`**（已从 `Google.JarResolver.dll` 中核实：注入正则只有 `.*\*\*DEPS\*\*.*` 与 `.*apply plugin: 'com\.android\.(application|library)'.*`）。因此 Java 源码必须落在**同一个模块**里 → 即 `Assets/Plugins/Android/` 下、由 Unity 编入 `unityLibrary`，**不要**自建 `*.androidlib` 子模块来放源码。

### 步骤 2：新增 Android 原生桥接（Java）

**目录必须镜像 Java 包路径**（Gradle 的 `src/main/java` 源集按包路径找文件，Unity 会把 `Assets/Plugins/Android/**` 的相对路径原样拷进源集）：

```
Assets/Plugins/Android/com/drakeforge/mergedefense/googleauth/GoogleSignInBridge.java
Assets/Plugins/Android/com/drakeforge/mergedefense/googleauth/GoogleSignInBridge.java.meta
Assets/Plugins/Android/com/drakeforge/mergedefense/googleauth/GoogleSignInResult.java        ← 可选：结果 DTO
```

用 **Java 而非 Kotlin**（Kotlin 需额外引入 Kotlin Gradle 插件，收益为零）。

类骨架与要点：

```java
package com.drakeforge.mergedefense.googleauth;

public final class GoogleSignInBridge {
    private static final String TAG = "DragonBoundGoogleAuth";
    private static CancellationSignal pending;       // 取消用
    private static int attemptToken;                 // 迟到回调丢弃用

    // 由 C# 通过 AndroidJavaClass 静态调用
    public static void signIn(String callbackObject, String callbackMethod,
                              String serverClientId, boolean filterByAuthorizedAccounts) { ... }

    public static void cancel() { ... }              // pending.cancel(); attemptToken++;
}
```

`signIn` 内部实现要点：

1. `Activity activity = com.unity3d.player.UnityPlayer.currentActivity;`
2. `CredentialManager manager = CredentialManager.create(activity);`
3. 组装请求：
   - `new GetGoogleIdOption.Builder().setServerClientId(serverClientId).setFilterByAuthorizedAccounts(filterByAuthorizedAccounts).setAutoSelectEnabled(filterByAuthorizedAccounts).build()`
   - `new GetCredentialRequest.Builder().addCredentialOption(googleIdOption).build()`
4. `pending = new CancellationSignal();`
   `manager.getCredentialAsync(activity, request, pending, ContextCompat.getMainExecutor(activity), callback)` → **用主线程 Executor，回调即主线程，C# 侧无需再 marshal**。
5. 结果解析：
   - `Credential c = response.getCredential();`
   - 判断 `c instanceof CustomCredential && GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(c.getType())`
   - `GoogleIdTokenCredential g = GoogleIdTokenCredential.createFrom(c.getData());`
   - 取 `g.getIdToken()`（→ `IdToken`）、`g.getId()`（→ `Subject`）、`g.getDisplayName()`、`g.getProfilePictureUri()`；**其它 credential type 一律忽略**。
6. **两次查询策略（严格按图2）**：
   - 第 1 次：`filterByAuthorizedAccounts = true`（只查已授权账号，静默优先），`autoSelectEnabled = true`。
   - 捕获 `GetCredentialException.TYPE_NO_CREDENTIAL` → 第 2 次：`filterByAuthorizedAccounts = false` 再查全部账号（**此时必须 `autoSelectEnabled = false`**，否则框架会抛参数异常）。
7. **错误映射（`errorType` 字段）**：

   | Java 异常 | errorType | 语义 |
   | --- | --- | --- |
   | `GetCredentialException.TYPE_USER_CANCELED` | `USER_CANCELED` | 用户取消（不弹提示） |
   | `GetCredentialException.TYPE_NO_CREDENTIAL` | `NO_CREDENTIAL` | 设备上无可用 Google 账号 |
   | `GetCredentialException.TYPE_INTERRUPTED` | `INTERRUPTED` | 被其它凭据请求打断，可重试 |
   | `GetCredentialException.TYPE_UNKNOWN` | `UNKNOWN` | 未知 |
   | `GoogleIdTokenParsingException` | `TOKEN_PARSE_FAILED` | `createFrom` 解析失败 |
   | `ApiException`（Play Services） | `PLAY_SERVICES_ERROR` | 带 `statusCode`，常见为版本过低/需要重新授权 |
   | `ActivityNotFoundException` / `FeatureNotAvailableException` | `PROVIDER_UNAVAILABLE` | 无 Play 服务 |
   | 其它 `Throwable` | `INTERNAL` | 兜底 |

8. **回传格式**（`UnityPlayer.UnitySendMessage(callbackObject, callbackMethod, json)`，单参数、必须 string）：

```json
{"requestId":"...","status":"ok","idToken":"...","subject":"...",
 "displayName":"...","pictureUrl":"...","errorType":"","errorCode":0,"message":""}
```

9. **安全约束（图2 硬要求）**：`idToken` 只经内存与 Unity 回调传递 → **不得写日志、不得落 PlayerPrefs/文件、不得塞进 Analytics 事件**；登录成功后由 C# 侧立即丢弃。

### 步骤 3：Unity 侧封装（两个新文件）

统一放 `Assets/Scripts/Auth/Infrastructure/`（Assembly-CSharp，与 Auth 现有代码同层，无 asmdef 需处理）。

#### 3.1 `AndroidGoogleSignInBridge.cs` —— JNI + Task 适配

- 整文件包 `#if UNITY_ANDROID && !UNITY_EDITOR`，避免其它平台编译到 JNI 类型。
- 单例：`DontDestroyOnLoad` 的 GameObject，名字固定（`UnitySendMessage` 用名字寻址），例如 `AndroidGoogleSignInBridge`；**不要在 composition root（`BeforeSceneLoad`）里 new**，用懒加载 `Instance`。
- 对外 API：
  - `Task<GoogleSignInResult> SignInAsync(string serverClientId, CancellationToken ct)`
  - `void Cancel()`
- 内部：
  - `TaskCompletionSource<GoogleSignInResult>` + `string requestId = Guid.NewGuid().ToString("N")`。
  - 调 `new AndroidJavaClass("com.drakeforge.mergedefense.googleauth.GoogleSignInBridge").CallStatic("signIn", gameObject.name, nameof(OnGoogleSignInResult), serverClientId, filterByAuthorizedAccounts)`。
  - `void OnGoogleSignInResult(string json)`：`JsonUtility.FromJson<GoogleSignInResultDto>` → **校验 `requestId` 匹配且 TCS 未完成**，否则直接丢弃（场景销毁/迟到回调/重复回调）。
  - **防重复点击**：`if (inFlight) { 直接以 INTERRUPTED/IN_PROGRESS 失败返回，不发起第二次原生登录 }`。
  - **取消**：`ct.Register(() => { CallStatic("cancel"); tcs.TrySetCanceled(); })`。
  - 第 2 次（全量账号）查询在 C# 侧驱动：拿到 `NO_CREDENTIAL` 后用 `filterByAuthorizedAccounts=false` 再调一次 `signIn`，C# 侧对外仍是**一次** `SignInAsync`（这样 Login 场景的 `requestInProgress` 语义不变）。
- 回收：`OnDestroy` 里 `TrySetCanceled` + 调一次 `cancel()`。

#### 3.2 `AndroidGoogleOAuthProvider.cs` —— 实现 `IGoogleOAuthProvider`

```csharp
public sealed class AndroidGoogleOAuthProvider : IGoogleOAuthProvider
{
    private readonly string serverClientId;   // = Web Client ID
    public AndroidGoogleOAuthProvider(string serverClientId) { ... }
    public async Task<PendingGoogleIdentity> SignInAsync(CancellationToken ct) { ... }
    public void CancelPendingSignIn() { ... }
}
```

- `SignInAsync` → `bridge.SignInAsync(serverClientId, ct)` → 成功则构造 `PendingGoogleIdentity`。
- 失败按 `errorType` 抛 `AuthException(code, message)`（码表见 §步骤 6）。
- `AvatarSprite = null`、`OwnsAvatarSprite = false`：图1 路线只给 `profilePictureUri`；`LoginController.cs:258-261` 已有 `defaultGoogleAvatarSprite` 兜底，**头像下载不属于 P0 阻塞项**。

#### 3.3 ⚠️ 必做的字段补齐：从 ID Token 解 JWT payload

`GoogleIdTokenCredential` **不暴露 email**，而 `LoginController.cs:248-251` 硬性要求 `Email` 非空且 `EmailVerified == true`。因此必须：

- 对 `IdToken` 做 **Base64Url 解码中段 payload**（不校验签名——签名由服务端校验），取 `email`、`email_verified`、`sub`、`picture`。
- 映射：`Subject = sub ?? g.getId()`、`Email = email`、`EmailVerified = email_verified`、`PictureUrl = picture ?? profilePictureUri`。
- 实现位置建议放 `GoogleIdTokenPayload.cs`（纯静态解析，可单测），解析失败 → `AuthException("TOKEN_PARSE_FAILED", ...)`。
- 这一步不做的话，真机拿到 token 也会被客户端自己判成 `INVALID_CREDENTIALS`——**是本阶段最容易踩的坑**。

### 步骤 4：`serverClientId` 配置化（不硬编码）

1. `Assets/Scripts/Services/Configuration/ClientServiceConfig.cs`：新增
   ```csharp
   [SerializeField] private string googleServerClientId = string.Empty;
   public string GoogleServerClientId => googleServerClientId;
   ```
2. `Assets/Resources/Configuration/ClientServiceConfig.asset`：填 `googleServerClientId: 82952977839-ui220r26h7t4cpepl9cq7r4q3vkoqf1d.apps.googleusercontent.com`（**Web** Client ID；Android Client ID 不填这里）。
3. （建议）`GoUnaryServiceModule.Validate` 增加强校验：`BackendMode.GoUnary` 时 `GoogleServerClientId` 不得为空 → 漏配在启动期就暴露，而不是点按钮才报错。
4. 用途：该字段同时供步骤 5 构造 Provider、以及 `GoogleSignInBridge` 的 `setServerClientId` 使用 —— 与图1「Credential Manager 使用 Web Client ID 作为 serverClientId」一致。

### 步骤 5：替换注入点（图4 的「当前需要替换的位置」）

`Assets/Scripts/Services/Composition/GoUnaryServiceModule.cs`

```csharp
// 第 22 行
- var googleOAuth = new UnavailableGoogleOAuthProvider();
+ IGoogleOAuthProvider googleOAuth = CreateGoogleOAuthProvider(config);

// 文件末尾（Validate 之后）新增
+ private static IGoogleOAuthProvider CreateGoogleOAuthProvider(ClientServiceConfig config)
+ {
+ #if UNITY_ANDROID && !UNITY_EDITOR
+     return new AndroidGoogleOAuthProvider(config.GoogleServerClientId);
+ #else
+     return new UnavailableGoogleOAuthProvider();
+ #endif
+ }
```

- 保留非 Android / Editor 的 `UnavailableGoogleOAuthProvider`（图4 第 3 条「Editor 和非 Android 平台继续使用不可用实现」）。
- 返回值类型写成 `IGoogleOAuthProvider`（原来是 `var`），`ClientServices` 第 4 个参数（`googleOAuth`）无需改动 —— `IClientServices.GoogleOAuth` 已是接口类型。
- Local 模式的 `MockGoogleOAuthProvider`（`LocalServiceModule.cs:8`）不动。

### 步骤 6：错误态文案（P0 最小可用版）

现状是 `LoginController` 三处 catch 直接 `ShowMessage(exception.Message)`（`:224/:270/:307`），Provider 抛什么就显示什么，且 `AuthException` 只有 `Code + Message`（`AuthException.cs:3-11`）。

最小改动：

1. 新增 `Assets/Scripts/Auth/Contracts/AuthErrorMessages.cs`：`public static string Resolve(AuthException e)`，按 `e.Code` 出用户文案。
2. `LoginController` 三处 `ShowMessage(exception.Message)` → `ShowMessage(AuthErrorMessages.Resolve(exception))`。
3. 约定 **`USER_CANCELED` 不弹提示**（静默还原 UI，等同用户主动放弃）。
4. 码表：

| Code | 触发 | 建议文案（英文，与现有 UI 一致） |
| --- | --- | --- |
| `USER_CANCELED` | 用户在账号选择里返回 | 不提示 |
| `NO_CREDENTIAL` | 设备无 Google 账号 | `No Google account found on this device.` |
| `PROVIDER_UNAVAILABLE` | 无/过旧 Play 服务 | `Google Play Services is unavailable or out of date.` |
| `PLAY_SERVICES_ERROR` | `ApiException` | `Google sign-in is temporarily unavailable. Please try again.` |
| `INTERRUPTED` / `IN_PROGRESS` | 并发/重复点击 | `Sign-in is already in progress.` |
| `TOKEN_PARSE_FAILED` / `INVALID_CREDENTIALS` | token 不可用或被服务端拒 | `Google sign-in failed. Please try again.` |
| `NETWORK_UNAVAILABLE` / 5xx | 网络失败 | `Network unavailable. Check your connection.` |
| `ACCOUNT_CONFLICT` | 该 Google 账号已绑定其它 PlayerId | `This Google account is already linked to another player.`（**服务端错误码待确认，见 §七**） |
| `SESSION_EXPIRED` | refresh 失败 / 会话失效 | `Session expired. Please sign in again.` |
| 兜底 | 其它 | `Google sign-in failed. Please try again.` |

> 说明：`LoginController` 里已有 `catch (OperationCanceledException)` 分支（静默），`USER_CANCELED` 建议**不要**走异常路径，而是 Provider 返回 `null` 或抛 `OperationCanceledException`，直接复用现有静默分支，改动更小。

### 步骤 7（配套，非代码）：`google-services.json` 补 `oauth_client`

图3 标注「`google-services.json` 中没有 `oauth_client` 条目 → 配置不完整」。当前文件 `oauth_client: []`。

- 这是**控制台 + 重新下载**的活：在 Google Cloud（项目 `drakeforge`）为包名 `com.drakeforge.mergedefense` + 已登记 SHA-1 创建 Android OAuth 客户端后，重新下载 `google-services.json` 覆盖 `Assets/google-services.json`。
- 影响面：Credential Manager 的 `GetGoogleIdOption` 只依赖运行时传入的 `serverClientId`，**不读** `google-services.json` 的 `oauth_client`；该条目真正影响的是 Firebase Auth 的 Google 登录与 Google 侧身份校验链。所以它**不是 P0 的代码阻塞项**，但图3 明确列为配置不完整项，建议同期补齐。
- 补充：`Assets/Plugins/Android/FirebaseApp.androidlib/res/values/google-services.xml` 是从该 json 派生的资源，覆盖 json 后需重新触发 Firebase 的导入流程。

---

## 四、P0 验收清单

| # | 验收点 | 判据 |
| --- | --- | --- |
| 1 | 依赖解析 | `mainTemplate.gradle` 出现 3 行新 `implementation`；`AndroidResolverDependencies.xml` 同步 |
| 2 | 真机登录 | Android 真机（有 Play 服务）点 Google → 弹账号选择 → 确认 → `POST /v1/auth/google` 返回 200/201 → 进 Main |
| 3 | 二次查询 | 首次只列已授权账号；无结果时自动再弹全量账号列表 |
| 4 | 占位不再生效 | 日志无 `GOOGLE_PROVIDER_NOT_CONFIGURED` |
| 5 | **字段校验通过** | 不出现客户端自造 `INVALID_CREDENTIALS`；`Email`/`EmailVerified` 由 JWT payload 正确填充 |
| 6 | 取消 | 账号选择返回 → 无错误提示、按钮恢复可点、可再次发起 |
| 7 | 防重复 | 连点 Google 按钮只产生一次原生登录 |
| 8 | 迟到回调 | 登录中切场景/退到后台再回来 → 无空引用、无报错、UI 状态正确 |
| 9 | fail-closed 未被破坏 | Editor / Windows Standalone 点 Google 仍返回 `GOOGLE_PROVIDER_NOT_CONFIGURED` |
| 10 | 包名/指纹 | APK 包名 `com.drakeforge.mergedefense`；签名 SHA-1 与 Google Cloud 登记一致 |
| 11 | Token 不外泄 | `PlayerPrefs`、`Logs/*.log`、Analytics 事件里搜不到 Google `id_token` |

---

## 五、风险与影响面

| 风险 | 说明 | 缓解 |
| --- | --- | --- |
| 源码放错模块 | 把 Java 放进自建 `*.androidlib` → 编译期找不到 `androidx.credentials`（EDM 不注入 androidlib） | 按步骤 2 放在 `Assets/Plugins/Android/com/...` 镜像包路径 |
| 源集路径不匹配 | 文件相对路径与 `package` 不一致 → Gradle `package does not exist` | 目录严格镜像包名；**备选方案 B**：改用 `DragonBoundGoogleAuth.androidlib` + 自带 `build.gradle` 写死依赖（代价：版本不再集中在 EDM XML，与图4 建议冲突） |
| 模板勾选状态矛盾 | `useCustom*Template` 全 0 但文件已存在 | 步骤 0 第 1 条先对齐 |
| IL2CPP 字符串/JNI | 本项目首次引入 JNI；`UnitySendMessage` 必须单 string 参数、目标对象名必须匹配且已激活 | 步骤 3.1 固定对象名 + 单参回调；避免在 BeforeSceneLoad 创建 |
| 线程 | Credential Manager 回调线程不定 | 统一 `ContextCompat.getMainExecutor(activity)` |
| Play 服务差异 | 设备/模拟器版本旧 → `ApiException` | 步骤 6 文案兜底；测试机固定一台真机 |
| R8/ProGuard | Release 开 minify 时可能裁掉 googleid 反射路径 | 目前 Debug 未开；Release 前补 keep 规则 `-keep class com.google.android.libraries.identity.googleid.**` |
| 体积 | 新增 3 个 AAR（数百 KB 量级）+ jetifier 处理 | 可接受；注意 `useJetifier=True` 已开（`AndroidResolverDependencies.xml:30`） |
| 波及面 | 仅 GoUnary 模式 Google 登录路径 | Local 模式、Guest 登录、其它 gateway、Login 场景 UI 均不改动 |

---

## 六、明确不在 P0 范围（图3 其余未完成项 → 建议 P1/P2）

| 图3 条目 | 现状核实 | 后续要做的事 |
| --- | --- | --- |
| 已有玩家绑定原 PlayerId | `GoAuthGateway.LinkGoogleAsync`（`GoAuthGateway.cs:67-87`）已实现，**全工程无调用入口** | ① Login 页：本地存在有效 Guest 会话时走 `LinkGoogleAsync`（保 player_id）而非 `GoogleLoginAsync`；② Main 场景新增「绑定 Google」入口。注意当前 `LoginController.RestoreSessionOrShowLogin:179-186` 在会话有效时**直接进 Main**，不会停在登录页 → 需要新的入口位置或调整启动判定 |
| 启动优先刷新 Drakeforge Token | 仅 `RefreshingUnaryTransport.cs:31` 的 401 被动刷新 | 启动期主动调一次 `/v1/auth/refresh`；refresh 失败才回登录页（顺带消除"重复弹 Google 账号选择"） |
| Token 存储不安全 | `PersistentAuthSessionStore.cs:89-91` PlayerPrefs 明文 | 换 Android Keystore（`EncryptedSharedPreferences` 或原生加密） |
| 取消/网络/Token/冲突错误态 | `LoginController` 三处统一 `ShowMessage(exception.Message)` | P0 只做码表映射（步骤 6）；P1 做重试/降级策略与埋点 |
| `google-services.json` 缺 `oauth_client` | 文件 `oauth_client: []` | 见步骤 7（控制台 + 重下载） |

---

## 七、需要服务端 / 后台确认的外部依赖（不要凭空猜）

1. **`/v1/auth/google` 的 200 vs 201 语义**：201 是否表示"新建玩家"？客户端是否需要据此区分"新号首登"引导？
2. **账号冲突错误码**：Google 账号已绑定另一个 PlayerId 时，服务端返回的 HTTP status 与业务 code 是什么（图2 要求覆盖"账号冲突"，客户端需要精确码做文案分支）。
3. **`/v1/auth/link/google` 的语义**：失败时是否可能返回"该 Google 账号已绑定他人"？绑定成功后是否需要重新下发 token？
4. **`/v1/auth/refresh` 的过期策略**：refresh_token 有效期、是否轮换（轮换则步骤 6 的 `SESSION_EXPIRED` 判定要配合）。
5. **Android OAuth 客户端登记**：由谁在 Google Cloud（`drakeforge`）登记包名 + SHA-1，并提供重签后的 `google-services.json`。

---

## 附：P0 交付物清单

| 类型 | 路径 | 新增/修改 |
| --- | --- | --- |
| 依赖 | `Assets/Plugins/Android/DragonBoundGoogleAuthDependencies.xml` | 新增 |
| Java | `Assets/Plugins/Android/com/drakeforge/mergedefense/googleauth/GoogleSignInBridge.java` | 新增 |
| C# | `Assets/Scripts/Auth/Infrastructure/AndroidGoogleSignInBridge.cs` | 新增 |
| C# | `Assets/Scripts/Auth/Infrastructure/AndroidGoogleOAuthProvider.cs` | 新增 |
| C# | `Assets/Scripts/Auth/Contracts/GoogleIdTokenPayload.cs` | 新增（JWT payload 解析） |
| C# | `Assets/Scripts/Auth/Contracts/AuthErrorMessages.cs` | 新增（错误码 → 文案） |
| C# | `Assets/Scripts/Services/Configuration/ClientServiceConfig.cs` | 新增字段 |
| 配置 | `Assets/Resources/Configuration/ClientServiceConfig.asset` | 填 Web Client ID |
| C# | `Assets/Scripts/Services/Composition/GoUnaryServiceModule.cs` | 替换注入点 + 可选强校验 |
| C# | `Assets/Scripts/Auth/UI/LoginController.cs` | 3 处 catch 改用文案映射 |
| 配置 | `Assets/Plugins/Android/mainTemplate.gradle` | EDM 自动写入（勿手改） |
| 配置 | `ProjectSettings/AndroidResolverDependencies.xml` | EDM 自动写入（勿手改） |
