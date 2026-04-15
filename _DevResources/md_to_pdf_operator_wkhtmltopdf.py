from pathlib import Path
import os
import markdown
import pdfkit

# Paths (resolve relative to repo root)
root = Path(__file__).resolve().parents[1]
md_path = root / "OperatorManual.md"
pdf_path = Path(os.environ.get("TEMP", str(root))) / "OperatorManual.pdf"
pdf_path.parent.mkdir(parents=True, exist_ok=True)

md_text = md_path.read_text(encoding="utf-8")
body = markdown.markdown(md_text, extensions=["extra", "tables", "sane_lists"])

html = f"""<!doctype html>
<html lang="ru">
<head>
  <meta charset="utf-8">
  <style>
    body {{ font-family: 'Segoe UI', 'Calibri', 'Arial', sans-serif; font-size: 12pt; line-height: 1.45; }}
    h1, h2, h3 {{ font-family: 'Segoe UI', 'Calibri', 'Arial', sans-serif; }}
    code, pre {{ font-family: 'Segoe UI', 'Calibri', 'Arial', sans-serif; }}
    code {{ background: #f3f3f3; padding: 0 2px; border-radius: 2px; }}
    pre {{ background: #f3f3f3; padding: 8px; border-radius: 4px; }}
    h1 {{ font-size: 20pt; margin: 0 0 12px 0; }}
    h2 {{ font-size: 16pt; margin: 18px 0 8px 0; }}
    h3 {{ font-size: 13pt; margin: 14px 0 6px 0; }}
    p {{ margin: 6px 0; }}
    ul, ol {{ margin: 6px 0 6px 18px; }}
    table {{ border-collapse: collapse; width: 100%; margin: 8px 0; }}
    th, td {{ border: 1px solid #c9c9c9; padding: 6px 8px; vertical-align: top; text-align: left; }}
    hr {{ border: none; border-top: 1px solid #999; margin: 14px 0; }}
  </style>
</head>
<body>
{body}
</body>
</html>"""

options = {
    "encoding": "UTF-8",
    "enable-local-file-access": ""
}

pdfkit.from_string(html, str(pdf_path), options=options)
print(f"Saved: {pdf_path}")
