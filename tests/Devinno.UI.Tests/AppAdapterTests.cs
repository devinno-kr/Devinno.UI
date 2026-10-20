using Devinno.UI.App;
using Xunit;

namespace Devinno.UI.Tests;

/// <summary>어댑터 연결 규칙(앱 하나 = 어댑터 하나) — 어댑터 없이 DvApp 만.</summary>
[Collection("AttachedApp")]
public class AppAdapterTests
{
    [Fact]
    public void Attach_Twice_Throws()
    {
        DvApp.ResetForTests();
        var app = new DvApp();
        var h = new object();
        app.Attach(h);
        try { Assert.Throws<InvalidOperationException>(() => app.Attach(new object())); }
        finally { app.Detach(h); }
    }

    [Fact]
    public void Attach_SameHostTwice_Throws()
    {
        DvApp.ResetForTests();
        var app = new DvApp();
        var host = new object();
        app.Attach(host);
        try { Assert.Throws<InvalidOperationException>(() => app.Attach(host)); }
        finally { app.Detach(host); }
    }

    [Fact]
    public void Detach_ThenAttach_Ok()
    {
        DvApp.ResetForTests();
        var app = new DvApp();
        var a = new object();
        var b = new object();
        app.Attach(a);
        app.Detach(a);
        app.Attach(b);
        app.Detach(b);
    }

    [Fact]
    public void Detach_ByOtherHost_KeepsAttachment()
    {
        DvApp.ResetForTests();
        var app = new DvApp();
        var h = new object();
        app.Attach(h);
        try
        {
            app.Detach(new object()); // 붙어 있지 않은 쪽의 Detach 는 무시
            Assert.Throws<InvalidOperationException>(() => app.Attach(new object()));
        }
        finally { app.Detach(h); }
    }

    [Fact]
    public void Attach_Null_Throws()
    {
        DvApp.ResetForTests();
        Assert.Throws<ArgumentNullException>(() => new DvApp().Attach(null!));
    }
}
