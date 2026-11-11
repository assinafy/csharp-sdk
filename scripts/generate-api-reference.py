"""Regenerate HTTP payload sections from docs/openapi.json; preserve SDK-specific text."""
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
SPEC_PATH = ROOT / "docs/openapi.json"
REFERENCE = ROOT / "docs/API.md"
METHODS = {"get", "post", "put", "patch", "delete", "head", "options"}


def clean(value):
    if isinstance(value, dict):
        return {key: clean(item) for key, item in value.items()}
    if isinstance(value, list):
        return [clean(item) for item in value]
    if isinstance(value, str):
        return re.sub(r"\n\n\[Copy as Markdown\].*", "", value, flags=re.S)
    return value


def generate(spec, previous):
    spec = clean(spec)
    operations = [(path, method, operation) for path, item in spec["paths"].items()
                  for method, operation in item.items() if method in METHODS]
    prefix = previous.split("## Official integration guidance", 1)[0]
    prefix = re.sub(r"Snapshot SHA-256: `[^`]+`",
                    f"Snapshot SHA-256: `{hashlib.sha256(SPEC_PATH.read_bytes()).hexdigest()}`", prefix)
    prefix = re.sub(r"API surface: .*", f"API surface: {len(operations)} documented production operations across "
                    f"{len(spec['paths'])} paths, {len(spec['components']['schemas'])} component schemas, "
                    f"and {len(spec['webhooks'])} webhook event payloads.", prefix)
    manual = "## SDK method index" + previous.split("## SDK method index", 1)[1].split(
        "## Complete production operation matrix", 1)[0]
    lines = [prefix.rstrip(), "", "## Official integration guidance", ""]
    for tag in spec["tags"]:
        lines.extend([f"### {tag['name']}", "", tag.get("description", ""), ""])
    lines.extend([manual.rstrip(), "", "## Complete production operation matrix", "",
                  "| Tag | Method | Path | Request body | Responses | Security |",
                  "|---|---|---|---|---|---|"])
    for path, method, operation in operations:
        auth = " or ".join(" + ".join(item) or "public" for item in operation.get("security", [])) or "public"
        body = ", ".join(operation.get("requestBody", {}).get("content", {})) or "none"
        lines.append(f"| {', '.join(operation.get('tags', []))} | {method.upper()} | `{path}` | "
                     f"{body} | {', '.join(operation['responses'])} | {auth} |")
    lines.extend(["", "## Full operation request and response payloads", "",
                  "All schemas, parameters, examples, security requirements, and response codes are preserved below. "
                  "JSON references resolve against the component sections in this document and the OpenAPI snapshot.", ""])

    def block(heading, value):
        lines.extend([heading, "", "```json", json.dumps(value, indent=2, ensure_ascii=False), "```", ""])

    for path, method, operation in operations:
        value = dict(operation)
        if spec["paths"][path].get("parameters"):
            value["parameters"] = spec["paths"][path]["parameters"] + value.get("parameters", [])
        block(f"### {method.upper()} {path}", value)
    for name, schema in spec["components"]["schemas"].items():
        block(f"### Schema: {name}", schema)
    lines.extend(["## Webhook event payloads", "", "<a id=\"webhook-payloads\"></a>", "",
                  "Deliveries are HTTP POST bodies, not REST envelopes. Verify the Standard Webhooks headers "
                  "against the raw body before deserialization.", ""])
    for name, event in spec["webhooks"].items():
        block(f"### Event `{name}`", event)
    lines.extend(["## Shared error response payloads", ""])
    for name, response in spec["components"]["responses"].items():
        block(f"### Shared {name}", response)
    for category in ("parameters", "examples", "securitySchemes"):
        lines.extend([f"## Shared {category}", ""])
        for name, value in spec["components"].get(category, {}).items():
            block(f"### {category}: {name}", value)
    result = "\n".join(lines).rstrip() + "\n"
    assert sum(line.startswith("### ") and line.split(" ")[1].lower() in METHODS
               for line in result.splitlines()) >= len(operations)
    assert "[Open in ChatGPT]" not in result
    return result


if __name__ == "__main__":
    specification = json.loads(SPEC_PATH.read_text())
    output = generate(specification, REFERENCE.read_text())
    assert generate(specification, output) == output, "Generation must be idempotent"
    REFERENCE.write_text(output)
