using Devinno.UI.App;
using Xunit;

namespace Devinno.UI.Tests;

/// <summary>정적 상태(붙은 앱·마지막 앱)를 건드리는 테스트는 병렬로 돌리지 않는다.</summary>
[CollectionDefinition("AttachedApp", DisableParallelization = true)]
public class AttachedAppCollection { }

/// <summary>프로세스에 붙는 앱은 하나, <see cref="DvApp.Current"/>, 떼기와 닫힘의 구분.</summary>
[Collection("AttachedApp")]
public class AppCurrentTests
{
    [Fact]
    public void Current_NeverAttached_Throws()
    {
        DvApp.ResetForTests();
        Assert.Throws<InvalidOperationException>(() => DvApp.Current);
    }

    [Fact]
    public void Attach_SetsCurrent()
    {
        DvApp.ResetForTests();
        var a = new DvApp();
        var h = new object();
        a.Attach(h);
        try { Assert.Same(a, DvApp.Current); }
        finally { a.Detach(h); }
    }

    [Fact]
    public void SecondAppAttach_Throws_FirstStays()
    {
        DvApp.ResetForTests();
        var a = new DvApp();
        var b = new DvApp();
        var h = new object();
        a.Attach(h);
        try
        {
            Assert.Throws<InvalidOperationException>(() => b.Attach(new object()));
            Assert.Same(a, DvApp.Current);
        }
        finally { a.Detach(h); }
    }

    [Fact]
    public void AfterDetach_OtherAppCanAttach_CurrentFollows()
    {
        DvApp.ResetForTests();
        var a = new DvApp();
        var b = new DvApp();
        var h = new object();
        var h2 = new object();
        a.Attach(h);
        a.Detach(h);
        b.Attach(h2);
        try { Assert.Same(b, DvApp.Current); }
        finally { b.Detach(h2); }
    }

    [Fact]
    public void AfterClose_CurrentStillLastApp_IsClosed()
    {
        DvApp.ResetForTests();
        var a = new DvApp();
        var h = new object();
        a.Attach(h);
        a.Close(h);
        try
        {
            Assert.Same(a, DvApp.Current);
            Assert.True(a.IsClosed);
            a.Attach(h);
            Assert.False(a.IsClosed);
        }
        finally { a.Detach(h); }
    }

    [Fact]
    public void Detach_DoesNotMarkClosed()
    {
        DvApp.ResetForTests();
        var a = new DvApp();
        var h = new object();
        a.Attach(h);
        a.Detach(h);
        Assert.False(a.IsClosed);
    }
}
