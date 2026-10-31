using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal static class Program {
    internal static readonly string Root=AppDomain.CurrentDomain.BaseDirectory;
    internal static readonly string Node=Path.Combine(Root,"runtime","node.exe");
    internal static readonly string Data=Environment.GetEnvironmentVariable("DAFEIYU_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexDafeiyuLive2D");
    internal const string Repository="https://github.com/Chris-Geeky/codex-dafeiyu-live2d";
    internal const string CoreLicense="https://www.live2d.com/eula/live2d-proprietary-software-license-agreement_en.html";
    internal static readonly string Version=((AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(),typeof(AssemblyInformationalVersionAttribute))).InformationalVersion;
    internal static EventWaitHandle StartSignal;
    internal static EventWaitHandle QuitSignal;
    internal static bool IsPreview;
    private static Icon icon;
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    [DllImport("user32.dll")] private static extern uint GetDpiForSystem();
    [DllImport("user32.dll")] private static extern int GetSystemMetricsForDpi(int metric,uint dpi);
    internal static int SystemDpi() { try { return (int)GetDpiForSystem(); } catch(EntryPointNotFoundException) { return 96; } }
    internal static int IconPixels(int dpi,bool small) {
        try { int size=GetSystemMetricsForDpi(small?49:11,(uint)dpi);if(size>0)return size; } catch(EntryPointNotFoundException) {}
        return Math.Max(16,(int)Math.Round((small?16:32)*dpi/96f));
    }
    internal static Icon LoadIconSize(int pixels) {
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Dafeiyu.AppIcon"))
        using(var source=new Icon(stream,pixels,pixels))return (Icon)source.Clone();
    }
    internal static Icon AppIcon {
        get {
            if(icon==null)icon=LoadIconSize(IconPixels(SystemDpi(),false));
            return icon;
        }
    }
    internal static Image LoadLogo() { using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Dafeiyu.AppLogo"))using(var image=Image.FromStream(stream))return new Bitmap(image); }
    internal static Dictionary<string,object> LoadAppearanceCatalog() { using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Dafeiyu.AppearanceCatalog"))using(var reader=new StreamReader(stream,Encoding.UTF8))return new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(reader.ReadToEnd()); }
    [STAThread] private static int Main(string[] args) {
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        try { SetCurrentProcessExplicitAppUserModelID("ChrisGeeky.CodexDafeiyu"); } catch {}
        if(args.Contains("--quit")) { try { using(var signal=EventWaitHandle.OpenExisting("Local\\CodexDafeiyuQuit"))signal.Set();return 0; } catch(WaitHandleCannotBeOpenedException) { return 2; } }
        if(args.Contains("--preview")) { try { return Preview(args); } catch(Exception error) { UiLog(error.ToString());return 1; } }
        if(args.Contains("--self-test")) {
            try {
                if(AppIcon.Width<16)throw new InvalidOperationException("Missing application icon");
                foreach(int dpi in new [] {96,120,144,168,192,216,240})foreach(bool small in new [] {true,false})
                    using(var sized=LoadIconSize(IconPixels(dpi,small)))if(sized.Width!=IconPixels(dpi,small))throw new InvalidOperationException("Missing native DPI icon size");
                using(var logo=LoadLogo())if(logo.Width!=256)throw new InvalidOperationException("Missing application logo");
                if(FileVersionInfo.GetVersionInfo(Application.ExecutablePath).CompanyName!="Chris-Geeky")throw new InvalidOperationException("Missing author metadata");
                Run("cli.mjs","verify");return 0;
            } catch(Exception error) { UiLog(error.Message);return 1; }
        }
        bool owned;
        using(var mutex=new Mutex(true,"Local\\CodexDafeiyuLive2DLauncher",out owned)) {
            if(!owned) { try { using(var signal=EventWaitHandle.OpenExisting("Local\\CodexDafeiyuStart"))signal.Set(); } catch {}return 0; }
            using(StartSignal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\CodexDafeiyuStart"))
            using(QuitSignal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\CodexDafeiyuQuit"))
            using(var context=new PetContext())Application.Run(context);
        }
        return 0;
    }
    private static int Preview(string[] args) {
        IsPreview=true;
        int at=Array.IndexOf(args,"--preview"),stateAt=Array.IndexOf(args,"--state");
        if(at+1>=args.Length)throw new ArgumentException("Missing preview path");
        string state=stateAt>=0&&stateAt+1<args.Length?args[stateAt+1]:"active";
        Form form;
        if(state=="license")form=new CoreConsent();
        else if(state=="close")form=new CloseChoice();
        else if(state=="appearance"||state=="appearance-live")form=new AppearanceWebDialog(null,state=="appearance-live");
        else if(state=="about")form=new AboutDialog();
        else {
            var dashboard=new Dashboard();dashboard.AllowClose=true;
            dashboard.UpdateState(state=="waiting"?"等待 Codex 正常退出":state=="error"?"暂未连接":"已连接 Codex",
                state=="waiting"?"请完成当前工作，再从系统托盘完整退出 Codex。助手会随后正常重开。":state=="error"?"角色暂未加载完成。可以重试连接，或查看日志了解原因。":"大肥鱼正在陪你工作。点关闭可选择收起到托盘、退出或取消。",
                "界面预览 · 自动跟随当前宠物 · 目标 60 FPS",state=="waiting"?"waiting":state=="error"?"error":"active",false,60,true);
            form=dashboard;
        }
        using(form) {
            form.StartPosition=FormStartPosition.Manual;form.Location=new Point(Screen.PrimaryScreen.WorkingArea.Left+20,Screen.PrimaryScreen.WorkingArea.Top+20);form.Opacity=0;form.ShowInTaskbar=false;form.Show();Application.DoEvents();
            if(state=="appearance-live") {
                var preview=(AppearanceWebDialog)form;var expires=DateTime.UtcNow.AddSeconds(30);
                while(!preview.Ready&&DateTime.UtcNow<expires) { Application.DoEvents();Thread.Sleep(20); }
                if(!preview.Ready)throw new InvalidOperationException("WebView preview did not become ready");
                var settled=DateTime.UtcNow.AddMilliseconds(1200);while(DateTime.UtcNow<settled) { Application.DoEvents();Thread.Sleep(15); }
                string output=Path.GetFullPath(args[at+1]);Directory.CreateDirectory(Path.GetDirectoryName(output));
                var capture=preview.CaptureAsync(output);while(!capture.IsCompleted) { Application.DoEvents();Thread.Sleep(10); }capture.GetAwaiter().GetResult();
                File.WriteAllText(output+".layout.json",new JavaScriptSerializer().Serialize(new {dpi=form.DeviceDpi,width=form.ClientSize.Width,height=form.ClientSize.Height}));form.Close();return 0;
            }
            using(var bitmap=new Bitmap(form.Width,form.Height)) {
                form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));string output=Path.GetFullPath(args[at+1]);Directory.CreateDirectory(Path.GetDirectoryName(output));
                int border=(form.Width-form.ClientSize.Width)/2,caption=form.Height-form.ClientSize.Height-border;
                using(var client=bitmap.Clone(new Rectangle(border,caption,form.ClientSize.Width,form.ClientSize.Height),System.Drawing.Imaging.PixelFormat.Format32bppArgb))client.Save(output,System.Drawing.Imaging.ImageFormat.Png);
                File.WriteAllText(output+".layout.json",new JavaScriptSerializer().Serialize(new {dpi=form.DeviceDpi,width=form.ClientSize.Width,height=form.ClientSize.Height,scaleX=form.AutoScaleDimensions.Width,scaleY=form.CurrentAutoScaleDimensions.Width}));
            }
            form.Hide();form.Close();
        }
        return 0;
    }
    internal static Dictionary<string,object> ReadJson(string file) {
        using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))
        using(var reader=new StreamReader(stream,Encoding.UTF8))return new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(reader.ReadToEnd());
    }
    internal static string Text(Dictionary<string,object> data,string key,string fallback="") { object value;return data!=null&&data.TryGetValue(key,out value)&&value!=null?Convert.ToString(value):fallback; }
    internal static bool CoreReady() {
        try {
            var manifest=ReadJson(Path.Combine(Root,"assets-manifest.json"));var expected=(Dictionary<string,object>)manifest["core_runtime"];
            var bytes=File.ReadAllBytes(Path.Combine(Data,"runtime","live2dcubismcore.min.js"));
            if(bytes.Length!=Convert.ToInt32(expected["bytes"]))return false;
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").Equals(Text(expected,"sha256"),StringComparison.OrdinalIgnoreCase);
        } catch { return false; }
    }
    internal static ProcessStartInfo Command(string script,string args) {
        if(!File.Exists(Node))throw new InvalidOperationException("缺少运行环境，请完整解压便携包；源码用户请参阅 README。");
        return new ProcessStartInfo(Node,"\""+Path.Combine(Root,script)+"\" "+args) {
            WorkingDirectory=Root,UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,
            StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8
        };
    }
    internal static string Run(string script,string args) {
        using(var process=new Process { StartInfo=Command(script,args) }) {
            process.Start();var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
            if(!process.WaitForExit(45000)) { try { process.Kill(); } catch {}throw new TimeoutException("操作超时，请检查网络或查看日志。"); }
            Task.WaitAll(output,error);
            if(process.ExitCode!=0)throw new InvalidOperationException(error.Result.Trim());
            return output.Result.Trim();
        }
    }
    internal static void UiLog(string message) { try { Directory.CreateDirectory(Data);File.AppendAllText(Path.Combine(Data,"launcher.log"),DateTime.UtcNow.ToString("o")+" "+message+Environment.NewLine,Encoding.UTF8); } catch {} }
    internal static void OpenUrl(string url) { Process.Start(new ProcessStartInfo(url) { UseShellExecute=true }); }
    internal static void OpenLocalFile(string file) { Process.Start(new ProcessStartInfo(file) { UseShellExecute=true }); }
    internal static void OpenLogs() {
        Directory.CreateDirectory(Data);
        var text=new StringBuilder("大肥鱼诊断记录：仅保存在本机，不会自动上传。\r\n\r\n");
        foreach(string relative in new [] {"launcher.log","sessions/controller.log"}) {
            text.AppendLine(relative);string file=Path.Combine(Data,relative);
            text.AppendLine(File.Exists(file)?String.Join(Environment.NewLine,File.ReadAllLines(file).Reverse().Take(80).Reverse()):"暂无记录。");text.AppendLine();
        }
        string output=Path.Combine(Data,"recent-diagnostics.txt");File.WriteAllText(output,text.ToString(),Encoding.UTF8);Process.Start("notepad.exe","\""+output+"\"");
    }
    internal static string SignatureDescription() {
        try { using(var certificate=X509Certificate.CreateFromSignedFile(Application.ExecutablePath))return "含数字签名（未在此验证证书链）"; }
        catch(CryptographicException) { return "未数字签名"; }
    }
}

internal sealed class PetContext : ApplicationContext {
    private readonly NotifyIcon tray;
    private readonly Dashboard panel=new Dashboard();
    private readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    private readonly ToolStripMenuItem enableMenu,restoreMenu,followMenu,fps30Menu,fps60Menu;
    private Process controller;
    private Icon trayIcon;
    private int trayPixels;
    private bool busy,closing,exitRequested,enabled,userPaused;
    private int fps=60;
    private bool automatic=true;
    private bool appearanceOpen;
    private string version="",heading="准备连接",detail="正在准备大肥鱼的运行环境。",displayState="starting";
    internal PetContext() {
        MainForm=panel;ReadSettings();
        panel.EnableRequested=StartPet;panel.RestoreRequested=RestorePet;panel.FollowRequested=delegate { Reconfigure("--pet-id auto"); };
        panel.FpsRequested=delegate(int requested) { if(requested!=fps)Reconfigure("--fps "+requested); };
        panel.HideRequested=delegate { panel.Hide(); };panel.ExitRequested=Shutdown;panel.LogsRequested=Program.OpenLogs;
        panel.AboutRequested=delegate { using(var about=new AboutDialog())about.ShowDialog(panel); };
        panel.AppearanceRequested=OpenAppearance;
        var menu=new ContextMenuStrip { Font=Palette.Font(9.5f) };
        menu.Items.Add("打开控制面板",null,delegate { ShowPanel(); });menu.Items.Add(new ToolStripSeparator());
        enableMenu=(ToolStripMenuItem)menu.Items.Add("启用大肥鱼",null,delegate { StartPet(); });
        restoreMenu=(ToolStripMenuItem)menu.Items.Add("恢复原宠物",null,delegate { RestorePet(); });
        followMenu=(ToolStripMenuItem)menu.Items.Add("自动跟随当前宠物",null,delegate { Reconfigure("--pet-id auto"); });
        menu.Items.Add("表情与装扮…",null,delegate { OpenAppearance(); });
        var frameRates=new ToolStripMenuItem("动画帧率");
        fps30Menu=(ToolStripMenuItem)frameRates.DropDownItems.Add("30 FPS",null,delegate { if(fps!=30)Reconfigure("--fps 30"); });
        fps60Menu=(ToolStripMenuItem)frameRates.DropDownItems.Add("60 FPS",null,delegate { if(fps!=60)Reconfigure("--fps 60"); });menu.Items.Add(frameRates);
        menu.Items.Add(new ToolStripSeparator());menu.Items.Add("查看日志",null,delegate { Program.OpenLogs(); });
        menu.Items.Add("退出助手并恢复原宠物",null,delegate { Shutdown(); });
        tray=new NotifyIcon { Text="大肥鱼 · 准备连接",ContextMenuStrip=menu,Visible=false };UpdateTrayIcon();tray.Visible=true;
        tray.DoubleClick+=delegate { ShowPanel(); };
        bool firstShow=true;panel.Shown+=delegate { if(firstShow) { firstShow=false;panel.BeginInvoke((Action)StartPet); } };
        timer.Interval=1500;timer.Tick+=delegate { Poll(); };timer.Start();Render();panel.Show();
    }
    private void ShowPanel() { if(!panel.Visible)panel.Show();if(panel.WindowState==FormWindowState.Minimized)panel.WindowState=FormWindowState.Normal;panel.BringToFront();panel.Activate(); }
    private void OpenAppearance() {
        if(busy||closing||appearanceOpen)return;appearanceOpen=true;
        try { ShowPanel();var state=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(Program.Run("appearance.mjs","get"));var selection=(Dictionary<string,object>)state["selection"];using(Form dialog=AppearanceWebDialog.IsAvailable()?(Form)new AppearanceWebDialog(selection):new AppearanceDialog(selection))dialog.ShowDialog(panel); }
        catch(Exception error) { Program.UiLog(error.ToString());MessageBox.Show(panel,error.Message,"装扮设置暂不可用",MessageBoxButtons.OK,MessageBoxIcon.Information); }
        finally { appearanceOpen=false; }
    }
    private void UpdateTrayIcon() {
        int pixels=Program.IconPixels(Program.SystemDpi(),true);if(trayIcon!=null&&pixels==trayPixels)return;
        var previous=trayIcon;trayIcon=Program.LoadIconSize(pixels);tray.Icon=trayIcon;trayPixels=pixels;if(previous!=null)previous.Dispose();
    }
    private void ReadSettings() {
        try { var settings=Program.ReadJson(Path.Combine(Program.Data,"settings.json"));fps=Convert.ToInt32(settings["fps"]);automatic=!settings.ContainsKey("petId")||settings["petId"]==null; } catch {}
    }
    private void Render() {
        if(panel.IsDisposed)return;
        string metadata=(String.IsNullOrEmpty(version)?"本机连接":"Codex "+version)+" · "+(automatic?"自动跟随当前宠物":"固定宠物")+" · 目标 "+fps+" FPS";
        panel.UpdateState(heading,detail,metadata,displayState,busy,fps,automatic);
        enableMenu.Enabled=!busy&&displayState!="active";restoreMenu.Enabled=!busy&&displayState!="paused";
        followMenu.Enabled=!busy;
        fps30Menu.Checked=fps==30;fps60Menu.Checked=fps==60;fps30Menu.Enabled=!busy;fps60Menu.Enabled=!busy;
        string tooltip="大肥鱼 · "+heading;tray.Text=tooltip.Length>63?tooltip.Substring(0,63):tooltip;
    }
    private void Display(string title,string description,string state) {
        bool changed=heading!=title;heading=title;detail=description;displayState=state;Render();
        if(changed&&!panel.Visible&&(state=="error"||state=="active")) { tray.BalloonTipTitle="大肥鱼";tray.BalloonTipText=title;tray.ShowBalloonTip(4000); }
    }
    private void Failure(Exception error) { Program.UiLog(error.ToString());Display("暂未连接","可以重试连接，或查看日志了解原因。原宠物仍可使用。","error"); }
    private void Work(Action action,Action done) {
        if(busy||closing)return;busy=true;Render();
        Task.Run(delegate {
            Exception failure=null;try { action(); } catch(Exception error) { failure=error; }
            if(panel.IsDisposed)return;
            panel.BeginInvoke((Action)delegate {
                busy=false;if(closing)return;
                if(exitRequested) { Shutdown();return; }
                if(failure!=null)Failure(failure);else if(done!=null)done();Render();
            });
        });
    }
    private void StartController() {
        if(controller!=null) { controller.Dispose();controller=null; }
        var process=new Process { StartInfo=Program.Command("launch.mjs","start --wait-for-exit --open-pet") };
        process.OutputDataReceived+=delegate {};
        process.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e) { if(!String.IsNullOrWhiteSpace(e.Data))Program.UiLog(e.Data); };
        process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();controller=process;enabled=true;userPaused=false;
        Display("正在连接 Codex","正在等待原生宠物窗口，连接完成后会在这里显示。","starting");
    }
    private void StartPet() {
        if(busy||closing)return;
        if(controller!=null&&!controller.HasExited) {
            if(displayState=="error") { Work(StopController,StartPet);return; }
            Poll();return;
        }
        if(!Program.CoreReady()) {
            ShowPanel();using(var consent=new CoreConsent())if(consent.ShowDialog(panel)!=DialogResult.OK) { userPaused=true;Display("尚未启用","首次准备需要同意运行库许可。你可以稍后再启用。","paused");return; }
        }
        Display("正在准备","正在校验角色资源并准备运行环境。","starting");
        Work(delegate { Program.Run("setup.mjs","--accept-live2d-license");Program.Run("cli.mjs","verify"); },delegate { try { StartController(); } catch(Exception error) { Failure(error); } });
    }
    private void StopController() {
        Program.Run("launch.mjs","restore");
        if(controller!=null) {
            if(!controller.WaitForExit(12000))throw new InvalidOperationException("控制器仍在退出，请稍后重试。");
            controller.Dispose();controller=null;
        }
        enabled=false;
    }
    private void RestorePet() {
        Display("正在恢复原宠物","稍等片刻，当前 Codex 会继续运行。","starting");
        Work(StopController,delegate { userPaused=true;Display("已恢复原宠物","需要大肥鱼时，点击启用即可。Codex 会继续运行。","paused"); });
    }
    private void Reconfigure(string args) {
        Display("正在应用设置","正在更新动画设置，Codex 会继续运行。","starting");
        Work(delegate { StopController();Program.Run("launch.mjs","configure "+args); },delegate { ReadSettings();StartPet(); });
    }
    private void Poll() {
        if(Program.QuitSignal.WaitOne(0)) { Shutdown();return; }
        if(!closing)UpdateTrayIcon();
        if(Program.StartSignal.WaitOne(0)) { ShowPanel();if(!busy&&!enabled&&!userPaused)StartPet(); }
        if(busy||closing||!enabled)return;
        if(controller!=null&&controller.HasExited) {
            enabled=false;
            if(controller.ExitCode==0)Display("尚未连接","Codex 的连接已结束。下次从这里启用即可。","paused");
            else Display("暂未连接","后台连接未能启动。可以重试，或查看日志了解原因。","error");
            return;
        }
        try {
            // Read the helper's own status file. Never start Node to poll it.
            var status=Program.ReadJson(Path.Combine(Program.Data,"sessions","controller.json"));
            if(controller==null||Convert.ToInt32(status["controllerPid"])!=controller.Id)return;
            version=Program.Text(status,"version");ReadSettings();
            string state=Program.Text(status,"status");
            if(state=="active")Display("已连接 Codex","大肥鱼正在陪你工作。收起到托盘后，助手会继续运行。","active");
            else if(state=="waiting-for-codex-exit")Display("等待 Codex 正常退出","请完成当前工作，再从系统托盘完整退出 Codex。助手会随后正常重开。","waiting");
            else if(state=="waiting-for-selected-pet")Display("等待已固定的宠物","请显示已固定的宠物，或恢复自动跟随当前宠物。","waiting");
            else if(state=="waiting-or-failed")Display("暂未接入","角色暂未加载完成。可以重试连接，或查看日志了解原因。","error");
            else if(state=="expired")Display("等待已结束","本次等候已超时。准备好后，点击启用重新连接。","paused");
            else Display("正在等待宠物窗口","请确认已在 Codex 中选择并显示一个宠物。","waiting");
        } catch(IOException) {} catch(ArgumentException) {} catch(InvalidOperationException) {} catch(KeyNotFoundException) {}
    }
    private void Shutdown() {
        if(closing)return;
        foreach(Form owned in panel.OwnedForms)owned.Close();
        if(busy) { exitRequested=true;Display("正在退出","当前操作完成后会退出，并恢复原宠物。","starting");return; }
        exitRequested=false;Display("正在退出","正在恢复原宠物，Codex 会继续运行。","starting");
        Work(StopController,delegate { closing=true;timer.Stop();tray.Visible=false;panel.AllowClose=true;panel.Close();ExitThread(); });
    }
    protected override void Dispose(bool disposing) { if(disposing) { timer.Dispose();tray.Dispose();if(trayIcon!=null)trayIcon.Dispose();panel.Dispose();if(controller!=null)controller.Dispose(); }base.Dispose(disposing); }
}
