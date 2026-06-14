#!/usr/bin/env python3
"""Deep crawl a site section using Crawl4AI (Firecrawl crawl fallback)."""

from __future__ import annotations

import argparse
import asyncio
import json
import re
import sys
from pathlib import Path
from urllib.parse import urlparse

from crawl4ai import AsyncWebCrawler, BrowserConfig, CacheMode, CrawlerRunConfig
from crawl4ai.deep_crawling import BFSDeepCrawlStrategy


def slug_from_url(url: str) -> str:
    parsed = urlparse(url)
    host = parsed.netloc.replace(":", "-")
    path = parsed.path.strip("/").replace("/", "-") or "index"
    slug = re.sub(r"[^a-zA-Z0-9._-]+", "-", f"{host}-{path}").strip("-")
    return slug[:80] or "crawl"


async def deep_crawl(url: str, max_pages: int, max_depth: int, output_dir: Path) -> list[dict]:
    output_dir.mkdir(parents=True, exist_ok=True)

    strategy = BFSDeepCrawlStrategy(max_depth=max_depth, max_pages=max_pages, include_external=False)
    browser_config = BrowserConfig(headless=True, verbose=False)
    run_config = CrawlerRunConfig(
        cache_mode=CacheMode.BYPASS,
        deep_crawl_strategy=strategy,
    )

    pages: list[dict] = []
    async with AsyncWebCrawler(config=browser_config) as crawler:
        results = await crawler.arun(url=url, config=run_config)
        if not isinstance(results, list):
            results = [results]

        for idx, result in enumerate(results, start=1):
            if not result.success:
                continue
            markdown = result.markdown
            text = getattr(markdown, "fit_markdown", None) or getattr(markdown, "raw_markdown", None) or str(markdown)
            page_url = getattr(result, "url", url)
            slug = slug_from_url(page_url)
            md_path = output_dir / f"{idx:03d}-{slug}.md"
            md_path.write_text(text, encoding="utf-8")
            pages.append({"url": page_url, "file": str(md_path), "chars": len(text)})

    index_path = output_dir / "index.json"
    index_path.write_text(json.dumps({"seed": url, "pages": pages}, indent=2), encoding="utf-8")
    return pages


def main() -> int:
    parser = argparse.ArgumentParser(description="Deep crawl with Crawl4AI")
    parser.add_argument("url", help="Seed URL")
    parser.add_argument("--max-pages", type=int, default=10)
    parser.add_argument("--max-depth", type=int, default=2)
    parser.add_argument("-o", "--output-dir", help="Output directory")
    args = parser.parse_args()

    out = Path(args.output_dir) if args.output_dir else Path(".crawl4ai") / f"crawl-{slug_from_url(args.url)}"

    try:
        pages = asyncio.run(deep_crawl(args.url, args.max_pages, args.max_depth, out))
    except Exception as exc:  # noqa: BLE001
        print(f"error: {exc}", file=sys.stderr)
        return 1

    print(f"saved: {out} ({len(pages)} pages)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
