"""Builds reference.docx: the Word styles used for the report (SLIIT-style cover, headings, tables,
A4 page, header and page-number footer). Pandoc copies these into the generated report.

    python3 docs/report/template/make_reference.py
"""
import pathlib
import re
import subprocess
import tempfile
import zipfile

HERE = pathlib.Path(__file__).resolve().parent
NAVY, ORANGE, GREY = "1B2A6B", "F58220", "BFBFBF"
FONT = "Calibri"


def para_style(sid, name, ppr="", rpr="", based="Normal", custom=True, nxt="BodyText"):
    return (f'<w:style w:type="paragraph"{" w:customStyle=\"1\"" if custom else ""} w:styleId="{sid}">'
            f'<w:name w:val="{name}"/><w:basedOn w:val="{based}"/><w:next w:val="{nxt}"/><w:qFormat/>'
            f'<w:pPr>{ppr}</w:pPr><w:rPr>{rpr}</w:rPr></w:style>')


def heading(level, size, color, extra_ppr=""):
    return para_style(
        f"Heading{level}", f"heading {level}",
        f'<w:keepNext/><w:keepLines/>{extra_ppr}<w:outlineLvl w:val="{level - 1}"/>',
        f'<w:rFonts w:ascii="{FONT}" w:hAnsi="{FONT}" w:cs="{FONT}"/><w:b/><w:bCs/>'
        f'<w:color w:val="{color}"/><w:sz w:val="{size}"/><w:szCs w:val="{size}"/>',
        custom=False)


STYLES = {
    "Heading1": heading(1, 36, NAVY,
                        '<w:pageBreakBefore/><w:pBdr><w:bottom w:val="single" w:sz="12" w:space="4" '
                        f'w:color="{ORANGE}"/></w:pBdr><w:spacing w:before="0" w:after="240"/>'),
    "Heading2": heading(2, 28, NAVY, '<w:spacing w:before="320" w:after="120"/>'),
    "Heading3": heading(3, 24, "C25E00", '<w:spacing w:before="240" w:after="80"/>'),
    "Heading4": heading(4, 22, NAVY, '<w:spacing w:before="200" w:after="60"/>'),
    "BodyText": para_style("BodyText", "Body Text", '<w:spacing w:before="0" w:after="140" w:line="276" '
                           'w:lineRule="auto"/><w:jc w:val="both"/>', custom=False),
    "Compact": para_style("Compact", "Compact", '<w:spacing w:before="20" w:after="20"/>',
                          '<w:sz w:val="19"/><w:szCs w:val="19"/>', based="BodyText"),
    "TOCHeading": para_style("TOCHeading", "TOC Heading", '<w:spacing w:before="0" w:after="240"/>',
                             f'<w:b/><w:color w:val="{NAVY}"/><w:sz w:val="36"/><w:szCs w:val="36"/>',
                             custom=False),
    "Table": (
        '<w:style w:type="table" w:default="1" w:styleId="Table"><w:name w:val="Table"/>'
        '<w:basedOn w:val="TableNormal"/><w:qFormat/>'
        '<w:pPr><w:spacing w:before="0" w:after="0"/></w:pPr>'
        '<w:tblPr><w:tblInd w:w="0" w:type="dxa"/>'
        f'<w:tblBorders><w:top w:val="single" w:sz="4" w:color="{GREY}"/><w:left w:val="single" w:sz="4" w:color="{GREY}"/>'
        f'<w:bottom w:val="single" w:sz="4" w:color="{GREY}"/><w:right w:val="single" w:sz="4" w:color="{GREY}"/>'
        f'<w:insideH w:val="single" w:sz="4" w:color="{GREY}"/><w:insideV w:val="single" w:sz="4" w:color="{GREY}"/></w:tblBorders>'
        '<w:tblCellMar><w:top w:w="40" w:type="dxa"/><w:left w:w="90" w:type="dxa"/>'
        '<w:bottom w:w="40" w:type="dxa"/><w:right w:w="90" w:type="dxa"/></w:tblCellMar></w:tblPr>'
        f'<w:tblStylePr w:type="firstRow"><w:rPr><w:b/><w:color w:val="FFFFFF"/></w:rPr>'
        f'<w:tcPr><w:shd w:val="clear" w:color="auto" w:fill="{NAVY}"/><w:vAlign w:val="bottom"/></w:tcPr></w:tblStylePr>'
        '</w:style>'),
}

COVER = {
    "CoverInstitute": ("Cover Institute", 32, NAVY, True, "1200", "360"),
    "CoverLogo": ("Cover Logo", 22, NAVY, False, "120", "480"),
    "CoverModule": ("Cover Module", 30, NAVY, True, "120", "120"),
    "CoverTitle": ("Cover Title", 40, NAVY, True, "240", "120"),
    "CoverSubtitle": ("Cover Subtitle", 28, ORANGE, True, "120", "600"),
    "CoverText": ("Cover Text", 24, "333333", False, "60", "60"),
}
cover_xml = "".join(
    para_style(sid, name, f'<w:jc w:val="center"/><w:spacing w:before="{b}" w:after="{a}"/>',
               ("<w:b/><w:bCs/>" if bold else "") + f'<w:color w:val="{col}"/><w:sz w:val="{sz}"/><w:szCs w:val="{sz}"/>')
    for sid, (name, sz, col, bold, b, a) in COVER.items())

HEADER = f"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:hdr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:p><w:pPr>
<w:pBdr><w:bottom w:val="single" w:sz="6" w:space="4" w:color="{ORANGE}"/></w:pBdr>
<w:tabs><w:tab w:val="right" w:pos="9300"/></w:tabs><w:spacing w:after="0"/></w:pPr>
<w:r><w:rPr><w:color w:val="{NAVY}"/><w:sz w:val="18"/></w:rPr><w:t xml:space="preserve">SE3090 – Software Engineering Frameworks | Assignment 1</w:t></w:r>
<w:r><w:rPr><w:color w:val="{NAVY}"/><w:sz w:val="18"/></w:rPr><w:tab/><w:t>IT22566102</w:t></w:r></w:p></w:hdr>"""

FOOTER = f"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:ftr xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"><w:p><w:pPr><w:jc w:val="center"/>
<w:spacing w:after="0"/></w:pPr>
<w:r><w:rPr><w:color w:val="595959"/><w:sz w:val="18"/></w:rPr><w:t xml:space="preserve">Page </w:t></w:r>
<w:r><w:rPr><w:color w:val="595959"/><w:sz w:val="18"/></w:rPr><w:fldChar w:fldCharType="begin"/></w:r>
<w:r><w:rPr><w:color w:val="595959"/><w:sz w:val="18"/></w:rPr><w:instrText xml:space="preserve"> PAGE </w:instrText></w:r>
<w:r><w:rPr><w:color w:val="595959"/><w:sz w:val="18"/></w:rPr><w:fldChar w:fldCharType="separate"/></w:r>
<w:r><w:rPr><w:color w:val="595959"/><w:sz w:val="18"/></w:rPr><w:t>1</w:t></w:r>
<w:r><w:rPr><w:color w:val="595959"/><w:sz w:val="18"/></w:rPr><w:fldChar w:fldCharType="end"/></w:r>
</w:p></w:ftr>"""

SECTPR = ('<w:sectPr><w:headerReference w:type="default" r:id="rIdHdr1"/>'
          '<w:footerReference w:type="default" r:id="rIdFtr1"/>'
          '<w:footnotePr><w:numRestart w:val="eachSect"/></w:footnotePr>'
          '<w:pgSz w:w="11906" w:h="16838"/>'
          '<w:pgMar w:top="1300" w:right="1250" w:bottom="1300" w:left="1250" w:header="600" w:footer="600" w:gutter="0"/>'
          '<w:titlePg/></w:sectPr>')


def main():
    with tempfile.TemporaryDirectory() as tmp:
        default = pathlib.Path(tmp) / "default.docx"
        with open(default, "wb") as f:
            subprocess.run(["pandoc", "--print-default-data-file", "reference.docx"], stdout=f, check=True)
        files = {}
        with zipfile.ZipFile(default) as z:
            for n in z.namelist():
                files[n] = z.read(n)

    styles = files["word/styles.xml"].decode()
    styles = re.sub(r'<w:rFonts w:asciiTheme="minorHAnsi"[^>]*/>',
                    f'<w:rFonts w:ascii="{FONT}" w:eastAsia="{FONT}" w:hAnsi="{FONT}" w:cs="{FONT}"/>', styles, count=1)
    styles = styles.replace('<w:sz w:val="24" />\n        <w:szCs w:val="24" />',
                            '<w:sz w:val="22" />\n        <w:szCs w:val="22" />', 1)
    for sid, xml in STYLES.items():
        styles, n = re.subn(rf'<w:style [^>]*w:styleId="{sid}">.*?</w:style>', xml, styles, count=1, flags=re.S)
        if not n:
            styles = styles.replace("</w:styles>", xml + "</w:styles>")
    styles = styles.replace("</w:styles>", cover_xml + "</w:styles>")
    files["word/styles.xml"] = styles.encode()

    doc = files["word/document.xml"].decode()
    doc = re.sub(r"<w:sectPr>.*?</w:sectPr>", SECTPR, doc, flags=re.S)
    files["word/document.xml"] = doc.encode()

    rels = files["word/_rels/document.xml.rels"].decode()
    rels = rels.replace("</Relationships>",
                        '<Relationship Id="rIdHdr1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/header" Target="header1.xml"/>'
                        '<Relationship Id="rIdFtr1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/footer" Target="footer1.xml"/>'
                        "</Relationships>")
    files["word/_rels/document.xml.rels"] = rels.encode()
    ct = files["[Content_Types].xml"].decode()
    ct = ct.replace("</Types>",
                    '<Override PartName="/word/header1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.header+xml"/>'
                    '<Override PartName="/word/footer1.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.footer+xml"/>'
                    "</Types>")
    files["[Content_Types].xml"] = ct.encode()
    files["word/header1.xml"] = HEADER.encode()
    files["word/footer1.xml"] = FOOTER.encode()

    out = HERE / "reference.docx"
    with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
        for n, data in files.items():
            z.writestr(n, data)
    print(out)


if __name__ == "__main__":
    main()
