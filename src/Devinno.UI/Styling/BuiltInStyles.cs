namespace Devinno.UI.Styling;

/// <summary>
/// The class stylesheet that the built-in themes provide by default. All values are theme tokens, so it is shared by <see cref="DvTheme.Light"/>/<see cref="DvTheme.Dark"/>.
/// </summary>
/// <remarks>
/// The engine applies it automatically through the theme, so app code normally does not use it directly; markup just uses class names such as <c>class="card"</c>.
/// When building a theme with extra classes in code, use it to concatenate, as in <c>new DvTheme(tokens, BuiltInStyles.Sheet + 내CSS)</c> (my CSS) (see <see cref="StyleSheet"/>).
/// </remarks>
public static class BuiltInStyles
{
    /// <summary>
    /// The CSS text defining the built-in classes. Included classes:
    /// text <c>.display</c> <c>.heading</c> <c>.title</c> <c>.body</c> <c>.label</c> <c>.caption</c> <c>.mono</c> <c>.muted</c> <c>.subtle</c>;
    /// surfaces <c>.page</c> <c>.card</c> <c>.card-flat</c> <c>.dialog</c> (the dialog frame <c>DvDialogFrame</c>); chip <c>.chip</c>;
    /// buttons <c>.btn</c> <c>.btn-sm</c> <c>.btn-lg</c> <c>.btn-primary</c> <c>.btn-secondary</c> <c>.btn-danger</c> <c>.btn-ghost</c> (implicitly added by the <c>variant</c>/<c>size</c> attributes of <c>DvButton</c>); latching button on <c>.btn-on</c>; checkbox and radio <c>.checkbox</c> <c>.radio</c>; list <c>.listbox</c> <c>.listbox-item</c> <c>.listbox-item-on</c>; collapsible panel <c>.expander</c> <c>.expander-header</c> <c>.expander-body</c>; tree <c>.treeview</c> <c>.treeview-item</c> <c>.treeview-item-on</c>; segment <c>.segment</c> <c>.segment-item</c> <c>.segment-item-on</c>; tabs <c>.tabs</c> <c>.tab-header</c> <c>.tab</c> <c>.tab-on</c> <c>.tab-rule</c> <c>.tab-body</c>;
    /// icon <c>.icon</c>; input box <c>.input</c> <c>.input-readonly</c>; for semantic component internals <c>.stat</c> <c>.stat-accent</c> <c>.stat-body</c> <c>.stat-value</c> <c>.status-item</c> <c>.switch-row</c>.
    /// </summary>
    public const string Sheet = """
        /* 타이포 7클래스 [D §2.3] */
        .display { font-size: token(font.display); font-weight: bold; color: token(text) }
        .heading { font-size: token(font.heading); font-weight: bold; color: token(text) }
        .title   { font-size: token(font.title); font-weight: bold; color: token(text) }
        .body    { font-size: token(font.body); color: token(text) }
        .label   { font-size: token(font.label); color: token(text) }
        .caption { font-size: token(font.caption); color: token(text-muted) }
        .mono    { font-size: token(font.mono); color: token(text) }
        .muted   { color: token(text-muted) }
        .subtle  { color: token(text-subtle) }

        /* 표면 */
        .page      { background: linear-gradient(160deg, token(background), token(background-alt));
                     padding: token(spacing.xl) }
        .card      { background: token(surface); border: 1 token(border); border-radius: token(radius.lg);
                     box-shadow: token(shadow.md); padding: token(spacing.lg) token(spacing.xl) }
        .card-flat { background: token(surface); border: 1 token(border); border-radius: token(radius.lg);
                     padding: token(spacing.lg) }
        .dialog    { background: token(surface-overlay); border: 1 token(border); border-radius: 12;
                     box-shadow: token(shadow.lg); padding: 20 }

        /* 칩 */
        .chip { font-size: token(font.caption); font-weight: bold; padding: 3 9; border-radius: 9;
                color: token(tone-neutral-fg); background: token(tone-neutral-soft) }

        /* 버튼 (DvButton variant="…") */
        .btn          { padding: 11 20; border-radius: token(radius.md); font-size: 14; font-weight: bold;
                        content-align: middle-center }
        .btn-sm       { padding: 7 12; font-size: 12 }
        .btn-lg       { padding: 14 26; font-size: 16 }
        .btn-primary  { background: linear-gradient(135deg, token(primary-press), token(primary-hover));
                        color: token(primary-fg); box-shadow: token(shadow.sm) }
        .btn-primary:hover { background: linear-gradient(135deg, token(primary-hover), token(primary)) }
        .btn-primary:press { background: token(primary-press) }
        .btn-secondary       { background: token(surface-raised); color: token(text); border: 1 token(border-strong) }
        .btn-secondary:hover { background: token(surface-overlay) }
        .btn-danger       { background: token(tone-danger-soft); color: token(tone-danger-fg); border: 1 token(tone-danger) }
        .btn-danger:hover { background: token(tone-danger); color: token(primary-fg) }
        .btn-ghost       { background: transparent; color: token(text-muted) }
        .btn-ghost:hover { background: token(surface-raised); color: token(text) }
        .btn:disabled    { background: token(surface-raised); color: token(text-subtle); border: 0 transparent;
                           box-shadow: 0 0 0 transparent }

        /* 래칭 버튼 (DvButton value="{Bind …}") 켜짐 */
        .btn-on       { background: token(primary); color: token(primary-fg); border: 1 token(primary) }
        .btn-on:hover { background: token(primary-hover); color: token(primary-fg) }
        .btn-on:press { background: token(primary-press) }

        /* 세그먼트 (DvSegment) — 항목은 라이브러리가 만든다 */
        .segment         { display: flex; flex-direction: row; gap: 2; padding: 3; border-radius: token(radius.md);
                           background: token(surface-raised); border: 1 token(border) }
        .segment-item    { padding: 8 16; border-radius: token(radius.sm); font-size: 14; font-weight: bold;
                           color: token(text-muted); content-align: middle-center; text-align: center; white-space: nowrap }
        .segment-item:hover    { color: token(text); background: token(surface-overlay) }
        .segment-item-on       { background: token(primary); color: token(primary-fg); box-shadow: token(shadow.sm) }
        .segment-item-on:hover { background: token(primary-hover); color: token(primary-fg) }
        .segment-item:disabled { color: token(text-subtle); background: transparent }

        /* 체크박스·라디오 (DvCheckBox · DvRadio) — 상자(24)는 왼쪽 안쪽 여백 자리에 라이브러리가 그린다(24 + 간격 10) */
        .checkbox          { padding: 8 0 8 34; min-height: 40; font-size: token(font.body); color: token(text); content-align: middle-left }
        .checkbox:disabled { color: token(text-subtle) }
        .radio             { padding: 8 0 8 34; min-height: 40; font-size: token(font.body); color: token(text); content-align: middle-left }
        .radio:disabled    { color: token(text-subtle) }

        /* 목록 (DvListBox) — 스크롤 패널, 행(높이 40)은 라이브러리가 만든다 */
        .listbox          { display: flex; flex-direction: column; align-items: stretch; gap: 2; overflow: scroll; padding: 4;
                            background: token(surface); border: 1 token(border-strong); border-radius: token(radius.sm) }
        .listbox:disabled { background: token(surface-raised); border: 1 token(border) }
        .listbox-item     { height: 40; flex-shrink: 0; padding: 0 12; border-radius: token(radius.sm); font-size: token(font.body);
                            color: token(text); content-align: middle-left; white-space: nowrap }
        .listbox-item:hover    { background: token(surface-raised) }
        .listbox-item:press    { background: token(surface-overlay) }
        .listbox-item-on       { background: token(primary); color: token(primary-fg) }
        .listbox-item-on:hover { background: token(primary-hover); color: token(primary-fg) }
        .listbox-item:disabled { color: token(text-subtle); background: transparent }

        /* 트리 (DvTreeView) — 스크롤 패널, 행(높이 40, 깊이마다 들여쓰기)은 라이브러리가 만든다 */
        .treeview          { display: flex; flex-direction: column; align-items: stretch; gap: 2; overflow: scroll; padding: 4;
                             background: token(surface); border: 1 token(border-strong); border-radius: token(radius.sm) }
        .treeview:disabled { background: token(surface-raised); border: 1 token(border) }
        .treeview-item     { height: 40; flex-shrink: 0; padding: 0 12; border-radius: token(radius.sm); font-size: token(font.body);
                             color: token(text); content-align: middle-left; white-space: nowrap }
        .treeview-item:hover    { background: token(surface-raised) }
        .treeview-item:press    { background: token(surface-overlay) }
        .treeview-item-on       { background: token(primary); color: token(primary-fg) }
        .treeview-item-on:hover { background: token(primary-hover); color: token(primary-fg) }
        .treeview-item:disabled { color: token(text-subtle); background: transparent }

        /* 접는 패널 (DvExpander) — 제목 줄·내용 상자는 라이브러리가 만든다 */
        .expander        { display: flex; flex-direction: column; align-items: stretch; background: token(surface);
                           border: 1 token(border); border-radius: token(radius.md); overflow: hidden }
        .expander-header { height: 44; flex-shrink: 0; padding: 0 14; font-size: 14; font-weight: bold; color: token(text);
                           content-align: middle-left; white-space: nowrap }
        .expander-header:hover    { background: token(surface-raised) }
        .expander-header:press    { background: token(surface-overlay) }
        .expander-header:disabled { color: token(text-subtle); background: transparent }
        .expander-body   { display: flex; flex-direction: column; align-items: stretch; gap: token(spacing.md); padding: 4 14 14 14 }

        /* 탭 (DvTabs · DvTab) — 머리글·구분선은 라이브러리가 만든다 */
        .tabs        { display: flex; flex-direction: column; align-items: stretch; gap: 0 }
        .tab-header  { display: flex; flex-direction: row; gap: 4 }
        .tab         { padding: 10 18; border-radius: token(radius.sm); font-size: 14; font-weight: bold;
                       color: token(text-muted); white-space: nowrap; content-align: middle-center }
        .tab:hover   { color: token(text); background: token(surface-raised) }
        .tab-on      { color: token(primary); background: token(surface-raised) }
        .tab-on:hover { color: token(primary) }
        .tab:disabled { color: token(text-subtle); background: transparent }
        .tab-rule    { height: 2; background: token(border) }
        .tab-body    { display: flex; flex-direction: column; flex-grow: 1; gap: token(spacing.md); padding: token(spacing.md) 0 0 0 }

        /* 아이콘 (DvIcon) — 크기 = font-size, 색 = color */
        .icon { font-size: 24; color: token(text) }

        /* 입력 상자 (DvInput) */
        .input          { background: token(surface); border: 1 token(border-strong); border-radius: token(radius.sm);
                          padding: 8 12; font-size: token(font.body); color: token(text) }
        .input:disabled { background: token(surface-raised); color: token(text-subtle); border: 1 solid token(border) }
        .input-readonly { background: transparent; border: 1 dashed token(border-strong) }

        /* 의미 컴포넌트 내부 */
        .stat        { background: token(surface); border: 1 token(border); border-radius: token(radius.lg);
                       box-shadow: token(shadow.md); overflow: hidden }
        .stat-accent { height: 3 }
        .stat-body   { padding: token(spacing.lg) token(spacing.xl) }
        .stat-value  { font-size: token(font.display); font-weight: bold; color: token(text) }
        .status-item { padding: token(spacing.sm) token(spacing.xs) }
        .switch-row  { padding: token(spacing.xs) }
        """;
}
