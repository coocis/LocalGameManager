# AI 元数据导入（CLI）

先读取待处理游戏索引：

```powershell
./LocalGameManager.Cli.exe metadata-index
```

输出中的 `id` 是后续导入的定位键；同时提供原名、译名、商店名称/链接/ID、游戏路径、`metadataUpdatedAtUtc`、`hasCover` 与 `screenshotCount`。`metadataUpdatedAtUtc` 为 `null` 表示从未通过自动导入更新。

AI 应先将网页上的封面和截图下载到本地临时图片文件，再生成 JSON。`coverPath` 指向单张封面；`screenshotPaths` 是截图路径数组。只要某个媒体字段没有出现在记录中，原有媒体就会保留；字段出现且值为 `null` 或空数组时会清除相应媒体。

```json
{
  "games": [
    {
      "id": 12,
      "originalName": "日文原名",
      "translatedName": "中文译名",
      "storeName": "DLsite",
      "storeUrl": "https://example.invalid/product/RJ000000",
      "storeId": "RJ000000",
      "clubName": "社团名",
      "authors": "作者",
      "illustrators": "插画师",
      "voiceActors": "声优",
      "releaseDate": "2026-08-07",
      "gameEngine": "RPG Maker",
      "description": "作品内容",
      "review": "评价",
      "coverPath": "C:\\temp\\cover.jpg",
      "screenshotPaths": ["C:\\temp\\1.jpg", "C:\\temp\\2.jpg"],
      "tags": [
        { "type": "WorkForm", "name": "角色扮演" },
        { "type": "Character", "name": "女主人公" }
      ]
    }
  ]
}
```

先预演，再实际写入：

```powershell
./LocalGameManager.Cli.exe bulk-update import.json
./LocalGameManager.Cli.exe bulk-update import.json --apply
```

所有输出为 JSON。写入前会备份数据库；单次导入使用事务，找不到游戏、标签或图片文件时不会提交该次导入。每一条成功写入的记录都会自动更新 `metadataUpdatedAtUtc`。
