using System.Diagnostics;
using System.Numerics;
using Devinno.UI;
using Devinno.UI.App;
using Devinno.UI.Scene3D;
using SkiaSharp;

namespace Devinno.UI.Scene3D.Demo;

/// <summary>
/// 창 없이 실제 화면 코드로 그리고, 값을 넣고, 클릭해서 결과를 PNG 와 수치로 확인한다.
/// 3D 자세는 마크업 정의(SceneView 자식)에서만 오므로, 그려진 자세(SceneView.LastWorld)가 VM 값과 맞는지 보면
/// 값 → 바인딩 → 정의 → 자세 경로 전체를 확인하는 셈이다.
/// </summary>
internal static class Verify
{
    private const int W = 1360, H = 820;

    public static int Run(DvApp app, MainPage page, string outDir)
    {
        Directory.CreateDirectory(outDir);
        foreach (var old in Directory.GetFiles(outDir, "*.png"))
            File.Delete(old);
        var s = app.Surface;
        var failed = 0;
        using var frame = SKSurface.Create(new SKImageInfo(W, H));
        var clock = 1000L;
        var driver = new Driver(page, app.Invalidate);
        var warnings = new Warnings();
        Trace.Listeners.Add(warnings);

        void Check(bool ok, string name, string detail = "")
        {
            Console.WriteLine($"{(ok ? "OK  " : "FAIL")} {name} {detail}");
            if (!ok)
                failed++;
        }

        void Frame()
        {
            s.Layout(W, H);
            s.RenderTo(frame.Canvas, W, H, clock += 16);
        }

        void Frames(int n)
        {
            for (var i = 0; i < n; i++)
                Frame();
        }

        // VM 값이 바뀐 뒤 화면을 다시 만들어 그린다 (실제 앱의 Driver 와 같은 길)
        void Rebuild()
        {
            app.Invalidate();
            Frame();
        }

        string Shot(string name)
        {
            s.Layout(W, H);
            var path = Path.Combine(outDir, name + ".png");
            DvRenderer.RenderToFile(s, W, H, path);
            return path;
        }

        // 목표값을 넣고 실제값이 따라갈 때까지 시간을 진행한다
        void Settle(double seconds = 5)
        {
            for (var t = 0.0; t < seconds; t += 0.02)
                driver.Step(0.02);
            Rebuild();
        }

        // ---- 1. 모델 ----
        var model = SceneModel.Load("Assets/scene3d_test.glb");
        var clip = model.Clips.FirstOrDefault(c => c.Name == "CrankCycle");
        Console.WriteLine($"INFO model: {model.Nodes.Count} nodes, {model.Parts.Count} parts, {model.Triangles:N0} triangles, " +
                          $"{model.Materials.Count} materials, {model.Textures.Count} textures, cameras [{string.Join(", ", model.Cameras.Select(c => c.Name))}], " +
                          $"clips [{string.Join(", ", model.Clips.Select(c => $"{c.Name} {c.Duration:0.##} s"))}]");
        Check(model.Triangles == 9336, "triangle count matches the manifest", $"{model.Triangles}");
        Check(model.Textures.Count == 1, "nameplate texture loaded");
        Check(model.Cameras.Count == 3, "three cameras");
        Check(clip is not null && Math.Abs(clip.Duration - 15) < 0.01, "CrankCycle clip is 15 s", $"{clip?.Duration}");

        Vector3 Origin(Matrix4x4[] world, string node) => world[model.NodeByName[node]].Translation;

        Vector3 PartCenter(Matrix4x4[] world, string node)
        {
            var i = model.NodeByName[node];
            var sum = Vector3.Zero;
            var n = 0;
            foreach (var part in model.Parts.Where(p => p.Node == i))
                for (var v = 0; v < part.Vertices.Length; v += 8)
                {
                    sum += Vector3.Transform(new Vector3(part.Vertices[v], part.Vertices[v + 1], part.Vertices[v + 2]), world[i]);
                    n++;
                }
            return sum / n;
        }

        double YAngle(Matrix4x4[] world, string node)
        {
            var m = world[model.NodeByName[node]];
            return Math.Atan2(-m.M13, m.M11) * 180 / Math.PI;
        }

        // 월드 점 → 화면 좌표 (마지막 그림 기준)
        SKPoint Screen(Vector3 p)
        {
            var c = Vector4.Transform(new Vector4(p, 1), SceneView.LastViewProj);
            var b = SceneView.LastBounds;
            return new SKPoint(b.Left + (c.X / c.W * 0.5f + 0.5f) * b.Width, b.Top + (0.5f - c.Y / c.W * 0.5f) * b.Height);
        }

        double Coverage(string path)
        {
            using var b = SKBitmap.Decode(path);
            var r = SceneView.LastBounds;
            var bg = b.GetPixel((int)r.Left + 2, (int)r.Top + 2);
            int hit = 0, all = 0;
            for (var y = (int)r.Top; y < (int)r.Bottom; y += 2)
                for (var x = (int)r.Left; x < (int)r.Right; x += 2)
                {
                    all++;
                    var c = b.GetPixel(x, y);
                    if (Math.Abs(c.Red - bg.Red) + Math.Abs(c.Green - bg.Green) + Math.Abs(c.Blue - bg.Blue) > 30)
                        hit++;
                }
            return (double)hit / all;
        }

        double Changed(string a, string b)
        {
            using var x = SKBitmap.Decode(a);
            using var y = SKBitmap.Decode(b);
            var r = SceneView.LastBounds;
            int diff = 0, all = 0;
            for (var yy = (int)r.Top; yy < (int)r.Bottom; yy += 2)
                for (var xx = (int)r.Left; xx < (int)r.Right; xx += 2)
                {
                    all++;
                    if (x.GetPixel(xx, yy) != y.GetPixel(xx, yy))
                        diff++;
                }
            return (double)diff / all;
        }

        SKColor Pixel(string path, SKPoint p)
        {
            using var b = SKBitmap.Decode(path);
            return b.GetPixel((int)p.X, (int)p.Y);
        }

        // ---- 2. 첫 화면과 카메라 ----
        driver.Derive();
        Rebuild();
        Frames(2);
        var front = Shot("01_front");
        var r = SceneView.LastBounds;
        Check(r.Width > 300 && r.Height > 300, "3D view has room", $"{r}");
        Check(Coverage(front) > 0.05, "model is drawn", $"coverage={Coverage(front):P0}");

        foreach (var cam in new[] { "CamSide", "CamTop", "Free" })
        {
            page.Camera = cam;
            app.Invalidate();
            Frames(30); // 0.3 초 이동 + 여유
            var shot = Shot($"02_camera_{cam}");
            Check(Changed(front, shot) > 0.1, $"camera {cam} changes the view", $"changed={Changed(front, shot):P0}");
        }
        page.Camera = "CamFront";
        app.Invalidate();
        Frames(30);
        var back = Shot("03_front_again");
        Check(Changed(front, back) < 0.01, "back to CamFront gives the same image", $"changed={Changed(front, back):P1}");

        // ---- 3. 정의 → 자세 ----
        page.TurretSet = 90;
        page.Arm1Set = 0;
        page.Arm2Set = 0;
        Settle();
        var gripper = Origin(SceneView.LastWorld, "Gripper");
        Check(Vector3.Distance(gripper, new Vector3(-0.95f, 0.79f, -1.05f)) < 0.002, "<Rotate Turret>: turret +90° swings the gripper to -Z", $"{gripper}");
        Shot("04_turret_90");

        page.TurretSet = 0;
        page.Arm1Set = 30;
        page.Arm2Set = -60;
        page.GripSet = 100;
        page.LiftSet = 150;
        page.DoorSet = 90;
        Settle();
        var world = SceneView.LastWorld;
        var a1 = 30 * MathF.PI / 180;
        var arm2Expected = new Vector3(-0.95f, 0.34f, -0.65f) + new Vector3(-MathF.Sin(a1), MathF.Cos(a1), 0) * 0.45f;
        Check(Vector3.Distance(Origin(world, "Arm2"), arm2Expected) < 0.002, "<Rotate Arm1>: arm 1 +30° tilts arm 2 toward -X", $"{Origin(world, "Arm2")}");
        var fingerGap = Vector3.Distance(Origin(world, "FingerL"), Origin(world, "FingerR"));
        Check(Math.Abs(fingerGap - 0.124) < 0.002, "<Move FingerL> + <Move FingerR follow ratio=-1>: 100 % opens 0.124 m", $"{fingerGap:0.000}");
        Check(Math.Abs(Origin(world, "LiftRod").Y - (0.83f + 0.15f)) < 0.002, "<Move LiftRod scale=0.001>: +150 mm", $"{Origin(world, "LiftRod").Y:0.000}");
        Shot("05_arms_grip_lift_door");
        page.GripSet = 0;
        Settle();
        fingerGap = Vector3.Distance(Origin(SceneView.LastWorld, "FingerL"), Origin(SceneView.LastWorld, "FingerR"));
        Check(fingerGap < 0.002, "gripper 0 % closes the fingers", $"{fingerGap:0.000}");

        // 기어: 시간을 진행해도 B 각도 = -2 × A 각도
        page.GearRpm = 37;
        for (var i = 0; i < 37; i++)
            driver.Step(0.02);
        Rebuild();
        var gearA = YAngle(SceneView.LastWorld, "GearA");
        var gearB = YAngle(SceneView.LastWorld, "GearB");
        var gearError = Math.Abs(((gearB + 2 * gearA) % 360 + 540) % 360 - 180);
        Check(gearError < 0.01, "<Rotate GearB follow=GearA ratio=-2>", $"A={gearA:0.00}°, B={gearB:0.00}°, error {gearError:0.0000}°");

        // ---- 4. 크랭크: C# 공식 값과 Blender 클립 비교 (disabled 로 둘 중 하나만) ----
        double maxPos = 0, maxRod = 0, maxPin = 0;
        for (var deg = 0; deg < 360; deg += 5)
        {
            page.CrankAngle = deg;
            driver.Derive();
            page.CrankMode = "formula";
            Rebuild();
            var f = SceneView.LastWorld;
            page.CrankMode = "clip";
            Rebuild();
            var c = SceneView.LastWorld;

            maxPos = Math.Max(maxPos, Vector3.Distance(Origin(f, "Piston"), Origin(c, "Piston")));
            var rodF = Vector3.Transform(new Vector3(-(float)Driver.L, 0, 0), f[model.NodeByName["ConnectingRod"]]);
            var rodC = Vector3.Transform(new Vector3(-(float)Driver.L, 0, 0), c[model.NodeByName["ConnectingRod"]]);
            maxRod = Math.Max(maxRod, Vector3.Distance(rodF, rodC));
            // 로드 큰 끝 = 크랭크 핀 (XY 평면)
            var pin = Vector3.Transform(new Vector3((float)Driver.R, 0, 0), f[model.NodeByName["Crank"]]);
            maxPin = Math.Max(maxPin, new Vector2(pin.X - rodF.X, pin.Y - rodF.Y).Length());
        }
        Check(maxPos < 0.001, "piston: formula = Blender clip", $"max difference {maxPos * 1000:0.000} mm");
        Check(maxRod < 0.001, "rod big end: formula = Blender clip", $"max difference {maxRod * 1000:0.000} mm");
        Check(maxPin < 0.001, "rod big end stays on the crank pin", $"max gap {maxPin * 1000:0.000} mm");

        page.CrankAngle = 90;
        driver.Derive();
        page.CrankMode = "formula";
        Rebuild();
        Shot("06_crank_90_formula");
        page.CrankMode = "clip";
        Rebuild();
        Shot("07_crank_90_clip");
        page.CrankMode = "formula";

        // ---- 5. 색 ----
        page.Lamp = "alarm";
        driver.Time = 0; // 깜빡임 켜진 때
        driver.Derive();
        Rebuild();
        var alarm = Shot("08_lamp_alarm");
        var lampPoint = Screen(PartCenter(SceneView.LastWorld, "PumpLamp"));
        var lamp = Pixel(alarm, lampPoint);
        Check(lamp.Red > 150 && lamp.Green < 90, "<Color PumpLamp>: alarm makes the lamp red", $"pixel {lamp} at {lampPoint}");
        page.Lamp = "run";
        driver.Derive();
        Rebuild();
        var run = Shot("09_lamp_run");
        lamp = Pixel(run, lampPoint);
        Check(lamp.Green > 120 && lamp.Red < 120, "run makes the lamp green", $"pixel {lamp}");

        // ---- 6. 마우스 ----
        s.PointerMove(lampPoint);
        Frame();
        var hover = Shot("10_hover_pump");
        Check(Changed(run, hover) > 0.002, "hover highlights the part", $"changed={Changed(run, hover):P1}");

        // 클릭: 부품을 클릭 단위로 올려 선택. 팬은 앞에서 보면 문틀 뒤라 옆 카메라에서 누른다.
        var targets = new (string Node, string Expect, string Camera)[]
        {
            ("PumpLamp", "PumpBody", "CamFront"), ("Nameplate", "PumpBody", "CamFront"), ("GearA", "GearA", "CamFront"),
            ("GearB", "GearB", "CamFront"), ("Turret", "RobotBase", "CamFront"), ("LiftRod", "LiftBody", "CamFront"),
            ("Door", "DoorFrame", "CamFront"), ("Piston", "Piston", "CamFront"), ("FanBlade1", "FanHub", "CamSide"),
        };
        foreach (var (node, expect, camera) in targets)
        {
            if (page.Camera != camera)
            {
                page.Camera = camera;
                app.Invalidate();
                Frames(30);
            }
            var p = Screen(PartCenter(SceneView.LastWorld, node));
            page.Selected = null;
            Rebuild();
            s.PointerDown(p);
            s.PointerUp(p);
            Frame();
            Check(page.Selected == expect, $"click on {node} selects {expect}", $"got '{page.Selected}', pick {SceneView.LastPickMs:0.00} ms");
        }
        s.PointerLeave();
        page.Camera = "CamFront";
        app.Invalidate();
        Frames(30);
        var selected = Shot("11_selected_front");

        // 드래그 회전, 휠 확대
        var center = new SKPoint(r.MidX, r.MidY);
        var before = page.Selected;
        s.PointerDown(center);
        s.PointerMove(new SKPoint(center.X + 60, center.Y));
        s.PointerMove(new SKPoint(center.X + 160, center.Y + 30));
        s.PointerUp(new SKPoint(center.X + 160, center.Y + 30));
        Frame();
        var rotated = Shot("12_rotated");
        Check(Changed(selected, rotated) > 0.05, "drag rotates the view", $"changed={Changed(selected, rotated):P0}");
        Check(page.Selected == before, "a drag is not a click", $"'{page.Selected}'");
        s.PointerWheel(center, 3);
        Frame();
        var zoomed = Shot("13_zoomed");
        Check(Coverage(zoomed) > Coverage(rotated), "wheel up zooms in", $"{Coverage(rotated):P0} -> {Coverage(zoomed):P0}");

        // ---- 7. 경고 없음 ----
        Check(warnings.Lines.Count == 0, "no Scene3D or Devinno.UI warnings", string.Join(" | ", warnings.Lines.Take(5)));

        // ---- 8. 성능: 20 ms 마다 값 쓰기 + 화면 다시 만들기 + 그리기 ----
        page.Auto = true;
        var times = new List<double>();
        var rebuild = new List<double>();
        var render = new List<double>();
        for (var i = 0; i < 150; i++)
        {
            var sw = Stopwatch.StartNew();
            driver.Step(0.02);
            app.Invalidate();
            s.Layout(W, H);
            rebuild.Add(sw.Elapsed.TotalMilliseconds);
            s.RenderTo(frame.Canvas, W, H, clock += 16);
            times.Add(sw.Elapsed.TotalMilliseconds);
            render.Add(SceneView.LastRenderMs);
        }
        times.Sort();
        rebuild.Sort();
        render.Sort();
        Console.WriteLine($"INFO auto mode, {r.Width:0}x{r.Height:0} view, Invalidate every 20 ms: rebuild median {rebuild[rebuild.Count / 2]:0.0} ms; " +
                          $"rebuild + frame median {times[times.Count / 2]:0.0} ms, worst {times[^1]:0.0} ms; 3D part median {render[render.Count / 2]:0.0} ms");
        driver.UpdateTexts();
        Rebuild();
        Shot("14_auto");

        Trace.Listeners.Remove(warnings);
        Console.WriteLine(failed == 0 ? "ALL PASSED" : $"{failed} FAILED");
        return failed == 0 ? 0 : 1;
    }

    private sealed class Warnings : TraceListener
    {
        public List<string> Lines { get; } = new();
        public override void Write(string? message) { }
        public override void WriteLine(string? message)
        {
            if (message is not null && (message.Contains("Scene3D:") || message.Contains("Devinno.UI")))
                lock (Lines) Lines.Add(message);
        }
        public override void TraceEvent(TraceEventCache? c, string source, TraceEventType type, int id, string? message) => WriteLine(message);
    }
}
