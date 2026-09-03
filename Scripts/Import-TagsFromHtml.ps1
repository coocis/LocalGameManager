param(
    [string]$HtmlPath = "C:\softwares\AI\GamesManager\Requirements\tags\tag.html",
    [string]$OutputPath = "C:\softwares\AI\GamesManager\Requirements\tags\import-tags.json",
    [string]$Type = ""
)

$utf8 = [Text.Encoding]::UTF8
$typeMap = @{
    $utf8.GetString([Convert]::FromBase64String('5YGP5aW9L+mcgOaxgg==')) = 'Preference'
    $utf8.GetString([Convert]::FromBase64String('54mp5ZOBL+mBk+WFtw==')) = 'Item'
    $utf8.GetString([Convert]::FromBase64String('6KeS6Imy')) = 'Character'
    $utf8.GetString([Convert]::FromBase64String('5Yi25pyNL+iho+edgC/ogYzkuJo=')) = 'Clothing'
    $utf8.GetString([Convert]::FromBase64String('5Ymn5oOFL+ezu+e7nw==')) = 'Plot'
    $utf8.GetString([Convert]::FromBase64String('546p5rOVL0jlgL7lkJE=')) = 'Gameplay'
    $utf8.GetString([Convert]::FromBase64String('5aSW6LKML+i6q+S9k+eJueW+gQ==')) = 'Appearance'
    $utf8.GetString([Convert]::FromBase64String('Ui0xOEcv54yO5aWH')) = 'Grotesque'
}

$content = Get-Content -LiteralPath $HtmlPath -Raw -Encoding UTF8
if ($Type) {
    if ($Type -ne 'WorkForm') { throw 'Only WorkForm is supported without category headings.' }
    $items = foreach ($li in [regex]::Matches($content, '<li\b[^>]*>(?<text>[\s\S]*?)</li>', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        $name = (($li.Groups['text'].Value -replace '<[^>]+>', '').Trim())
        if ($name) { [pscustomobject]@{ name = $name; type = $Type } }
    }
} else {
    $items = foreach ($part in [regex]::Split($content, '(?=<div\s+id="scroll_genre)')) {
        if ($part -notmatch '^<div\s+id="scroll_genre[^"]+"[\s\S]*?<p[^>]*>(?<title>[\s\S]*?)</p>') { continue }
        $title = (($matches.title -replace '<[^>]+>', '').Trim())
        if (-not $typeMap.ContainsKey($title)) { throw "Unmapped tag category: $title" }
        foreach ($li in [regex]::Matches($part, '<li\b[^>]*>(?<text>[\s\S]*?)</li>', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
            $name = (($li.Groups['text'].Value -replace '<[^>]+>', '').Trim())
            if ($name) { [pscustomobject]@{ name = $name; type = $typeMap[$title] } }
        }
    }
}

$items | Sort-Object type, name -Unique | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
$items | Group-Object type | Sort-Object Name | ForEach-Object { "{0}: {1}" -f $_.Name, $_.Count }
Write-Output "JSON: $OutputPath"
