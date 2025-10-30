# survey_parser.py
import pdfplumber
import pytesseract
from PIL import Image, ImageOps, ImageFilter
import os, io, json, sys

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8")
    sys.stderr.reconfigure(encoding="utf-8")
# 필요하면 주석 풀기
# pytesseract.pytesseract.tesseract_cmd = r"C:\Program Files\Tesseract-OCR\tesseract.exe"

def extract_pages(pdf_path):
    pages = []
    with pdfplumber.open(pdf_path) as pdf:
        for i, page in enumerate(pdf.pages):
            text = page.extract_text() or ""
            tables_info = []
            try:
                tables = page.find_tables()
            except Exception:
                tables = []
            for t in tables:
                ocr_text = ""
                try:
                    cropped = page.within_bbox(t.bbox)
                    img = cropped.to_image(resolution=300)
                    buf = io.BytesIO()
                    img.save(buf, format="PNG")
                    buf.seek(0)
                    pil_img = Image.open(buf)
                    pil_img = pil_img.convert("L")
                    pil_img = ImageOps.autocontrast(pil_img)
                    pil_img = pil_img.filter(ImageFilter.MedianFilter())
                    ocr_text = pytesseract.image_to_string(pil_img, lang="kor+eng")
                except Exception:
                    ocr_text = ""
                tables_info.append({
                    "bbox": list(t.bbox),
                    "ocr_text": ocr_text
                })
            pages.append({
                "page": i + 1,
                "text": text,
                "tables": tables_info
            })
    return pages


def main():
    if len(sys.argv) < 2:
        out = {
            "status": "error",
            "error": "pdf path not provided",
            "pages": []
        }
        print(json.dumps(out, ensure_ascii=False))
        return

    pdf_path = sys.argv[1]

    if not os.path.exists(pdf_path):
        out = {
            "status": "error",
            "error": f"file not found: {pdf_path}",
            "pages": []
        }
        print(json.dumps(out, ensure_ascii=False))
        return

    try:
        pages = extract_pages(pdf_path)
        out = {
            "status": "ok",
            "pdf_path": pdf_path,
            "pages": pages
        }
        # 유니티가 이 한 줄만 받도록
        print(json.dumps(out, ensure_ascii=False))
    except Exception as e:
        out = {
            "status": "error",
            "error": str(e),
            "pages": []
        }
        print(json.dumps(out, ensure_ascii=False))


if __name__ == "__main__":
    # 표준 출력이 ascii로 묶여 있으면 여기서라도 한 번 설정
    if sys.stdout.encoding is None:
        # 대충 넘어감
        pass
    main()
