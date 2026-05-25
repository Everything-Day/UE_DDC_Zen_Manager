# UE DDC Manager — 虚幻引擎 DDC、Zen 缓存管理工具

一键修改 Unreal Engine 的 Derived Data Cache (DDC) 和 Zen Server 缓存存储位置，释放系统盘空间。

## 虚幻引擎缓存机制简介

Unreal Engine 在运行过程中会生成大量**派生数据缓存（Derived Data Cache, DDC）**，用于存储纹理压缩、Shader 编译、静态网格构建等预处理结果，避免每次启动都重新计算。

### 缓存类型

| 缓存类型 | 说明 | 默认位置 |
|---------|------|---------|
| **Local DDC** | 本地派生数据缓存，存储当前用户的编译产物 | `%LOCALAPPDATA%\UnrealEngine\Common\DerivedDataCache` |
| **Zen Server** | UE5 新增的高性能缓存服务，替代部分传统 DDC 功能 | `%LOCALAPPDATA%\UnrealEngine\Common\Zen\Data` |
| **Shared DDC** | 团队共享缓存，多人可复用同一份编译产物加速加载 | 无默认值，需手动配置 |

### 缓存控制方式

引擎通过以下机制决定缓存存储位置（优先级从高到低）：

1. **用户环境变量 `UE-LocalDataCachePath`** — 同时控制 Local DDC 和 Zen Server 数据路径（通过引擎内置 `LocalDataCachePathEnvOverride` 机制）
2. **BaseEngine.ini 配置文件** — `[InstalledDerivedDataBackendGraph]` / `[DerivedDataBackendGraph]` 中的 `Local=(..., Path=xxx)` 字段
3. **引擎硬编码默认路径** — 即上表中的 `%LOCALAPPDATA%` 路径

### 为什么需要迁移

- 默认缓存全部存储在系统盘（C 盘），随项目增多可占用 **数十 GB** 空间
- SSD 系统盘空间宝贵，DDC 数据完全可以存放在大容量非系统盘
- 缓存删除后引擎会自动重新生成，迁移操作**完全安全**

---

## 功能说明

### 模块一：引擎版本检测

| 按钮 | 功能 |
|------|------|
| **浏览...** | 选择虚幻引擎安装根目录 |
| **扫描引擎** | 扫描指定目录下的所有引擎版本，识别 UE4/UE5，读取当前 DDC 配置路径 |

扫描逻辑支持：
- Epic Games Launcher 标准安装布局（`UE_5.4`、`UE_5.5` 等）
- 自定义命名（`UE_5_4`、`UE-5.4` 等）
- 源码编译版本（含 `Engine/Build/Build.version`）

### 模块二：指定新缓存位置

| 按钮 | 功能 |
|------|------|
| **浏览文件夹...** | 选择新的 DDC 和 Zen Server 缓存存储目录 |

设置的路径将同时作为 Local DDC 和 Zen Server 的数据目录。

### 高级选项：Shared DDC

| 控件 | 功能 |
|------|------|
| **勾选框** | 启用/禁用 Shared DDC 设置 |
| **浏览文件夹...** | 选择团队共享缓存目录（设置 `UE-SharedDataCachePath` 环境变量） |

适用于团队协作场景，个人开发者通常无需设置。

### 模块三：修改方式

| 选项 | 说明 |
|------|------|
| **方式一：环境变量**（推荐） | 设置用户级环境变量 `UE-LocalDataCachePath`，全局生效，无需修改任何文件 |
| **方式二：配置文件** | 直接修改选中引擎的 `BaseEngine.ini`，仅对该版本生效 |
| **方式三：双重保险** | 同时应用环境变量和配置文件 |

### 环境变量状态区

| 按钮 | 功能 |
|------|------|
| **移除缓存环境变量** | 删除 `UE-LocalDataCachePath`，恢复引擎默认缓存位置 |
| **移除 Shared DDC 变量** | 删除 `UE-SharedDataCachePath` |

### 清理默认缓存

| 按钮 | 功能 |
|------|------|
| **清理旧缓存** | 删除系统盘上默认 Local DDC 和 Zen Server 缓存内容，释放空间 |

安全机制：
- 清理前检测 UnrealEditor、ZenServer、EpicGamesLauncher 进程，如有运行则阻止操作
- 仅清空目录内容，保留空目录壳
- 逐文件删除，遇到权限问题跳过不崩溃
- 显示释放的磁盘空间大小

### 底部操作区

| 按钮 | 功能 |
|------|------|
| **全选/取消全选** | 切换引擎列表全选状态 |
| **一键修改缓存位置** | 执行所选修改方式，应用新缓存路径 |

### 语言切换

| 按钮 | 功能 |
|------|------|
| **CN/EN**（右上角） | 切换界面中文/英文显示 |

---

## 运行环境要求

- **操作系统**：Windows 10 / 11（x64）
- **运行时**：[.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（必须安装）
- **权限**：普通用户权限即可（设置用户级环境变量无需管理员）

> 如果未安装 .NET 8 运行时，双击 exe 后系统会弹窗提示下载。

---

## 构建方式

```bash
# 克隆仓库
git clone https://github.com/your-username/UE_DDC_Manager.git
cd UE_DDC_Manager/UE_DDC_Manager

# 发布单文件
dotnet publish -c Release -o bin/Publish
```

生成的 `bin/Publish/UE_DDC_Manager.exe` 即为可分发的单文件程序。

---

## 注意事项

- 修改完成后，请**彻底退出** Epic Games Launcher 及所有后台虚幻引擎进程（包括 ZenServer.exe），再次启动引擎才能完全生效
- 配置文件修改方式会自动备份原始 `BaseEngine.ini`（后缀 `.backup_时间戳`）
- 程序会自动清理历史遗留的无效环境变量 `UE-ZenDataPath`（该变量不被引擎识别）

---

## License

MIT
