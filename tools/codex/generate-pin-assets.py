"""Generate the collection pin sprite sheet through the configured Images API."""
import base64
import json
import os
from pathlib import Path
import urllib.error
import urllib.request


PROMPT = """Create a minimal UI sprite sheet on a genuinely transparent background.
Exactly two matching upright pushpin icons, evenly spaced horizontally:
left: hollow outline pushpin (unpinned); right: filled silhouette pushpin (pinned).
Same shape, size and alignment. Flat white monochrome, clean thick outline,
simple geometric head and pointed stem, easily recognizable at 24 pixels.
Each icon centered within its own half of the canvas, with generous clear margins.
No text, border, shadow, glow, texture, ornaments, perspective or extra objects.
This is a quiet utility icon for a stone-and-copper game interface."""


def main():
    key = os.environ.get("OPENAI_API_KEY")
    base = os.environ.get("OPENAI_BASE_URL")
    model = os.environ.get("IMAGE_MODEL")
    if not all((key, base, model)):
        raise SystemExit("OPENAI_API_KEY, OPENAI_BASE_URL and IMAGE_MODEL are required.")
    endpoint = base.rstrip("/") + "/images/generations"
    body = json.dumps({"model": model, "prompt": PROMPT, "n": 1,
                       "size": "1024x1024"}).encode("utf-8")
    request = urllib.request.Request(endpoint, data=body, headers={
        "Authorization": "Bearer " + key, "Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(request, timeout=240) as response:
            result = json.load(response)
    except urllib.error.HTTPError as error:
        raise SystemExit("Images API HTTP " + str(error.code)) from None
    items = result.get("data") or []
    if not items:
        raise SystemExit("Images API returned no image data.")
    item = items[0]
    if item.get("b64_json"):
        raw = base64.b64decode(item["b64_json"], validate=True)
    elif item.get("url"):
        with urllib.request.urlopen(item["url"], timeout=120) as response:
            raw = response.read()
    else:
        raise SystemExit("Images API returned neither base64 nor a URL.")
    output = Path("data/production-tree-20261013/pin-generated.png")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_bytes(raw)
    output.with_suffix(".prompt.txt").write_text(PROMPT, encoding="utf-8")
    print("Saved generated pin sheet:", output)


if __name__ == "__main__":
    main()
