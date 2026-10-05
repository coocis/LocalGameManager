# 本地游戏管理器

一个面向 Windows 的本地游戏库管理器。它以 WPF 图形界面管理已安装的游戏，并附带一个适合自动化与 AI 工作流使用的命令行工具。

> 本项目仅管理已经存在于本机的游戏目录，不提供下载或分发功能。

## 功能概览

- 扫描文件夹或手动添加本地游戏；自动记录路径、可执行文件和文件容量。
- 大图标与详细信息两种游戏列表视图，支持评分、状态标记、筛选与全文搜索。
- 游戏资料：名称、封面与截图、社团与人员信息、作品形式、分类标签、商店链接、游玩统计等。
- 本地 SQLite 数据库保存游戏、标签和图片二进制数据；图片不会以普通图片文件形式落在资料目录中。
- 支持深色/浅色主题、图片浏览、标签管理与统计信息。
- 翻译中转器支持本地和远程模式；支持一键配置 XUnity，以及保存中转器设置时批量同步游戏配置。
- 可从 DLsite 获取资料、封面、截图、销量和评分；支持单项与批量更新。
- 自动检测 Unity、RPG Maker、Ren'Py、Unreal Engine、Godot 与 KiriKiri 等常见引擎特征。
- `@` 开头的 AI 自然语言搜索：模型先选择必要字段，再返回经过排序的本地游戏 ID，不会发送路径或可执行文件。
- 命令行工具可供脚本或 AI 批量导入、修改游戏资料；详见 [AI 元数据导入说明](LocalGameManager.Cli/Docs/AI_METADATA_IMPORT.md)。

## 项目结构

| 目录 | 说明 |
|---|---|
| `LocalGameManager` | WPF GUI 项目 |
| `LocalGameManager.Cli` | 面向自动化的 CLI 项目 |
| `LocalGameManager.MetadataFetcher` | Python 3.12 DLsite 元数据抓取器 |
| `LocalGameManager/InitialData` | 仅包含预置标签的初始 SQLite 数据库 |

## 环境要求

- Windows 10/11
- .NET SDK 10
- Python 3.12（仅自动获取 DLsite 资料时需要）

## 构建与运行

```powershell
dotnet build .\LocalGameManager\LocalGameManager.csproj -c Release
dotnet run --project .\LocalGameManager\LocalGameManager.csproj
```

发布 GUI 与 CLI 到同一个便携目录：

```powershell
dotnet publish .\LocalGameManager\LocalGameManager.csproj -c Release -o .\Release
dotnet publish .\LocalGameManager.Cli\LocalGameManager.Cli.csproj -c Release -o .\Release
```

首次运行时，程序会在可执行文件旁创建 `Data` 目录，并将随程序提供的初始数据库复制为 `Data/library.db`。初始数据库仅含标准标签，不含任何游戏、图片、商店收藏、路径或个人配置。之后的个人资料均保存在 `Data` 中，不应提交到版本控制。

## AI 搜索配置

在设置页中配置模型名称、服务地址和 API Key 对应的环境变量名。默认变量名为 `DEEPSEEK_API_KEY`。例如：

```powershell
[Environment]::SetEnvironmentVariable('DEEPSEEK_API_KEY', '你的 API Key', 'User')
```

使用 `@` 开头的搜索会调用模型，例如：

```text
@列出文件容量前 3 高且未通关的游戏
```

AI 请求仅发送本次查询需要的游戏字段与本地 ID；游戏路径、可执行文件路径和备注不会发送。作品内容只有在查询明确提及剧情、故事、介绍或设定时才允许发送。

## DLsite 资料更新

资料翻译使用设置页中配置的翻译中转器，支持本地服务与远程 HTTP 服务器。配置方式见下节。

抓取器会请求 DLsite 的公开页面，并尽量使用简体中文页面资料。它会下载封面与截图、提取游戏资料，并通过 CLI 写入本地数据库。使用前请确认你的使用方式符合目标网站规则；详细用法见 [抓取器说明](LocalGameManager.MetadataFetcher/README.md)。

## 翻译中转器与 XUnity

在设置页选择中转器模式，并填写服务地址和访问密钥：

- **本地**：默认地址为 `http://127.0.0.1:8765`，另需填写启动文件路径及启动等待时间。管理器按需启动服务，并在没有游戏或更新任务使用时关闭自己启动的进程。
- **远程**：填写服务器的 HTTP 地址，例如 `http://服务器地址:8765`。此模式隐藏本地启动选项，不启动或关闭服务器进程。
- **访问密钥**：填写中转器 `access_key.txt` 中的 64 位十六进制密钥。配置保存在本机 `Data/settings.json`，不要上传个人配置文件。
- **测试连接**：检查服务的 `/health` 接口是否可用；该检查不验证翻译密钥。

服务地址不需要附加 `/health` 或 `/translate`。管理器自动生成健康检查地址和 `/translate/密钥` 翻译地址。旧设置中的 `healthUrl` 会兼容读取为服务地址。游戏详情中的“使用翻译中转器”控制本地服务的按需启动；远程服务不可用时，游戏仍可启动。

准备好 XUnity 插件后，在本地游戏详情页点击“一键配置 XUnity”。程序在游戏可执行文件所在目录和游戏目录中查找 `BepInEx/config/AutoTranslatorConfig.ini`，并写入：

```ini
[Service]
Endpoint=CustomTranslate
FallbackEndpoint=

[General]
Language=zh-CN
FromLanguage=ja

[Custom]
Url=http://服务器地址:8765/translate/访问密钥

[Behaviour]
MaxCharactersPerTranslation=200
```

其他配置项会保留。找不到文件或密钥无效时会弹窗提示；此功能不安装插件。

保存设置时，若中转器配置发生变化，管理器会批量更新所有勾选“使用翻译中转器”的本地游戏的 XUnity 配置。单个游戏缺少配置文件或更新失败不会中止其他游戏，结束后显示成功、缺失和失败数量。未修改中转器配置时不会触发批量同步。

数据库写入不自动创建备份；需要备份时可在关闭程序后自行复制 `Data` 目录。

## 许可

本项目采用 [MIT License](LICENSE)。界面图标的来源与许可说明见 [图标致谢](LocalGameManager/Assets/ICON_ATTRIBUTION.md)。
