namespace Devinno.UI.Components;

/// <summary>
/// The semantic components built into the library (<c>DvPageFrame</c>, <c>DvDialogFrame</c>, <c>DvCard</c>, <c>DvBadge</c>, <c>DvStat</c>, <c>DvStatusItem</c>, <c>DvSwitch</c>).
/// Markup states only "what it is"; the appearance is determined by theme tokens and built-in classes (<c>page</c>, <c>dialog</c>, <c>card</c>, <c>chip</c>, <c>stat</c>, etc.).
/// </summary>
/// <remarks>
/// <para>They can be used on any screen without registration. If the user's <see cref="ComponentRegistry"/> has a component with the same name, the user's one takes precedence.</para>
/// <para>The <c>tone</c>/<c>state</c> parameters are used inside styles as part of a token name, as in <c>token(tone-{tone}-fg)</c>.
/// Parameter values are filled in first and then tokens are resolved, so for example <c>tone="warn"</c> applies the <c>tone-warn-fg</c> token color.</para>
/// </remarks>
public static class BuiltInComponents
{
    private static readonly Lazy<ComponentRegistry> Lazy = new(Create);

    /// <summary>
    /// Gets the registry with all built-in components registered. Created once on first read (thread-safe); the layout engine uses it to find tags not in the user registry.
    /// It is a shared instance, so do not <see cref="ComponentRegistry.Register"/> into it; create an app-specific <see cref="ComponentRegistry"/> instead.
    /// </summary>
    public static ComponentRegistry Registry => Lazy.Value;

    private static ComponentRegistry Create()
    {
        var r = new ComponentRegistry();

        r.Register("""
            <DvComponent name="DvPageFrame">
              <DvParam name="title"/>
              <DvParam name="subtitle"/>
              <DvStack class="page" gap="lg">
                <DvStack gap="none" visible="{title}">
                  <DvLabel class="heading" text="{title}"/>
                  <DvLabel class="label muted" text="{subtitle}" visible="{subtitle}"/>
                </DvStack>
                <DvSlot/>
              </DvStack>
            </DvComponent>
            """);

        // 다이얼로그 틀 — DvPageFrame 과 같은 구조(제목·부제목 + 본문). 크기는 이 요소의 style width/height, 없으면 내용 크기.
        r.Register("""
            <DvComponent name="DvDialogFrame">
              <DvParam name="title"/>
              <DvParam name="subtitle"/>
              <DvStack class="dialog" gap="md">
                <DvStack gap="none" visible="{title}">
                  <DvLabel class="title" text="{title}"/>
                  <DvLabel class="label muted" text="{subtitle}" visible="{subtitle}"/>
                </DvStack>
                <DvSlot/>
              </DvStack>
            </DvComponent>
            """);

        r.Register("""
            <DvComponent name="DvCard">
              <DvParam name="title"/>
              <DvParam name="badge"/>
              <DvStack class="card" gap="md">
                <DvRow gap="sm" visible="{title}">
                  <DvLabel class="title" text="{title}"/>
                  <DvSpacer/>
                  <DvBadge text="{badge}" visible="{badge}"/>
                </DvRow>
                <DvSlot/>
              </DvStack>
            </DvComponent>
            """);

        r.Register("""
            <DvComponent name="DvBadge">
              <DvParam name="text"/>
              <DvParam name="tone" default="neutral"/>
              <DvLabel class="chip" text="{text}"
                       style="color:token(tone-{tone}-fg); background:token(tone-{tone}-soft)"/>
            </DvComponent>
            """);

        r.Register("""
            <DvComponent name="DvStat">
              <DvParam name="label"/>
              <DvParam name="value"/>
              <DvParam name="unit"/>
              <DvParam name="delta"/>
              <DvParam name="tone" default="info"/>
              <DvStack class="stat" gap="none">
                <DvBox class="stat-accent"
                       style="background:linear-gradient(90deg, token(tone-{tone}), token(tone-{tone}-fg))"/>
                <DvStack class="stat-body" gap="sm">
                  <DvRow gap="sm">
                    <DvLabel class="label muted" text="{label}"/>
                    <DvSpacer/>
                    <DvBadge text="{delta}" tone="{tone}" visible="{delta}"/>
                  </DvRow>
                  <DvRow gap="xs" align="end">
                    <DvLabel class="stat-value" text="{value}"/>
                    <DvLabel class="body subtle" text="{unit}" visible="{unit}" style="padding:0 0 6 0"/>
                  </DvRow>
                </DvStack>
              </DvStack>
            </DvComponent>
            """);

        r.Register("""
            <DvComponent name="DvStatusItem">
              <DvParam name="label"/>
              <DvParam name="state" default="off"/>
              <DvParam name="status"/>
              <DvParam name="blink" default="false"/>
              <DvRow class="status-item" gap="md">
                <DvLamp on="true" state="{state}" blink="{blink}" blink-interval="450" style="width:12; height:12"/>
                <DvLabel class="body" text="{label}"/>
                <DvSpacer/>
                <DvLabel class="chip" text="{status}" visible="{status}"
                         style="color:token(state-{state}-fg); background:token(state-{state}-soft)"/>
              </DvRow>
            </DvComponent>
            """);

        r.Register("""
            <DvComponent name="DvSwitch">
              <DvParam name="label"/>
              <DvParam name="value"/>
              <DvRow class="switch-row" gap="md">
                <DvLabel class="body" text="{label}"/>
                <DvSpacer/>
                <DvToggle value="{value}" style="width:46; height:26"/>
              </DvRow>
            </DvComponent>
            """);

        return r;
    }
}
