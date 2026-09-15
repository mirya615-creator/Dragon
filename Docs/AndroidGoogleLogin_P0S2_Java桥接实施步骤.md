# 步骤 2：Android 原生桥接（Java）— 具体实施步骤

> 目标：把 Credential Manager 的 Google 登录能力封装成一段 Android 原生代码，由步骤 3 的 C# 通过 JNI 调用。
> 适用：本项目 Unity **2022.3.62t14（团结引擎）**，Android 平台，包名 `com.drakeforge.mergedefense`。
> 状态：**方案，未落任何文件**。

---

## 0. ⚠️ 先纠正上一版方案的一个错误结论

`Docs/AndroidGoogleLogin_P0_实施步骤.md` §步骤 2 写的是：

> ~~Java 源码必须进 `unityLibrary` 模块，直接放 `Assets/Plugins/Android/com/xxx/GoogleSignInBridge.java`，**不要**自建 `*.androidlib`~~

**这个结论是错的，现予推翻。** 核实依据：

| 来源 | 证据 |
| --- | --- |
| Unity 官方《Gradle for Android》（2022.3 / 2023.2 / 6000.0 三个版本措辞一致） | `unityLibrary/src/main/java` 一栏原文：*"Unity **only uses this directory to store the UnityPlayerActivity source file**"* → 散放的 `.java` 不在 Unity 支持范围内，不应依赖 |
| Unity 官方《Create an Android Library plug-in》 | 明确给出的 **`MyFeature.androidlib` + 自定义 `build.gradle` + `src/main/java/<package>/Controller.java`** 结构，并用 `AndroidJavaClass("com.company.feature.Controller")` 调用 —— **与本项目步骤 3 的设计完全同构** |
| 本机 Unity 安装 `PlaybackEngines/AndroidPlayer/Tools/GradleTemplates/libTemplate.gradle` | 这是 Unity **为 `.androidlib` 自动生成**的 build.gradle。其中 `manifest.srcFile 'AndroidManifest.xml'`、`java.srcDirs = ['src']` 被**注释掉**（→ 走 AGP 默认 `src/main/java`）、依赖只有 `fileTree` → 证实 Unity 不会代写第三方依赖，**必须自带 build.gradle** |
| 实证（Google 官方 AdMob Unity 插件） | 插件目录就是 `GoogleMobileAdsPlugin.androidlib/build.gradle` —— 生产级 Unity Android 插件的标准做法 |

**正确做法**：`DragonBoundGoogleAuth.androidlib`（自带 `build.gradle` 声明 `androidx.credentials` 依赖）+ `src/main/java/…/GoogleSignInBridge.java`。

> 附带影响：P0 初版方案里基于该错误论断写下的「EDM 依赖 XML 是必需的一步」不再成立 —— 详见本文件 §4（建议删除 `DragonBoundGoogleAuthDependencies.xml`）。

---

## 1. 建目录与文件

在 **`Assets/Plugins/Android/`** 下建（`.androidlib` 后缀必须一字不差，Unity 靠它识别为独立 Gradle 模块）：

```
Assets/Plugins/Android/
└── DragonBoundGoogleAuth.androidlib/
    ├── AndroidManifest.xml          ← 必须在顶层（对应 libTemplate 的 manifest.srcFile 相对路径）
    ├── project.properties           ← 可选，与项目现有 3 个 androidlib 保持一致
    ├── build.gradle                 ← 必须自带，否则 Unity 生成的版本里没有 credentials 依赖
    ├── proguard-rules.pro           ← Release 开混淆时的 keep 规则
    └── src/main/java/com/drakeforge/mergedefense/googleauth/
        └── GoogleSignInBridge.java  ← 目录必须严格镜像 package 路径
```

**为什么这样布局**（逐条对应本机 `libTemplate.gradle`）：

| 约束 | 依据 |
| --- | --- |
| manifest 放顶层，不放 `src/main/` | Unity 模板写 `manifest.srcFile 'AndroidManifest.xml'`（相对模块根）。本项目现有 3 个 androidlib 也都是这个布局 |
| Java 放 `src/main/java/<package>/` | Unity 模板里 `//java.srcDirs = ['src']` 是**注释掉的** → 实际走 AGP 默认源集 `src/main/java`；包名路径必须镜像，否则 Gradle 报 `package does not exist` |
| 不用 Kotlin | Kotlin 需额外引入 Kotlin Gradle 插件，Unity 支持较弱且收益为零 |

`.meta` 文件**不要手写**，让 Unity 自动生成（见 §5）。

---

## 2. 文件内容（完整，可直接粘贴）

### 2.1 `build.gradle`

```groovy
apply plugin: 'com.android.library'

dependencies {
    implementation fileTree(dir: 'bin', include: ['*.jar'])
    implementation fileTree(dir: 'libs', include: ['*.jar'])

    implementation 'androidx.core:core:1.15.0'
    implementation 'androidx.credentials:credentials:1.6.0'
    implementation 'androidx.credentials:credentials-play-services-auth:1.6.0'
    implementation 'com.google.android.libraries.identity.googleid:googleid:1.2.0'
}

android {
    namespace "com.drakeforge.mergedefense.googleauth"

    sourceSets {
        main {
            manifest.srcFile 'AndroidManifest.xml'
            res.srcDirs = ['res']
            assets.srcDirs = ['assets']
            jniLibs.srcDirs = ['libs']
        }
    }

    compileSdk 35

    compileOptions {
        sourceCompatibility JavaVersion.VERSION_11
        targetCompatibility JavaVersion.VERSION_11
    }

    defaultConfig {
        minSdk 24
        targetSdk 35
        consumerProguardFiles 'proguard-rules.pro'
    }

    lint {
        abortOnError false
    }
}
```

**版本与参数说明：**

| 项 | 取值 | 理由 |
| --- | --- | --- |
| `androidx.credentials` + `-play-services-auth` | **1.6.0**（2026-04-08 发布，**当前最新稳定版**；`1.7.0-alpha02/03` 是 alpha，不上生产） | 两者**必须同版本号** |
| `googleid` | **1.2.0** ⚠️ 上一版文档写的 `1.1.0` 已过时 | `credentials-play-services-auth:1.6.0` 自身依赖 `googleid:1.2.0`；显式声明避免版本漂移。我们要直接用 `GoogleIdTokenCredential`，传递依赖不可靠，必须显式声明 |
| `androidx.core` | 1.15.0 | 代码要用 `ContextCompat.getMainExecutor`；显式声明更安全 |
| `compileSdk 35` | ⚠️ 硬约束 | `androidx.core:1.15.0` 起要求 `compileSdk ≥ 35`。低于 35 时 AGP 会报 `Dependency 'androidx.core:core:1.15.0' requires 'compileSdkVersion' to be set to 35 or higher`（见 §6 处理办法） |
| `minSdk 24` | 与 `ProjectSettings.asset:176` 的 `AndroidMinSdkVersion=24` 对齐 | `credentials` 从 1.6.0-alpha05 起把默认 minSdk 从 21 提到 **23**，24 满足 ✅ |
| 不写 `buildToolsVersion` | 省略 | 交给 AGP 默认版本，避免与本机 SDK 里装的版本对不上 |
| DSL 用 `minSdk/targetSdk/compileSdk`（无 Version 后缀） | 与本机 `libTemplate.gradle` 一致 | Unity 的 `mainTemplate.gradle`（unityLibrary）用的还是旧 DSL，两者在一个 Gradle 工程里共存没问题 |

### 2.2 `AndroidManifest.xml`

```xml
<?xml version="1.0" encoding="utf-8"?>
<manifest xmlns:android="http://schemas.android.com/apk/res/android">
</manifest>
```

> **不要写 `package="..."` 属性。** AGP 7.4+ 之后由 `build.gradle` 的 `namespace` 承担命名空间；再写 `package` 在新 AGP 上会告警/报错。（项目现有 3 个 androidlib 之所以写 `package`，是因为它们不带 build.gradle、由 Unity 从 manifest 里取 namespace。）

### 2.3 `project.properties`（可选，建议保留一致性）

```
target=android-35
android.library=true
```

### 2.4 `proguard-rules.pro`

```
-keep class com.drakeforge.mergedefense.googleauth.** { *; }
-keep class com.google.android.libraries.identity.googleid.** { *; }
-keep class androidx.credentials.** { *; }
```

> `GoogleSignInBridge` 是被 C# 侧的 `AndroidJavaClass` 用 JNI `FindClass` 反射性加载的，Java 侧无任何引用 → **开启混淆时会被整类裁掉**。第一行 keep 是必须的。

### 2.5 `src/main/java/com/drakeforge/mergedefense/googleauth/GoogleSignInBridge.java`

```java
package com.drakeforge.mergedefense.googleauth;

import android.app.Activity;
import android.os.CancellationSignal;
import android.util.Log;

import androidx.core.content.ContextCompat;
import androidx.credentials.Credential;
import androidx.credentials.CredentialManager;
import androidx.credentials.CredentialManagerCallback;
import androidx.credentials.CustomCredential;
import androidx.credentials.GetCredentialRequest;
import androidx.credentials.GetCredentialResponse;
import androidx.credentials.exceptions.GetCredentialCancelationException;
import androidx.credentials.exceptions.GetCredentialException;
import androidx.credentials.exceptions.GetCredentialInterruptedException;
import androidx.credentials.exceptions.GetCredentialProviderConfigurationException;
import androidx.credentials.exceptions.GetCredentialUnknownException;
import androidx.credentials.exceptions.GetCredentialUnsupportedException;
import androidx.credentials.exceptions.NoCredentialException;

import com.google.android.libraries.identity.googleid.GetGoogleIdOption;
import com.google.android.libraries.identity.googleid.GoogleIdTokenCredential;
import com.google.android.libraries.identity.googleid.GoogleIdTokenParsingException;
import com.unity3d.player.UnityPlayer;

import org.json.JSONException;
import org.json.JSONObject;

import java.util.concurrent.Executor;
import java.util.concurrent.atomic.AtomicInteger;

/**
 * Credential Manager based Google sign-in bridge for Unity.
 *
 * Called from C# via AndroidJavaClass.CallStatic(...). Results are pushed back with
 * UnityPlayer.UnitySendMessage(gameObject, method, jsonString).
 */
public final class GoogleSignInBridge {

    private static final String TAG = "DBGoogleAuth";

    private static CancellationSignal pendingSignal;
    private static final AtomicInteger generation = new AtomicInteger(0);

    private GoogleSignInBridge() { }

    /**
     * Performs ONE credential query. The two-step strategy (authorized accounts -> all accounts)
     * is driven from the C# side, so this method stays single-shot.
     *
     * @param serverClientId the WEB client id (not the Android client id)
     */
    public static void signIn(String callbackGameObject,
                              String callbackMethod,
                              String requestId,
                              String serverClientId,
                              boolean filterByAuthorizedAccounts) {
        Activity activity = UnityPlayer.currentActivity;
        if (activity == null) {
            emitError(callbackGameObject, callbackMethod, requestId,
                    "INTERNAL", 0, "Unity activity is not available.");
            return;
        }
        if (serverClientId == null || serverClientId.length() == 0) {
            emitError(callbackGameObject, callbackMethod, requestId,
                    "INTERNAL", 0, "serverClientId is empty.");
            return;
        }

        cancel();                                   // invalidate any previous attempt
        final int expectedGeneration = generation.get();

        CredentialManager credentialManager = CredentialManager.create(activity);
        Executor mainExecutor = ContextCompat.getMainExecutor(activity);

        GetGoogleIdOption googleIdOption = new GetGoogleIdOption.Builder()
                .setServerClientId(serverClientId)
                .setFilterByAuthorizedAccounts(filterByAuthorizedAccounts)
                .setAutoSelectEnabled(filterByAuthorizedAccounts)   // must be false when not filtering
                .build();

        GetCredentialRequest request = new GetCredentialRequest.Builder()
                .addCredentialOption(googleIdOption)
                .build();

        CancellationSignal signal = new CancellationSignal();
        pendingSignal = signal;

        credentialManager.getCredentialAsync(
                activity,
                request,
                signal,
                mainExecutor,
                new CredentialManagerCallback<GetCredentialResponse, GetCredentialException>() {
                    @Override
                    public void onResult(GetCredentialResponse result) {
                        if (expectedGeneration != generation.get()) {
                            Log.d(TAG, "Discarded late result.");
                            return;
                        }
                        pendingSignal = null;
                        handleSuccess(callbackGameObject, callbackMethod, requestId, result);
                    }

                    @Override
                    public void onError(GetCredentialException e) {
                        if (expectedGeneration != generation.get()) {
                            Log.d(TAG, "Discarded late error.");
                            return;
                        }
                        pendingSignal = null;
                        emitError(callbackGameObject, callbackMethod, requestId,
                                mapError(e), 0, describe(e));
                    }
                });
    }

    /** Cancels the in-flight request, if any. Safe to call repeatedly. */
    public static void cancel() {
        generation.incrementAndGet();
        if (pendingSignal != null) {
            try {
                pendingSignal.cancel();
            } catch (Throwable ignored) {
                // nothing actionable
            }
            pendingSignal = null;
        }
    }

    private static void handleSuccess(String gameObject, String method, String requestId,
                                      GetCredentialResponse response) {
        Credential credential = response.getCredential();
        String type = credential.getType();

        if (!(credential instanceof CustomCredential)
                || !GoogleIdTokenCredential.TYPE_GOOGLE_ID_TOKEN_CREDENTIAL.equals(type)) {
            emitError(gameObject, method, requestId,
                    "NO_CREDENTIAL", 0, "Unsupported credential type.");
            return;
        }

        try {
            GoogleIdTokenCredential googleCredential =
                    GoogleIdTokenCredential.createFrom(((CustomCredential) credential).getData());

            JSONObject payload = new JSONObject();
            payload.put("idToken", googleCredential.getIdToken());
            payload.put("subject", googleCredential.getId());
            payload.put("displayName", googleCredential.getDisplayName());
            payload.put("pictureUrl", googleCredential.getProfilePictureUri() == null
                    ? "" : googleCredential.getProfilePictureUri().toString());

            emitSuccess(gameObject, method, requestId, payload);
        } catch (GoogleIdTokenParsingException e) {
            emitError(gameObject, method, requestId,
                    "TOKEN_PARSE_FAILED", 0, "Failed to parse Google ID token credential.");
        } catch (JSONException e) {
            emitError(gameObject, method, requestId,
                    "INTERNAL", 0, "Failed to serialize sign-in result.");
        }
    }

    private static String mapError(GetCredentialException e) {
        if (e instanceof GetCredentialCancelationException) return "USER_CANCELED";
        if (e instanceof NoCredentialException) return "NO_CREDENTIAL";
        if (e instanceof GetCredentialInterruptedException) return "INTERRUPTED";
        if (e instanceof GetCredentialUnsupportedException) return "PROVIDER_UNAVAILABLE";
        if (e instanceof GetCredentialProviderConfigurationException) return "PROVIDER_UNAVAILABLE";
        if (e instanceof GetCredentialUnknownException) return "UNKNOWN";
        return "UNKNOWN";
    }

    /** Never includes the ID token — used for both logs and the Unity-side message. */
    private static String describe(GetCredentialException e) {
        return e == null || e.getClass() == null ? "Unknown error." : e.getClass().getName();
    }

    private static void emitSuccess(String gameObject, String method, String requestId,
                                    JSONObject payload) throws JSONException {
        JSONObject json = new JSONObject(payload.toString());
        json.put("requestId", requestId);
        json.put("status", "ok");
        json.put("errorType", "");
        json.put("errorCode", 0);
        json.put("message", "");
        UnityPlayer.UnitySendMessage(gameObject, method, json.toString());
    }

    private static void emitError(String gameObject, String method, String requestId,
                                  String errorType, int errorCode, String message) {
        try {
            JSONObject json = new JSONObject();
            json.put("requestId", requestId);
            json.put("status", "error");
            json.put("errorType", errorType);
            json.put("errorCode", errorCode);
            json.put("message", message == null ? "" : message);
            UnityPlayer.UnitySendMessage(gameObject, method, json.toString());
        } catch (JSONException ignored) {
            // nothing actionable
        }
        Log.w(TAG, "signIn failed: " + (errorType == null ? "?" : errorType));
    }
}
```

**几个容易踩的点，代码里已处理：**

1. **`GetCredentialCancelationException` 拼写是 Cancelation（一个 l）** —— AndroidX 的实际类名就这样，写成 `Cancellation` 编译不过。
2. **`autoSelectEnabled` 必须等于 `filterByAuthorizedAccounts`** —— 查全量账号时框架要求 `autoSelectEnabled=false`，否则抛参数异常。
3. **两次查询不在 Java 侧串起来** —— Java 只做单次查询，`NO_CREDENTIAL` 由 C# 侧换 `filterByAuthorizedAccounts=false` 再调一次；这样 `LoginController` 的"一次登录在飞行中"语义不变。
4. **generation 计数器** —— `cancel()` 自增，旧回调回来发现 generation 变了就静默丢弃，解决"取消/切场景后回调迟到"导致的 TCS 双写。
5. **回调走主线程 Executor** —— C# 侧 `OnGoogleSignInResult` 已在 Unity 主线程，无需再 marshal。
6. **ID token 不落日志** —— `emitError` 的 `message` 只放异常类名/固定文案。

---

## 3. 交付给步骤 3（C#）的契约

| 项 | 约定 |
| --- | --- |
| JNI 类名 | `com.drakeforge.mergedefense.googleauth.GoogleSignInBridge` |
| 静态方法 | `signIn(String cbGameObject, String cbMethod, String requestId, String serverClientId, boolean filterByAuthorizedAccounts)` |
| 取消方法 | `cancel()` |
| 回调方式 | `UnityPlayer.UnitySendMessage(gameObject, method, json)` — **单 string 参数** |
| 成功 JSON | `{"requestId":"…","status":"ok","idToken":"…","subject":"…","displayName":"…","pictureUrl":"…","errorType":"","errorCode":0,"message":""}` |
| 失败 JSON | `{"requestId":"…","status":"error","errorType":"USER_CANCELED\|NO_CREDENTIAL\|INTERRUPTED\|PROVIDER_UNAVAILABLE\|UNKNOWN\|TOKEN_PARSE_FAILED\|INTERNAL","errorCode":0,"message":"…"}` |

C# 侧用一个 `[Serializable]` DTO 接 `JsonUtility.FromJson<T>`（字段名逐字对应，见旧文档 §步骤 3.1）。

---

## 4. 与步骤 1（EDM XML）的关系 —— 建议删掉那个 XML

`Assets/Plugins/Android/DragonBoundGoogleAuthDependencies.xml`（你已创建，内容正确）现在**变成冗余**了：

- EDM 只往 `mainTemplate.gradle`（= **unityLibrary** 模块）注入依赖；
- 而 Java 源码在 **`DragonBoundGoogleAuth.androidlib` 这个独立模块**里，Gradle 的 `implementation` 不跨模块传递 → unityLibrary 里的 credentials 依赖对这个模块**完全没有帮助**；
- 这个模块需要的依赖已在自己的 `build.gradle` 里声明。

**两条路，任选其一：**

| 选项 | 做法 | 评价 |
| --- | --- | --- |
| **A（推荐）** | 删除 `DragonBoundGoogleAuthDependencies.xml` 及其 `.meta`，依赖真相收敛到 `androidlib/build.gradle` 一处 | 单一来源，不存在"一处升级一处忘"的版本漂移。依赖一生效于 **gradle 构建时**（`settingsTemplate.gradle` 里已有 `google()` + `mavenCentral()`，能被解析到） |
| B | 两个都留 | 能编译，但 unityLibrary 平白多一份用不到的依赖，且两处版本号可能漂移 |

> 注意这与 EDM/Firebase 的 `AppDependencies.xml` 不冲突 —— 那些依赖是给 unityLibrary 里的 Firebase C# 插件用的，保留不动。

---

## 5. Unity 侧操作与验证

1. 建完目录文件后回到 Unity，等待导入；检查 `.androidlib.meta` 被识别为 **PluginImporter**（参考 `FirebaseApp.androidlib.meta`：`Android: enabled 1`，其余平台 `enabled 0`）。若识别成别的 importer，手动在 Inspector 里勾选 **Load on startup / Android**。
2. `src/main/java/**/GoogleSignInBridge.java` 会被 Unity 当成普通文本资产导入，**不影响编译**（它只是源文件，真正编译由 Gradle 做）。
3. **验证被 Gradle 接纳（必做）**：Build Settings → Android → 勾选 **Export Project**，导出后检查：
   - 根目录 `settings.gradle` 的 include 里出现 `:DragonBoundGoogleAuth`
   - `<导出目录>/DragonBoundGoogleAuth/` 存在，且内部保留了 `src/main/java/com/drakeforge/mergedefense/googleauth/GoogleSignInBridge.java`
   - `<导出目录>/unityLibrary/build.gradle` 里有 `implementation project(':DragonBoundGoogleAuth')`
   - **`<导出目录>/DragonBoundGoogleAuth/build.gradle` 就是你自己写的那份，没被 Unity 覆盖**
4. 用 Android Studio 打开导出工程做一次 `assemble`，确认 Java 编译通过。

> Export Project 这一步强烈建议别跳过 —— 这是唯一能一步确认"文件放对位置 + 依赖被识别"的方法，比打整包快得多。

---

## 6. 预期编译问题速查

| 报错 | 原因 | 处理 |
| --- | --- | --- |
| `package com.drakeforge.mergedefense.googleauth does not exist` | Java 目录没镜像 package 路径 | 检查目录是 `src/main/java/com/drakeforge/mergedefense/googleauth/` |
| `cannot find symbol GetCredentialCancelationException` | 拼成 `GetCredentialCancellationException` | 类名是 `Cancelation`（单 l） |
| `Dependency 'androidx.core:core:1.15.0' requires 'compileSdkVersion' to be set to 35 or higher` | 本机 Android SDK 没装 API 35 | Unity Hub 给 2022.3.62t14 加装 Android SDK Platform 35；或临时把 `androidx.core` 降到 1.13.x（不推荐，长期还是要升级） |
| `Could not resolve androidx.credentials:credentials:1.6.0` | 离线/仓库未配 | `settingsTemplate.gradle` 里已有 `google()` + `mavenCentral()`（已确认），检查 Gradle 是否能联网 |
| `Manifest merger failed` | manifest 里残留 package/权限 | 用 §2.2 的空 manifest |
| `Execution failed for ':DragonBoundGoogleAuth'... namespace not specified` | AGP 要求 namespace | build.gradle 里已写 `namespace "com.drakeforge.mergedefense.googleauth"` |
| Unity 报 `.androidlib` 需要一个 `AndroidManifest.xml` | manifest 放到了 `src/main/` 而不是模块根 | 按 §1 布局放顶层 |

---

## 7. 安全约束（硬性）

- `idToken` **只**存在于 Java 方法栈与 JSON 回调字符串里，**不写日志、不写文件、不进 PlayerPrefs、不进 Analytics 事件**。
- C# 侧收到后立刻转成 `PendingGoogleIdentity` 使用，不做持久化。
- 后续 §步骤 3.3 提到要从 JWT payload 解 `email` / `email_verified` —— 那步用的是同一个 token，同样遵守上述约束。

---

## 8. 本步交付物

| 文件 | 动作 |
| --- | --- |
| `Assets/Plugins/Android/DragonBoundGoogleAuth.androidlib/AndroidManifest.xml` | 新增 |
| `Assets/Plugins/Android/DragonBoundGoogleAuth.androidlib/project.properties` | 新增（可选） |
| `Assets/Plugins/Android/DragonBoundGoogleAuth.androidlib/build.gradle` | 新增 |
| `Assets/Plugins/Android/DragonBoundGoogleAuth.androidlib/proguard-rules.pro` | 新增 |
| `Assets/Plugins/Android/DragonBoundGoogleAuth.androidlib/src/main/java/com/drakeforge/mergedefense/googleauth/GoogleSignInBridge.java` | 新增 |
| `Assets/Plugins/Android/DragonBoundGoogleAuthDependencies.xml`（+meta） | **建议删除**（见 §4） |
