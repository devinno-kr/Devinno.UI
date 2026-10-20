"""Font Awesome 폰트 파일에서 아이콘 이름표(src/Devinno.UI/Resources/fa-icons.txt)를 만든다.

FA 폰트는 글리프 이름 = 아이콘 이름(예: 0xf001 = "music")이라 손으로 옮긴 표 없이 폰트에서 직접 뽑는다.
폰트를 새 버전으로 바꾸면 이 스크립트만 다시 돌린다.

    python tools/gen_fa_icons.py        (fontTools 필요: pip install fonttools)

출력 한 줄 = "<스타일> <이름> <코드(16진)>", 스타일 s=solid r=regular b=brands.
한 이름이 여러 코드에 매핑되면 사설 영역(U+E000~) 코드 중 가장 작은 것, 없으면 가장 작은 코드.
"""
import collections
import os

from fontTools.ttLib import TTFont

HERE = os.path.dirname(os.path.abspath(__file__))
RES = os.path.join(HERE, "..", "src", "Devinno.UI", "Resources")
FONTS = [("s", "fa-solid.otf"), ("r", "fa-regular.otf"), ("b", "fa-brands.otf")]
SKIP = {"zero-width-space"}


def main():
    lines = []
    for style, file in FONTS:
        by_name = collections.defaultdict(list)
        for code, name in TTFont(os.path.join(RES, file)).getBestCmap().items():
            if name not in SKIP:
                by_name[name].append(code)
        for name in sorted(by_name):
            codes = sorted(by_name[name])
            pua = [c for c in codes if c >= 0xE000]
            lines.append(f"{style} {name} {(pua or codes)[0]:x}")

    with open(os.path.join(RES, "fa-icons.txt"), "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines) + "\n")
    print(f"{len(lines)} icons")


if __name__ == "__main__":
    main()
