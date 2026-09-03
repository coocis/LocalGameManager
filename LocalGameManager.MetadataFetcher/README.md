# DLsite 元数据抓取器

使用 Python 3.12 和标准库，从已入库游戏的 DLsite 商店链接读取简体中文页面，下载轮播图片，并生成给 LocalGameManager.Cli bulk-update 使用的 JSON。

~~~powershell
py -3.12 .\dlsite_metadata_fetcher.py --app-root ..\Release
~~~

- 请求会附带 locale=zh-cn Cookie，并使用 locale=zh_CN 查询参数。
- 轮播图第一张保存为封面，其余保存为截图。
- 作品内容只提取介绍区域前六个非空文本行。
- storeId 从 URL 的 product_id/RJ... 提取。
- 默认仅刷新从未自动更新过、或自动更新时间超过设置页“信息自动更新最短间隔”的游戏；命令行 `--refresh-days` 可临时指定天数，`--force` 忽略此限制。
- 译名与作品内容优先调用 Data\settings.json 中配置的翻译中转器；中转器未配置、不可用或翻译失败时，才通过 Google Translate 的公开网页接口翻译为简体中文。两者都失败时保留原文。使用 --no-translate 禁用。
- 默认只生成 JSON 与下载图片。增加 --apply 后才调用 CLI 写入资料库。
- 使用 --apply 成功写入后，临时下载图片会自动删除，避免在文件管理器中遗留可直接浏览的图片；--keep-downloads 可保留它们用于排错。

单个游戏测试：

~~~powershell
py -3.12 .\dlsite_metadata_fetcher.py --app-root ..\Release --id 6 --force
~~~

按游戏原名搜索 DLsite、取首个结果并更新指定游戏：

~~~powershell
py -3.12 .\dlsite_metadata_fetcher.py --app-root ..\Release --id 6 --lookup-original "游戏原名" --force --apply
~~~

下载和 JSON 默认保存在 Release\Data\metadata-fetch。CLI 成功写入后会将图片作为 BLOB 保存至数据库。
