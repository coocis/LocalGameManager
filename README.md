# 本地游戏管理器

一个面向 Windows 的本地游戏库管理器。它以 WPF 图形界面管理已安装的游戏，并附带一个适合自动化与 AI 工作流使用的命令行工具。

> 本项目仅管理已经存在于本机的游戏目录，不提供下载或分发功能。

## 功能概览

- 扫描文件夹或手动添加本地游戏；自动记录路径、可执行文件和文件容量。
- 大图标与详细信息两种游戏列表视图，支持评分、状态标记、筛选与全文搜索。
- 游戏资料：名称、封面与截图、社团与人员信息、作品形式、分类标签、商店链接、游玩统计等。
- 本地 SQLite 数据库保存游戏、标签和图片二进制数据；图片不会以普通图片文件形式落在资料目录中。
- 支持深色/浅色主题、图片浏览、标签管理、统计信息和数据备份。
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

抓取器会请求 DLsite 的公开页面，并尽量使用简体中文页面资料。它会下载封面与截图、提取游戏资料，并通过 CLI 写入本地数据库。使用前请确认你的使用方式符合目标网站规则；详细用法见 [抓取器说明](LocalGameManager.MetadataFetcher/README.md)。

## 许可

本项目采用 [MIT License](LICENSE)。界面图标的来源与许可说明见 [图标致谢](LocalGameManager/Assets/ICON_ATTRIBUTION.md)。
