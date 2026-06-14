#!/usr/bin/env python3
"""Batch crawl OCC research URLs with Crawl4AI."""
from __future__ import annotations

import asyncio
import json
import re
import sys
from pathlib import Path
from urllib.parse import urlparse

from crawl4ai import AsyncWebCrawler, BrowserConfig, CacheMode, CrawlerRunConfig

TOPICS: dict[str, list[str]] = {
    "01-ops-dashboard": [
        "https://support.zendesk.com/hc/en-us/articles/4408886248346-Working-with-tickets-in-the-agent-workspace",
        "https://www.intercom.com/help/en/articles/2026249-the-inbox",
        "https://help.front.com/en/articles/2059",
        "https://linear.app/docs/triage",
    ],
    "02-winui-ux": [
        "https://learn.microsoft.com/en-us/windows/apps/design/accessibility/accessibility-overview",
        "https://learn.microsoft.com/en-us/windows/api/winrt/microsoft.ui.xaml.automation.automationproperties",
        "https://learn.microsoft.com/en-us/windows/apps/design/controls/items-repeater",
        "https://learn.microsoft.com/en-us/windows/api/winrt/microsoft.ui.xaml.controls.listview.isitemclickenabled",
    ],
    "03-sla-kpi": [
        "https://support.zendesk.com/hc/en-us/articles/4408824760858-About-SLA-policies-and-metrics",
        "https://support.freshdesk.com/support/solutions/articles/37630-understanding-sla-policies",
        "https://www.helpscout.com/helpu/average-response-time/",
    ],
    "04-message-preview": [
        "https://www.nngroup.com/articles/list-ui-view-extra-info/",
        "https://m3.material.io/components/cards/guidelines",
        "https://support.google.com/mail/answer/9261412",
    ],
    "05-historical-analytics": [
        "https://grafana.com/docs/grafana/latest/dashboards/use-dashboards/",
        "https://docs.mixpanel.com/docs/reports/apps/insights",
        "https://cloud.google.com/looker/docs/filters-and-filter-expressions",
    ],
    "06-ollama-ai-ux": [
        "https://github.com/ollama/ollama/blob/main/docs/api.md",
        "https://www.nngroup.com/articles/response-times-3-important-limits/",
        "https://pair.withgoogle.com/guidebook/chapters",
    ],
    "07-webview2-nav": [
        "https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft_web_webview2_core_corewebview2_executescriptasync",
        "https://learn.microsoft.com/en-us/microsoft-edge/webview2/reference/winrt/microsoft_web_webview2_core_corewebview2navigationcompletedeventargs",
        "https://learn.microsoft.com/en-us/microsoft-edge/webview2/concepts/windowed-vs-visual-hosting",
    ],
}


def slug_from_url(url: str) -> str:
    parsed = urlparse(url)
    host = parsed.netloc.replace(":", "-")
    path = parsed.path.strip("/").replace("/", "-") or "index"
    slug = re.sub(r"[^a-zA-Z0-9._-]+", "-", f"{host}-{path}").strip("-")
    return slug[:100] or "page"


async def crawl_one(crawler: AsyncWebCrawler, url: str, topic: str, out_dir: Path) -> dict:
    run_config = CrawlerRunConfig(cache_mode=CacheMode.BYPASS, page_timeout=60000)
    result = await crawler.arun(url=url, config=run_config)
    slug = slug_from_url(url)
    md_path = out_dir / topic / f"{slug}.md"
    json_path = out_dir / topic / f"{slug}.json"
    md_path.parent.mkdir(parents=True, exist_ok=True)

    if not result.success:
        payload = {"url": url, "topic": topic, "success": False, "error": result.error_message}
        json_path.write_text(json.dumps(payload, indent=2), encoding="utf-8")
        return payload

    markdown = result.markdown
    if hasattr(markdown, "fit_markdown") and markdown.fit_markdown:
        text = markdown.fit_markdown
    elif hasattr(markdown, "raw_markdown") and markdown.raw_markdown:
        text = markdown.raw_markdown
    else:
        text = str(markdown)

    payload = {
        "url": url,
        "topic": topic,
        "success": True,
        "title": getattr(result, "title", None),
        "char_count": len(text),
        "markdown_preview": text[:4000],
        "full_markdown": text,
    }
    md_path.write_text(text, encoding="utf-8")
    summary = {k: v for k, v in payload.items() if k != "full_markdown"}
    summary["markdown_preview"] = text[:2000]
    json_path.write_text(json.dumps(summary, indent=2), encoding="utf-8")
    return payload


async def main(out_dir: Path) -> int:
    browser_config = BrowserConfig(headless=True, verbose=False)
    results: list[dict] = []
    async with AsyncWebCrawler(config=browser_config) as crawler:
        for topic, urls in TOPICS.items():
            for url in urls:
                print(f"crawling [{topic}] {url}", flush=True)
                try:
                    payload = await crawl_one(crawler, url, topic, out_dir)
                except Exception as exc:
                    payload = {"url": url, "topic": topic, "success": False, "error": str(exc)}
                results.append(payload)
                status = "OK" if payload.get("success") else f"FAIL: {payload.get('error', '?')}"
                print(f"  -> {status}", flush=True)

    index_path = out_dir / "index.json"
    index_path.write_text(json.dumps(results, indent=2, default=str), encoding="utf-8")
    ok = sum(1 for r in results if r.get("success"))
    print(f"\nDone: {ok}/{len(results)} succeeded -> {index_path}")
    return 0 if ok > len(results) // 2 else 1


if __name__ == "__main__":
    out = Path(sys.argv[1]) if len(sys.argv) > 1 else Path(".crawl4ai/occ-research")
    raise SystemExit(asyncio.run(main(out)))
