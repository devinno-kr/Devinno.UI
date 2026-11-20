using Devinno.UI.Scene3D.Demo;
using Devinno.UI.App;
using Devinno.UI.Components;
using Devinno.UI.OpenTK;
using Devinno.UI.Styling;
using Devinno.UI.Scene3D;

// 사용법
//   dotnet run              창을 띄운다 (--gpu 를 붙이면 GPU 모드)
//   dotnet run -- --verify  창 없이 그리기·자세 계산·클릭·성능을 확인하고 output 에 PNG 를 남긴다
//   dotnet run -c Release -- --bench  창 없이 크기별·캔버스별(CPU/GPU) 3D 프레임 시간과 메모리를 잰다

// 모델 경로는 실행 폴더 기준
Environment.CurrentDirectory = AppContext.BaseDirectory;

if (args.Contains("--bench"))
    return Bench.Run();

var registry = new ComponentRegistry().UseScene3D();
var page = new MainPage();
var app = new DvApp { Theme = DvTheme.Dark, Registry = registry };
app.Pages.Add(page);

if (args.Contains("--verify"))
    return Verify.Run(app, page, Path.Combine(FindProjectDir(), "output"));

using var window = new ViewWindow(app, 1360, 820, "Scene3D - C# driven", fit: true) { Gpu = args.Contains("--gpu") };
using var driver = new Driver(page, window.Invalidate);
driver.Start();
window.Run();
return 0;

static string FindProjectDir()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Devinno.UI.Scene3D.Demo.csproj")))
        dir = dir.Parent;
    return dir?.FullName ?? throw new DirectoryNotFoundException("Devinno.UI.Scene3D.Demo.csproj not found");
}
