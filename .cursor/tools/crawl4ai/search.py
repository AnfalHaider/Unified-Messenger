#!/usr/bin/env python3
"""Web search fallback when Firecrawl credits are exhausted."""

from __future__ import annotations

import argparse
import json
import re
import subprocess
import sys
from pathlib import Path
from urllib.parse import quote_plus


def slugify(query: str) -> str:
    slug = re.sub(r"[^a-zA-Z0-9._-]+", "-", query.lower()).strip("-")
    return slug[:80] or "search"


def duckduckgo_search(query: str, limit: int) -> list[dict]:
    try:
        from ddgs import DDGS
    except ImportError:
        subprocess.check_call([sys.executable, "-m", "pip", "install", "ddgs"])
        from ddgs import DDGS

    results: list[dict] = []
    with DDGS() as ddgs:
        for item in ddgs.text(query, max_results=limit):
            results.append(
                {
                    "title": item.get("title"),
                    "url": item.get("href"),
                    "snippet": item.get("body"),
                }
            )
    return results


def main() -> int:
    parser = argparse.ArgumentParser(description="Search the web (Crawl4AI fallback)")
    parser.add_argument("query", help="Search query")
    parser.add_argument("--limit", type=int, default=5)
    parser.add_argument("-o", "--output", help="Output JSON path")
    parser.add_argument("--scrape", action="store_true", help="Scrape top results to markdown")
    args = parser.parse_args()

    out_dir = Path(".crawl4ai")
    out_dir.mkdir(parents=True, exist_ok=True)
    output = Path(args.output) if args.output else out_dir / f"search-{slugify(args.query)}.json"

    try:
        web = duckduckgo_search(args.query, args.limit)
    except Exception as exc:  # noqa: BLE001
        print(f"error: {exc}", file=sys.stderr)
        return 1

    payload = {"query": args.query, "provider": "duckduckgo", "data": {"web": web}}
    output.write_text(json.dumps(payload, indent=2), encoding="utf-8")
    print(f"saved: {output} ({len(web)} results)")

    if args.scrape and web:
        scrape_script = Path(__file__).with_name("scrape.py")
        for idx, hit in enumerate(web[: min(3, len(web))], start=1):
            url = hit.get("url")
            if not url:
                continue
            target = out_dir / f"search-{slugify(args.query)}-{idx}.md"
            subprocess.run(
                [sys.executable, str(scrape_script), url, "-o", str(target)],
                check=False,
            )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
