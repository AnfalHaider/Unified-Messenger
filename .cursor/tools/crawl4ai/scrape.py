#!/usr/bin/env python3
"""Scrape a URL to markdown using Crawl4AI (Firecrawl fallback)."""

from __future__ import annotations

import argparse
import asyncio
import json
import re
import sys
from pathlib import Path
from urllib.parse import urlparse

from crawl4ai import AsyncWebCrawler, BrowserConfig, CacheMode, CrawlerRunConfig


def slug_from_url(url: str) -> str:
    parsed = urlparse(url)
    host = parsed.netloc.replace(":", "-")
    path = parsed.path.strip("/").replace("/", "-") or "index"
    slug = re.sub(r"[^a-zA-Z0-9._-]+", "-", f"{host}-{path}").strip("-")
    return slug[:120] or "page"


async def scrape(url: str, output: Path | None, fit: bool) -> dict:
    browser_config = BrowserConfig(headless=True, verbose=False)
    run_config = CrawlerRunConfig(cache_mode=CacheMode.BYPASS)

    async with AsyncWebCrawler(config=browser_config) as crawler:
        result = await crawler.arun(url=url, config=run_config)

    if not result.success:
        raise RuntimeError(result.error_message or "Crawl failed")

    markdown = result.markdown
    if fit and hasattr(markdown, "fit_markdown") and markdown.fit_markdown:
        text = markdown.fit_markdown
    elif hasattr(markdown, "raw_markdown") and markdown.raw_markdown:
        text = markdown.raw_markdown
    else:
        text = str(markdown)

    payload = {
        "url": url,
        "success": True,
        "title": getattr(result, "title", None),
        "markdown": text,
        "links": {
            "internal": list(getattr(result, "links", {}).get("internal", [])[:50]),
            "external": list(getattr(result, "links", {}).get("external", [])[:50]),
        },
    }

    if output:
        output.parent.mkdir(parents=True, exist_ok=True)
        if output.suffix.lower() == ".json":
            output.write_text(json.dumps(payload, indent=2), encoding="utf-8")
        else:
            output.write_text(text, encoding="utf-8")

    return payload


def main() -> int:
    parser = argparse.ArgumentParser(description="Scrape a URL with Crawl4AI")
    parser.add_argument("url", help="URL to scrape")
    parser.add_argument("-o", "--output", help="Output file (.md or .json)")
    parser.add_argument("--fit", action="store_true", help="Use fit_markdown (noise-filtered)")
    args = parser.parse_args()

    output = Path(args.output) if args.output else None
    if output is None:
        output = Path(".crawl4ai") / f"{slug_from_url(args.url)}.md"

    try:
        payload = asyncio.run(scrape(args.url, output, args.fit))
    except Exception as exc:  # noqa: BLE001 - CLI boundary
        print(f"error: {exc}", file=sys.stderr)
        return 1

    print(f"saved: {output} ({len(payload['markdown'])} chars)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
