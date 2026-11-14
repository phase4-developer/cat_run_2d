# 구조 PDF 다시 만들기

`../MoonVillage_CodeStructure.pdf`의 원본이다. 그림은 `diagrams.py`(SVG), 본문·표는 `build.py`에 있다.

```bash
cd Docs/Architecture/pdf_src
py -3 build.py "$PWD/out.html"
"/c/Program Files/Google/Chrome/Application/chrome.exe" --headless=new --disable-gpu --no-pdf-header-footer --print-to-pdf="$(cygpath -w "$PWD/../MoonVillage_CodeStructure.pdf")" "file:///$(cygpath -m $PWD)/out.html"
rm out.html
```

외부 라이브러리 없이 Python 표준 라이브러리와 로컬 Chrome만 쓴다. 구조가 바뀌면 md 문서와 함께 고친다.
