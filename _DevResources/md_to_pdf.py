from pathlib import Path
import markdown
from weasyprint import HTML

# Paths
md_path = Path(r"C:\Users\cbxjy\Projects\BIM _net56\OperatorManual.md")
pdf_path = Path(r"C:\Users\cbxjy\Projects\BIM _net56\OperatorManual.pdf")

md_text = md_path.read_text(encoding="utf-8")
html = markdown.markdown(md_text, extensions=["extra", "tables", "sane_lists"])

# Render PDF
HTML(string=html, base_url=str(md_path.parent)).write_pdf(str(pdf_path))
print(f"Saved: {pdf_path}")