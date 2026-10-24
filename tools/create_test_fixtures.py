"""Optional independent PDF fixtures. Requires reportlab and pypdf."""
from pathlib import Path
from reportlab.pdfgen import canvas
from pypdf import PdfReader, PdfWriter
from pypdf.constants import UserAccessPermissions as P

root = Path(__file__).resolve().parent.parent / "tests" / "fixtures"
root.mkdir(parents=True, exist_ok=True)
c = canvas.Canvas(str(root / "external.pdf"), pagesize=(612, 792))
c.setFont("Helvetica", 18)
c.drawString(50, 730, "Original office text")
c.setFont("Helvetica", 12)
c.drawString(50, 690, "Find this word. Find this word again.")
c.showPage()
c.setFont("Helvetica", 16)
c.drawString(50, 730, "External second page")
c.save()
w = PdfWriter(clone_from=str(root / "external.pdf"))
w.encrypt("reader", "owner", permissions_flag=P.PRINT)
with (root / "restricted.pdf").open("wb") as f:
    w.write(f)
w = PdfWriter(clone_from=str(root / "external.pdf"))
w.encrypt("reader", "owner")
with (root / "password.pdf").open("wb") as f:
    w.write(f)
print("Independent PDF fixtures ready:", root)
