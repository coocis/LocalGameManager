#!/usr/bin/env python3
"""Fetch DLsite metadata in Simplified Chinese and pass normalized JSON to LocalGameManager.Cli."""

from __future__ import annotations

import argparse
import html
import json
import re
import shutil
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

USER_AGENT = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0.0.0 Safari/537.36"
if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
PRODUCT_ID_RE = re.compile(r"/product_id/(RJ[0-9A-Z]+)", re.IGNORECASE)


@dataclass(frozen=True)
class Tag:
    type: str
    name: str


def cli_path(app_root: Path) -> Path:
    return app_root / "LocalGameManager.Cli.exe"


def run_cli(app_root: Path, *arguments: str) -> dict[str, Any]:
    result = subprocess.run([str(cli_path(app_root)), *arguments], cwd=app_root, capture_output=True, text=True, encoding="utf-8", check=False)
    if result.returncode:
        raise RuntimeError(result.stderr.strip() or result.stdout.strip() or "CLI failed.")
    return json.loads(result.stdout)


def request_bytes(url: str) -> bytes:
    separator = "&" if "?" in url else "?"
    request = urllib.request.Request(
        f"{url}{separator}locale=zh_CN",
        headers={"User-Agent": USER_AGENT, "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8", "Accept-Language": "zh-CN,zh;q=0.9,ja;q=0.6", "Referer": "https://www.dlsite.com/maniax/", "Cookie": "locale=zh-cn; locale=zh_CN"},
    )
    for attempt in range(3):
        try:
            with urllib.request.urlopen(request, timeout=20) as response:
                return response.read()
        except urllib.error.HTTPError as error:
            if error.code in {403, 429} and attempt < 2:
                time.sleep(2)
                continue
            raise RuntimeError(f"DLsite 拒绝了本次请求（HTTP {error.code}）。请稍后重试。") from error
        except (urllib.error.URLError, TimeoutError, OSError) as error:
            if attempt < 2:
                time.sleep(2)
                continue
            raise RuntimeError(f"DLsite 请求或图片下载超时：{error}") from error


def strip_html(value: str) -> str:
    value = re.sub(r"<br\s*/?>", "\n", value, flags=re.IGNORECASE)
    value = re.sub(r"</(?:p|div|h[1-6]|li|tr)>", "\n", value, flags=re.IGNORECASE)
    return html.unescape(re.sub(r"<[^>]+>", "", value)).replace("\xa0", " ")


def element_by_id(source: str, element_id: str) -> str:
    match = re.search(rf'<(?P<tag>\w+)\b[^>]*\bid=["\']{re.escape(element_id)}["\'][^>]*>(?P<body>[\s\S]*?)</(?P=tag)>', source, re.IGNORECASE)
    return match.group("body") if match else ""


def table_html(source: str, label: str) -> str | None:
    for header, body in re.findall(r"<tr\b[^>]*>[\s\S]*?<th\b[^>]*>([\s\S]*?)</th>[\s\S]*?<td\b[^>]*>([\s\S]*?)</td>[\s\S]*?</tr>", source, re.IGNORECASE):
        if strip_html(header).strip() == label:
            return body
    return None


def table_value(source: str, label: str) -> str | None:
    body = table_html(source, label)
    return re.sub(r"\s+", " ", re.sub(r"\s*/\s*", " / ", strip_html(body).strip())) if body else None


def first_six_lines(source: str) -> str | None:
    match = re.search(r'<(?:div|section)\b[^>]*\bitemprop=["\']description["\'][^>]*>', source, re.IGNORECASE)
    if not match:
        return None
    body = source[match.end():]
    end = re.search(r'<div\b[^>]*\bid=["\']version_up["\']', body, re.IGNORECASE)
    lines = [re.sub(r"\s+", " ", line).strip() for line in strip_html(body[:end.start()] if end else body).splitlines()]
    return "\n".join([line for line in lines if line][:6]) or None


def slider_images(source: str) -> list[str]:
    match = re.search(r'<div\b[^>]*\bclass=["\'][^"\']*product-slider-data[^"\']*["\'][^>]*>([\s\S]*?)</div>\s*<div\b[^>]*\bclass=["\'][^"\']*work_slider', source, re.IGNORECASE)
    urls = re.findall(r'\bdata-src=["\']([^"\']+)', match.group(1), re.IGNORECASE) if match else []
    result: list[str] = []
    for value in urls:
        url = urllib.parse.urljoin("https://www.dlsite.com", html.unescape(value))
        if url not in result:
            result.append(url)
    return result


def product_id(url: str) -> str:
    match = PRODUCT_ID_RE.search(url)
    if not match:
        raise ValueError(f"DLsite URL has no product_id: {url}")
    return match.group(1).upper()


def product_url(value: str) -> str:
    return f"https://www.dlsite.com/maniax/work/=/product_id/{value}.html"


def product_stats(store_id: str) -> tuple[str | None, str | None]:
    url = f"https://www.dlsite.com/maniax/product/info/ajax?product_id={urllib.parse.quote(store_id)}"
    try:
        payload = json.loads(request_bytes(url).decode("utf-8", errors="replace"))
        item = payload.get(store_id) or payload.get(store_id.upper()) or {}
        sales = item.get("dl_count")
        rating = item.get("rate_average_2dp")
        return (f"{int(sales):,}" if sales is not None else None, str(rating) if rating is not None else None)
    except Exception:
        return None, None


def search_product_id(original_name: str) -> str:
    keyword = urllib.parse.quote(original_name, safe="")
    url = f"https://www.dlsite.com/maniax/fsr/=/language/jp/sex_category%5B0%5D/male/keyword/{keyword}/ana_flg/all/"
    source = request_bytes(url).decode("utf-8", errors="replace")
    results = element_by_id(source, "search_result_img_box")
    match = re.search(r'<li\b[^>]*\bdata-list_item_product_id=["\'](RJ[0-9A-Z]+)["\']', results, re.IGNORECASE)
    if not match:
        raise RuntimeError(f"DLsite 搜索未找到 {original_name!r} 的 RJ 商品。")
    return match.group(1).upper()


def search_store_games(keyword: str) -> list[dict[str, str]]:
    encoded = urllib.parse.quote_plus(keyword, safe="")
    url = f"https://www.dlsite.com/maniax/fsr/=/language/jp/sex_category%5B0%5D/male/keyword/%22{encoded}%22/work_category%5B0%5D/doujin/order/release_d/ana_flg/all/"
    source = request_bytes(url).decode("utf-8", errors="replace")
    start = source.find('id="search_result_img_box"')
    body = source[start:] if start >= 0 else ""
    result: list[dict[str, str]] = []
    for item in re.findall(r'<li\b[\s\S]*?</li>', body, re.IGNORECASE):
        match = re.search(r'\bdata-list_item_product_id=["\'](RJ[0-9A-Z]+)["\']', item, re.IGNORECASE)
        if not match:
            continue
        store_id = match.group(1).upper()
        title = re.search(r'<dd\b[^>]*\bclass=["\'][^"\']*work_name[^"\']*["\'][^>]*>[\s\S]*?<a\b[^>]*\btitle=["\']([^"\']+)', item, re.IGNORECASE)
        cover = re.search(r':thumb-candidates=["\']\[\'[\s]*([^\']+)', item, re.IGNORECASE)
        if not cover:
            cover = re.search(r'<div\b[^>]*\bclass=["\'][^"\']*thumb-container[^"\']*["\'][^>]*>[\s\S]*?<img\b[^>]*(?:data-src|src)=["\']([^"\']+)', item, re.IGNORECASE)
        result.append({"storeId": store_id, "originalName": html.unescape(title.group(1)).strip() if title else store_id, "storeUrl": product_url(store_id), "coverUrl": urllib.parse.urljoin("https://www.dlsite.com", html.unescape(cover.group(1))) if cover else ""})
    return result


def store_details(store_id: str, relay: str | None, translate: bool) -> dict[str, Any]:
    url = product_url(store_id)
    page = request_bytes(url).decode("utf-8", errors="replace")
    images = slider_images(page)
    categories = table_value(page, "分类")
    category_type = strip_html(element_by_id(page, "category_type"))
    sales, rating = product_stats(store_id)
    original = strip_html(element_by_id(page, "work_name")).strip() or store_id
    description = first_six_lines(page) or ""
    return {"storeId": store_id, "storeUrl": url, "originalName": original, "translatedName": translate_text(original, relay, translate), "clubName": table_value(page, "社团名"), "authors": table_value(page, "作者"), "illustrators": table_value(page, "插画"), "voiceActors": table_value(page, "声优"), "releaseDate": table_value(page, "发售日"), "description": translate_text(description, relay, translate), "workForms": " / ".join(strip_html(value).strip() for value in re.findall(r"<a\b[^>]*>([\s\S]*?)</a>", element_by_id(page, "category_type"), re.IGNORECASE) if strip_html(value).strip()), "categoryTags": categories, "fileSize": table_value(page, "文件容量"), "supportedLanguages": table_value(page, "支持的语言"), "ageRating": table_value(page, "年龄指定"), "salesCount": sales, "storeRating": rating, "hasVoice": "有配音" in category_type, "isAnimated": "有动画" in category_type, "coverUrl": images[0] if images else "", "screenshotUrls": images[1:]}


def release_date(value: str | None) -> str | None:
    match = re.search(r"(\d{4})年\s*(\d{1,2})月\s*(\d{1,2})日", value or "")
    return f"{match.group(1)}-{int(match.group(2)):02d}-{int(match.group(3)):02d}" if match else None


def page_tags(source: str, available: set[Tag]) -> list[dict[str, str]]:
    values: list[Tag] = []
    for value in re.findall(r"<a\b[^>]*>([\s\S]*?)</a>", element_by_id(source, "category_type"), re.IGNORECASE):
        tag = Tag("WorkForm", strip_html(value).strip())
        if tag in available:
            values.append(tag)
    for value in re.findall(r"<a\b[^>]*>([\s\S]*?)</a>", table_html(source, "分类") or "", re.IGNORECASE):
        candidates = [tag for tag in available if tag.name == strip_html(value).strip() and tag.type != "WorkForm"]
        if len(candidates) == 1:
            values.append(candidates[0])
    return [{"type": tag.type, "name": tag.name} for tag in dict.fromkeys(values)]


def download(url: str, destination: Path) -> Path:
    for attempt in range(2):
        try:
            destination.parent.mkdir(parents=True, exist_ok=True)
            temporary = destination.with_suffix(destination.suffix + ".part")
            temporary.write_bytes(request_bytes(url))
            temporary.replace(destination)
            return destination
        except FileNotFoundError:
            if attempt == 1:
                raise
            time.sleep(1)
    raise RuntimeError(f"无法保存图片：{destination}")


def suffix_for(url: str) -> str:
    suffix = Path(urllib.parse.urlparse(url).path).suffix.lower()
    return suffix if suffix in {".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"} else ".jpg"


def translate_title(original: str, enabled: bool) -> str:
    return translate_text(original, None, enabled)


def relay_endpoint(app_root: Path) -> str | None:
    settings_path = app_root / "Data" / "settings.json"
    try:
        settings = json.loads(settings_path.read_text(encoding="utf-8"))
        health_url = str(settings.get("translationRelay", {}).get("healthUrl", "")).strip()
        if not health_url:
            return None
        parsed = urllib.parse.urlparse(health_url)
        if not parsed.scheme or not parsed.netloc:
            return None
        path = re.sub(r"/health/?$", "/translate", parsed.path or "/health")
        return urllib.parse.urlunparse(parsed._replace(path=path, query=""))
    except (OSError, ValueError, TypeError):
        return None


def translate_by_relay(text: str, endpoint: str) -> str:
    translated: list[str] = []
    for line in text.splitlines(keepends=True):
        ending = "\n" if line.endswith("\n") else ""
        body = line.rstrip("\n")
        if not body:
            translated.append(ending)
            continue
        for start in range(0, len(body), 200):
            query = urllib.parse.urlencode({"text": body[start:start + 200], "from": "ja", "to": "zh-CN"})
            request = urllib.request.Request(f"{endpoint}?{query}", headers={"User-Agent": USER_AGENT})
            with urllib.request.urlopen(request, timeout=65) as response:
                if response.status != 200:
                    raise RuntimeError(f"Translation relay returned HTTP {response.status}.")
                translated.append(response.read().decode("utf-8").strip())
        translated.append(ending)
    return "".join(translated).strip()


def translate_text(original: str, relay: str | None, enabled: bool) -> str:
    if not enabled or not original:
        return original
    if relay:
        try:
            return translate_by_relay(original, relay) or original
        except Exception:
            pass
    endpoint = "https://translate.googleapis.com/translate_a/single?" + urllib.parse.urlencode({"client": "gtx", "sl": "ja", "tl": "zh-CN", "dt": "t", "q": original})
    try:
        payload = json.loads(request_bytes(endpoint).decode("utf-8"))
        return "".join(part[0] for part in payload[0] if part and part[0]).strip() or original
    except Exception:
        return original


def needs_refresh(value: str | None, days: int, force: bool) -> bool:
    if force or not value:
        return True
    try:
        updated_at = datetime.fromisoformat(value.replace("Z", "+00:00"))
        # Older database rows were stored without a UTC offset. Treat those values as UTC.
        if updated_at.tzinfo is None:
            updated_at = updated_at.replace(tzinfo=timezone.utc)
        return updated_at <= datetime.now(timezone.utc) - timedelta(days=days)
    except ValueError:
        return True


def fetch_game(game: dict[str, Any], available_tags: set[Tag], media_root: Path, relay: str | None, translate: bool) -> dict[str, Any]:
    url = game.get("storeUrl")
    if not url:
        original_name = str(game.get("originalName") or "").strip()
        if not original_name:
            raise RuntimeError("未填写游戏原名，已跳过 DLsite 匹配。")
        url = product_url(search_product_id(original_name))
    page = request_bytes(url).decode("utf-8", errors="replace")
    original = strip_html(element_by_id(page, "work_name")).strip() or game["originalName"]
    images = slider_images(page)
    if not images:
        raise RuntimeError(f"No slider images found for {game['id']}.")
    folder = media_root / str(game["id"])
    cover = download(images[0], folder / f"cover{suffix_for(images[0])}")
    screenshots: list[Path] = []
    for index, image in enumerate(images[1:], 1):
        try:
            screenshots.append(download(image, folder / f"screenshot-{index:02d}{suffix_for(image)}"))
        except Exception:
            # A single unavailable screenshot should not discard an otherwise valid game update.
            continue
    record: dict[str, Any] = {
        "id": game["id"], "originalName": original, "translatedName": translate_text(original, relay, translate),
        "storeName": "DLsite", "storeUrl": url, "storeId": product_id(url),
        "clubName": table_value(page, "社团名"), "authors": table_value(page, "作者"),
        "illustrators": table_value(page, "插画"), "voiceActors": table_value(page, "声优"),
        "releaseDate": release_date(table_value(page, "发售日")), "description": translate_text(first_six_lines(page) or "", relay, translate),
        "coverPath": str(cover), "screenshotPaths": [str(path) for path in screenshots],
        "hasVoice": "有配音" in strip_html(element_by_id(page, "category_type")),
        "isAnimated": "有动画" in strip_html(element_by_id(page, "category_type")),
    }
    tags = page_tags(page, available_tags)
    if tags:
        record["tags"] = tags
    return {key: value for key, value in record.items() if value is not None}


def main() -> int:
    parser = argparse.ArgumentParser(description="Fetch DLsite metadata and create LocalGameManager bulk-update JSON.")
    parser.add_argument("--app-root", type=Path, default=Path(__file__).resolve().parents[1] / "Release")
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--id", type=int, action="append", dest="ids")
    parser.add_argument("--lookup-original", help="按原名搜索 DLsite，并仅更新由 --id 指定的一条游戏。")
    parser.add_argument("--store-search")
    parser.add_argument("--store-details")
    parser.add_argument("--refresh-days", type=int, default=30)
    parser.add_argument("--force", action="store_true")
    parser.add_argument("--no-translate", action="store_true")
    parser.add_argument("--keep-downloads", action="store_true", help="Keep temporary downloaded images after a successful --apply.")
    parser.add_argument("--apply", action="store_true")
    args = parser.parse_args()
    app_root = args.app_root.resolve()
    relay = relay_endpoint(app_root)
    if args.store_search is not None:
        print(json.dumps({"ok": True, "games": search_store_games(args.store_search)}, ensure_ascii=False)); return 0
    if args.store_details is not None:
        print(json.dumps({"ok": True, "game": store_details(args.store_details.upper(), relay, not args.no_translate)}, ensure_ascii=False)); return 0
    output = (args.output_dir or app_root / "Data" / "metadata-fetch").resolve()
    index = run_cli(app_root, "metadata-index")
    tag_index = run_cli(app_root, "tags-index")
    available_tags = {Tag(item["type"], item["name"]) for item in tag_index["tags"]}
    if args.lookup_original:
        games = [game for game in index["games"] if args.ids and game["id"] in args.ids]
        if len(games) != 1:
            raise RuntimeError("按原名查找需要使用 --id 指定且仅指定一个游戏。")
        found_id = search_product_id(args.lookup_original.strip())
        games[0] = {**games[0], "storeUrl": product_url(found_id), "storeId": found_id}
    else:
        games = [game for game in index["games"] if (not args.ids or game["id"] in args.ids) and needs_refresh(game.get("metadataUpdatedAtUtc"), args.refresh_days, args.force)]
    records: list[dict[str, Any]] = []
    errors: list[dict[str, Any]] = []
    for game in games:
        try:
            records.append(fetch_game(game, available_tags, output / "media", relay, not args.no_translate))
        except Exception as error:
            errors.append({"id": game["id"], "error": str(error)})
        time.sleep(1)
    manifest = output / "bulk-update.json"
    manifest.parent.mkdir(parents=True, exist_ok=True)
    manifest.write_text(json.dumps({"games": records, "errors": errors}, ensure_ascii=False, indent=2), encoding="utf-8")
    result: dict[str, Any] = {"ok": not errors, "manifest": str(manifest), "fetched": len(records), "errors": errors}
    if args.apply and records:
        result["apply"] = run_cli(app_root, "bulk-update", str(manifest), "--apply")
        if not args.keep_downloads:
            shutil.rmtree(output / "media", ignore_errors=True)
            result["temporaryImagesDeleted"] = True
    print(json.dumps(result, ensure_ascii=False, indent=2))
    # Individual games may be missing from DLsite. Their errors are reported in the JSON result,
    # while successfully fetched games are still applied.
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as error:
        print(json.dumps({"ok": False, "error": str(error)}, ensure_ascii=False), file=sys.stderr)
        raise SystemExit(1)
