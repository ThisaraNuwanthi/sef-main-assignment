"""Builds the consolidated report (Word) from report.md.

    python3 docs/report/build.py

Needs pandoc. Mermaid diagrams are rendered to PNG with the public mermaid.ink service.
Output: docs/report/build/SE3090_IT22566102_Report.docx (git-ignored).
"""
import base64
import os
import pathlib
import re
import subprocess
import urllib.request

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[1]
OUT = HERE / "build"
OUT.mkdir(exist_ok=True)


def include(match: re.Match) -> str:
    text = (ROOT / match.group(1).strip()).read_text(encoding="utf-8")
    # Included documents start at "#"; push them one level down so they sit inside a report section.
    text = re.sub(r"^(#+) ", lambda m: "#" + m.group(1) + " ", text, flags=re.M)
    # Links inside the repo don't work in a document; keep the link text only.
    return re.sub(r"\[([^\]]+)\]\((?!https?:)[^)]+\)", r"\1", text)


def mermaid(match: re.Match, counter=[0]) -> str:
    counter[0] += 1
    code = match.group(1)
    png = OUT / f"diagram-{counter[0]}.png"
    if not png.exists():
        key = base64.urlsafe_b64encode(code.encode()).decode()
        req = urllib.request.Request(f"https://mermaid.ink/img/{key}?type=png&bgColor=white&width=1400",
                                     headers={"User-Agent": "report-build"})
        png.write_bytes(urllib.request.urlopen(req, timeout=60).read())
    return f"![]({png.name}){{width=100%}}\n"


md = (HERE / "report.md").read_text(encoding="utf-8")
md = re.sub(r"^<!-- include: (.+?) -->$", include, md, flags=re.M)
# The signature image is kept outside the repository; set SKCA_SIGNATURE to its path.
sig = os.environ.get("SKCA_SIGNATURE")
md = md.replace("{{SIGNATURE}}", f"![]({sig}){{width=1.8in}}" if sig else "____________________")
md = re.sub(r"```mermaid\n(.*?)```\n", mermaid, md, flags=re.S)
source = OUT / "report.full.md"
source.write_text(md, encoding="utf-8")

subprocess.run(["pandoc", source.name, "-o", "SE3090_IT22566102_Report.docx", "--reference-doc", str(HERE / "template" / "reference.docx"),
                "--resource-path", f".:{HERE}/build"], cwd=OUT, check=True)
print(OUT / "SE3090_IT22566102_Report.docx")
