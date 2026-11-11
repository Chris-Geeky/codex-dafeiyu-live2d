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
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window,int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
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
        if(args.Contains("--codex-only")) {
            bool launchOwned;
            using(var launchMutex=new Mutex(true,"Local\\CodexDafeiyuCodexOnly",out launchOwned))
                return launchOwned?OpenCodexOnly():0;
        }
        int themeAt=Array.IndexOf(args,"--theme");ApplyTheme(themeAt>=0&&themeAt+1<args.Length?args[themeAt+1]:Text(ReadPreferences(),"theme","system"));
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
    private static int OpenCodexOnly() {
        try {
            var result=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(Run("launch.mjs","open-codex"));
            if(Text(result,"status")=="already-running") {
                try {
                    using(var process=Process.GetProcessById(Convert.ToInt32(result["appPid"]))) {
                        var window=process.MainWindowHandle;
                        if(window!=IntPtr.Zero){if(IsIconic(window))ShowWindowAsync(window,9);SetForegroundWindow(window);}
                    }
                }catch(ArgumentException) {}catch(InvalidOperationException) {}
            }
            return 0;
        }catch(Exception error) {
            UiLog(error.ToString());
            MessageBox.Show("未能打开 Codex。可以用原来的图标启动，或在大肥鱼助手中查看日志。","打开 Codex",MessageBoxButtons.OK,MessageBoxIcon.Information);
            return 1;
        }
    }
    private static int Preview(string[] args) {
        IsPreview=true;
        int at=Array.IndexOf(args,"--preview"),stateAt=Array.IndexOf(args,"--state");
        if(at+1>=args.Length)throw new ArgumentException("Missing preview path");
        string state=stateAt>=0&&stateAt+1<args.Length?args[stateAt+1]:"active";
        if(state.StartsWith("tray",StringComparison.Ordinal)) {
            using(var menu=new PetTrayMenu()) {
                menu.UpdateState(state=="tray-handoff"?"等待你完整退出 Codex":"已连接 Codex",state=="tray-handoff"?"handoff-wait":"active",false,60,true);
                menu.Show(new Point(Screen.PrimaryScreen.WorkingArea.Left+20,Screen.PrimaryScreen.WorkingArea.Top+20));Application.DoEvents();
                ToolStrip captureMenu=menu;
                if(state=="tray-fps") {var rates=(ToolStripMenuItem)menu.Items["Trayfps"];rates.ShowDropDown();Application.DoEvents();captureMenu=rates.DropDown;}
                string output=Path.GetFullPath(args[at+1]);Directory.CreateDirectory(Path.GetDirectoryName(output));
                using(var bitmap=new Bitmap(captureMenu.Width,captureMenu.Height)) {captureMenu.DrawToBitmap(bitmap,new Rectangle(Point.Empty,captureMenu.Size));bitmap.Save(output,System.Drawing.Imaging.ImageFormat.Png);}
                File.WriteAllText(output+".layout.json",new JavaScriptSerializer().Serialize(new {dpi=Program.SystemDpi(),width=captureMenu.Width,height=captureMenu.Height}));
                menu.Close();return 0;
            }
        }
        Form form;
        if(state=="license")form=new CoreConsent();
        else if(state=="close")form=new CloseChoice();
        else if(state=="appearance"||state=="appearance-live")form=new AppearanceWebDialog(null,state=="appearance-live");
        else if(state=="about")form=new AboutDialog();
        else {
            var dashboard=new Dashboard();dashboard.AllowClose=true;
            dashboard.UpdateState(state=="restore-warning"?"大肥鱼已停用，原宠物未显示":state=="waiting"?"等待 Codex 正常退出":state=="error"?"暂未连接":"已连接 Codex",
                state=="restore-warning"?"刷新宠物窗口后，原图仍未加载。可再次恢复，或启用大肥鱼。":state=="waiting"?"请完成当前工作，再从系统托盘完整退出 Codex。助手会随后正常重开。":state=="error"?"角色暂未加载完成。可以重试连接，或查看日志了解原因。":"大肥鱼正在陪你工作。收起到托盘后，助手会继续运行。",
                "界面预览 · 自动跟随当前宠物 · 目标 60 FPS",state=="restore-warning"?state:state=="waiting"?"waiting":state=="error"?"error":"active",false,60,true);
            if(state=="watching")dashboard.UpdateState("等待你打开 Codex","助手继续监测。点“打开 Codex”或再次运行大肥鱼入口，即可一起打开并接入。","自动监测 · 目标 60 FPS",state,false,60,true);
            if(state=="handoff-wait")dashboard.UpdateState("等待你完整退出 Codex","已安排自动接入。请从系统托盘右键 Codex → 退出，助手会随后重开。只关窗口不算退出。","等待接续 · 目标 60 FPS",state,false,60,true);
            if(state=="relaunch-needed")dashboard.UpdateState("需要重开 Codex 才能接入","这次 Codex 未开启宠物接入通道。点“安排接入”，再从托盘完整退出一次，助手会接续打开。","自动监测 · 目标 60 FPS",state,false,60,true);
            int previewTheme=Array.IndexOf(args,"--theme");dashboard.UpdateOptions(previewTheme>=0&&previewTheme+1<args.Length?args[previewTheme+1]:Text(ReadPreferences(),"theme","system"),true,args.Contains("--early-avoidance"),"press",100,false);
            form=dashboard;
        }
        using(form) {
            form.StartPosition=FormStartPosition.Manual;form.Location=new Point(Screen.PrimaryScreen.WorkingArea.Left+20,Screen.PrimaryScreen.WorkingArea.Top+20);form.Opacity=0;form.ShowInTaskbar=false;form.Show();Application.DoEvents();
            if(state=="options-menu"){
                var menu=((Dashboard)form).ShowOptionsMenu();Application.DoEvents();
                string output=Path.GetFullPath(args[at+1]);Directory.CreateDirectory(Path.GetDirectoryName(output));
                using(var bitmap=new Bitmap(menu.Width,menu.Height)){
                    menu.DrawToBitmap(bitmap,new Rectangle(Point.Empty,menu.Size));bitmap.Save(output,System.Drawing.Imaging.ImageFormat.Png);
                }
                menu.Close();form.Hide();form.Close();return 0;
            }
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
    internal static Dictionary<string,object> ReadPreferences() {try {return ReadJson(Path.Combine(Data,"preferences.json"));}catch{return new Dictionary<string,object>{{"theme","system"},{"dragCompatibility",true},{"earlyAvoidance",false},{"avoidanceTrigger","press"},{"previewBackground","theme"}};} }
    // This window preference belongs to the launcher, independent of the renderer.
    internal static string ReadCloseBehavior() {
        try {var value=File.ReadAllText(Path.Combine(Data,"close-behavior.txt")).Trim();if(value=="tray"||value=="exit")return value;}
        catch(IOException) {} catch(UnauthorizedAccessException) {}
        return "ask";
    }
    internal static void WriteCloseBehavior(string value) {
        if(value!="ask"&&value!="tray"&&value!="exit")throw new ArgumentException("Invalid close behavior");
        Directory.CreateDirectory(Data);
        var file=Path.Combine(Data,"close-behavior.txt");var temp=file+".tmp";
        File.WriteAllText(temp,value+"\n",new UTF8Encoding(false));
        if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);
    }
    internal static Point? ReadPanelPosition() {
        try {
            var values=File.ReadAllText(Path.Combine(Data,"panel-position.txt")).Trim().Split(',');
            int x,y;
            if(values.Length==2&&Int32.TryParse(values[0],out x)&&Int32.TryParse(values[1],out y)&&
                Math.Abs((long)x)<=1000000&&Math.Abs((long)y)<=1000000)return new Point(x,y);
        } catch(IOException) {} catch(UnauthorizedAccessException) {}
        return null;
    }
    internal static void WritePanelPosition(Point position) {
        Directory.CreateDirectory(Data);
        var file=Path.Combine(Data,"panel-position.txt");var temp=file+".tmp";
        File.WriteAllText(temp,String.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1}\n",position.X,position.Y),new UTF8Encoding(false));
        if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);
    }
    internal static Point ConstrainPanelPosition(Point position,Size size,Rectangle work) {
        return new Point(Math.Max(work.Left,Math.Min(position.X,work.Right-Math.Min(size.Width,work.Width))),
            Math.Max(work.Top,Math.Min(position.Y,work.Bottom-Math.Min(size.Height,work.Height))));
    }
    internal static void ApplyTheme(string theme) {if(!Palette.Configure(theme))return;foreach(Form form in Application.OpenForms){var app=form as AppForm;if(app!=null)app.RefreshTheme();} }
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
    private readonly PetTrayMenu trayMenu;
    private Process controller;
    private readonly NativeDragGuard dragGuard;
    private Icon trayIcon;
    private int trayPixels;
    private bool busy,closing,exitRequested,enabled,userPaused;
    private int fps=60,petSize=100;
    private int? pendingPetSize;
    private Point? savedPanelPosition;
    private bool automatic=true;
    private bool appearanceOpen;
    private string restoreNotice="";
    private bool stoppedWithoutPet;
    private string theme="system";
    private bool dragCompatibility=true,earlyAvoidance;
    private string avoidanceTrigger="press";
    private string version="",heading="准备连接",detail="正在准备大肥鱼的运行环境。",displayState="starting";
    internal PetContext() {
        dragGuard=new NativeDragGuard(delegate(bool moving) {if(controller!=null&&!controller.HasExited){controller.StandardInput.WriteLine("{\"type\":\"window-drag\",\"moving\":"+(moving?"true":"false")+",\"nativeFastHideCount\":"+dragGuard.FastHideCount+"}");controller.StandardInput.Flush();}});
        MainForm=panel;ReadSettings();panel.CloseBehavior=Program.ReadCloseBehavior();
        savedPanelPosition=Program.ReadPanelPosition();
        if(savedPanelPosition.HasValue){panel.StartPosition=FormStartPosition.Manual;panel.Location=savedPanelPosition.Value;}
        panel.PositionSettled=RememberPanelPosition;
        panel.EnableRequested=StartPet;panel.RestoreRequested=RestorePet;panel.FollowRequested=delegate { Reconfigure("--pet-id auto"); };
        panel.FpsRequested=delegate(int requested) { if(requested!=fps)Reconfigure("--fps "+requested); };
        panel.PetSizeRequested=SavePetSize;
        panel.HideRequested=HidePanel;panel.ExitRequested=Shutdown;panel.LogsRequested=Program.OpenLogs;
        panel.CloseActionRequested=delegate(string action,bool remember) {
            if(remember&&!SetCloseBehavior(action))return;
            if(action=="exit")Shutdown();else HidePanel();
        };
        panel.ClosePreferenceRequested=delegate(string choice){SetCloseBehavior(choice);};
        panel.AboutRequested=delegate { using(var about=new AboutDialog())about.ShowDialog(panel); };
        panel.AppearanceRequested=OpenAppearance;
        panel.ThemeRequested=delegate(string choice){SavePreference("theme",choice);};
        panel.DragCompatibilityRequested=delegate(bool choice){SavePreference("dragCompatibility",choice);};
        panel.EarlyAvoidanceRequested=delegate(bool choice){SavePreference("earlyAvoidance",choice);};
        panel.AvoidanceTriggerRequested=delegate(string choice){SavePreference("avoidanceTrigger",choice);};
        trayMenu=new PetTrayMenu();trayMenu.PanelRequested=ShowPanel;trayMenu.EnableRequested=StartPet;
        trayMenu.RestoreRequested=RestorePet;trayMenu.FollowRequested=delegate {Reconfigure("--pet-id auto");};
        trayMenu.AppearanceRequested=OpenAppearance;trayMenu.LogsRequested=Program.OpenLogs;trayMenu.ExitRequested=Shutdown;
        trayMenu.CloseBehaviorRequested=delegate(string choice){SetCloseBehavior(choice);};
        trayMenu.FpsRequested=delegate(int requested) {if(requested!=fps)Reconfigure("--fps "+requested);};
        tray=new NotifyIcon { Text="大肥鱼 · 准备连接",ContextMenuStrip=trayMenu,Visible=false };UpdateTrayIcon();tray.Visible=true;
        tray.DoubleClick+=delegate { ShowPanel(); };
        bool firstShow=true;panel.Shown+=delegate { if(firstShow) { firstShow=false;KeepPanelVisible();panel.BeginInvoke((Action)StartPet); } };
        timer.Interval=1500;timer.Tick+=delegate { Poll(); };timer.Start();Render();panel.Show();
    }
    private void KeepPanelVisible() {
        if(panel.WindowState!=FormWindowState.Normal)return;
        Point location=Program.ConstrainPanelPosition(panel.Location,panel.Size,Screen.FromRectangle(panel.Bounds).WorkingArea);
        if(location!=panel.Location)panel.Location=location;
    }
    private void RememberPanelPosition() {
        if(panel.IsDisposed||!panel.Visible||panel.WindowState!=FormWindowState.Normal||savedPanelPosition==panel.Location)return;
        try {Program.WritePanelPosition(panel.Location);savedPanelPosition=panel.Location;}
        catch(IOException error) {Program.UiLog(error.Message);}
        catch(UnauthorizedAccessException error) {Program.UiLog(error.Message);}
    }
    private void HidePanel(){RememberPanelPosition();panel.Hide();}
    private void ShowPanel() { if(!panel.Visible)panel.Show();if(panel.WindowState==FormWindowState.Minimized)panel.WindowState=FormWindowState.Normal;KeepPanelVisible();panel.BringToFront();panel.Activate(); }
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
        try {
            var settings=Program.ReadJson(Path.Combine(Program.Data,"settings.json"));
            fps=Convert.ToInt32(settings["fps"]);automatic=!settings.ContainsKey("petId")||settings["petId"]==null;
            int size;petSize=Int32.TryParse(Program.Text(settings,"customPetSize",Program.Text(settings,"petSize","100")),out size)&&
                size>=SizeSlider.Minimum&&size<=SizeSlider.Maximum?size:SizeSlider.DefaultPercent;
        } catch {}
        var preferences=Program.ReadPreferences();theme=Program.Text(preferences,"theme","system");dragCompatibility=!Program.Text(preferences,"dragCompatibility","True").Equals("False",StringComparison.OrdinalIgnoreCase);earlyAvoidance=Program.Text(preferences,"earlyAvoidance","False").Equals("True",StringComparison.OrdinalIgnoreCase);avoidanceTrigger=Program.Text(preferences,"avoidanceTrigger","press");Program.ApplyTheme(theme);
    }
    private void SavePreference(string key,object value) {if(busy||closing)return;string encoded=Convert.ToBase64String(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(new Dictionary<string,object>{{key,value}})));Work(delegate{Program.Run("preferences.mjs","set "+encoded);},delegate{ReadSettings();Render();});}
    private void SavePetSize(int requested) {
        if(busy||closing||requested==petSize)return;
        pendingPetSize=requested;
        Work(delegate{Program.Run("launch.mjs","set-pet-size "+requested);},
            delegate{pendingPetSize=null;ReadSettings();},
            delegate(Exception error){
                pendingPetSize=null;Program.UiLog(error.ToString());
                Display(heading,"未能保存宠物大小。可以重试，或从上方更多选项查看日志。",displayState);
            });
    }
    private bool SetCloseBehavior(string value) {
        if(value==panel.CloseBehavior)return true;
        try {Program.WriteCloseBehavior(value);}
        catch(IOException error) {return CloseBehaviorWriteFailed(error);}
        catch(UnauthorizedAccessException error) {return CloseBehaviorWriteFailed(error);}
        panel.CloseBehavior=value;trayMenu.UpdateCloseBehavior(value);return true;
    }
    private bool CloseBehaviorWriteFailed(Exception error) {
        Program.UiLog(error.ToString());ShowPanel();Display(heading,"未能保存关闭偏好。请重试，或查看日志。",displayState);return false;
    }
    private void Render() {
        if(panel.IsDisposed)return;
        string metadata=(String.IsNullOrEmpty(version)?"本机连接":"Codex "+version)+" · "+(automatic?"自动跟随当前宠物":"固定宠物")+" · 目标 "+fps+" FPS";
        panel.UpdateState(heading,detail,metadata,displayState,busy,fps,automatic);
        panel.UpdateOptions(theme,dragCompatibility,earlyAvoidance,avoidanceTrigger,pendingPetSize??petSize,busy);
        trayMenu.UpdateState(heading,displayState,busy,fps,automatic);
        trayMenu.UpdateCloseBehavior(panel.CloseBehavior);
        string tooltip="大肥鱼 · "+heading;tray.Text=tooltip.Length>63?tooltip.Substring(0,63):tooltip;
    }
    private void Display(string title,string description,string state) {
        bool changed=heading!=title;heading=title;detail=description;displayState=state;Render();
        if(changed&&!panel.Visible&&(state=="error"||state=="active"||state=="restore-warning")) { tray.BalloonTipTitle="大肥鱼";tray.BalloonTipText=title;tray.ShowBalloonTip(4000); }
    }
    private void Failure(Exception error) { Program.UiLog(error.ToString());Display("暂未连接","可以重试连接，或查看日志了解原因。Codex 会继续运行。","error"); }
    private void Work(Action action,Action done,Action<Exception> failed=null) {
        if(busy||closing)return;busy=true;Render();
        Task.Run(delegate {
            Exception failure=null;try { action(); } catch(Exception error) { failure=error; }
            if(panel.IsDisposed)return;
            panel.BeginInvoke((Action)delegate {
                busy=false;if(closing)return;
                if(exitRequested) { Shutdown();return; }
                if(failure!=null){if(failed==null)Failure(failure);else failed(failure);}else if(done!=null)done();Render();
            });
        });
    }
    private void StartController() {
        if(controller!=null) { controller.Dispose();controller=null; }
        var process=new Process { StartInfo=Program.Command("launch.mjs","start --wait-for-exit --open-pet --watch-codex --native-helper") };
        process.StartInfo.RedirectStandardInput=true;
        process.OutputDataReceived+=delegate {};
        process.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e) { if(!String.IsNullOrWhiteSpace(e.Data))Program.UiLog(e.Data); };
        process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();controller=process;enabled=true;userPaused=false;
        Display("正在连接 Codex","正在等待原生宠物窗口，连接完成后会在这里显示。","starting");
    }
    private void StartPet() {
        if(busy||closing)return;
        if(controller!=null&&!controller.HasExited) {
            if(displayState!="active")try {controller.StandardInput.WriteLine("{\"type\":\"reconnect\"}");controller.StandardInput.Flush();}catch(Exception error){Failure(error);return;}
            Poll();return;
        }
        if(!Program.CoreReady()) {
            ShowPanel();using(var consent=new CoreConsent())if(consent.ShowDialog(panel)!=DialogResult.OK) { userPaused=true;Display("尚未启用","首次准备需要同意运行库许可。你可以稍后再启用。","paused");return; }
        }
        Display("正在准备","正在校验角色资源并准备运行环境。","starting");
        Work(delegate { Program.Run("setup.mjs","--accept-live2d-license");Program.Run("cli.mjs","verify"); },delegate { try { StartController(); } catch(Exception error) { Failure(error); } });
    }
    private void StopController() {
        var result=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(Program.Run("launch.mjs","restore"));
        if(controller!=null) {
            if(!controller.WaitForExit(12000))throw new InvalidOperationException("控制器仍在退出，请稍后重试。");
            controller.Dispose();controller=null;
        }
        enabled=false;
        string reason=Program.Text(result,"reason");stoppedWithoutPet=reason=="not-attached"||reason=="app-exited"||reason=="not-started";
        restoreNotice=Program.Text(result,"restored").Equals("True",StringComparison.OrdinalIgnoreCase)?"":"原宠物图片未能确认显示："+Program.Text(result,"reason","unknown");
        if(restoreNotice!="")Program.UiLog(restoreNotice);
    }
    private void RestorePet() {
        dragGuard.Configure(0,false);
        Display("正在恢复原宠物","稍等片刻，当前 Codex 会继续运行。","starting");
        Work(StopController,delegate { userPaused=true;if(restoreNotice=="")Display(stoppedWithoutPet?"已停止自动接入":"已恢复原宠物",stoppedWithoutPet?"自动检测已停止。需要大肥鱼时，点击启用即可。":"原图已加载。需要大肥鱼时，点击启用即可。","paused");else Display("大肥鱼已停用，原宠物未显示","原图仍未加载或连接不可用。可再次恢复、启用大肥鱼，或查看日志。","restore-warning"); });
    }
    private void Reconfigure(string args) {
        dragGuard.Configure(0,false);
        Display("正在应用设置","正在更新动画设置，Codex 会继续运行。","starting");
        Work(delegate { StopController();Program.Run("launch.mjs","configure "+args); },delegate { ReadSettings();StartPet(); });
    }
    private void Poll() {
        if(Program.QuitSignal.WaitOne(0)) { Shutdown();return; }
        if(!closing)UpdateTrayIcon();
        if(Program.StartSignal.WaitOne(0)) { ShowPanel();if(!busy&&!userPaused&&displayState!="active")StartPet(); }
        ReadSettings();if(busy||closing||!enabled){dragGuard.Configure(0,false);return;}
        if(controller!=null&&controller.HasExited) {
            dragGuard.Configure(0,false);
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
            uint appPid=status.ContainsKey("appPid")&&status["appPid"]!=null?Convert.ToUInt32(status["appPid"]):0;dragGuard.SetAvoidanceTrigger(avoidanceTrigger);dragGuard.SetEarlyAvoidance(earlyAvoidance);dragGuard.Configure(appPid,dragCompatibility&&state=="active");
            object rawStatus,rawBounds;if(status.TryGetValue("lastStatus",out rawStatus)&&rawStatus is Dictionary<string,object>&&((Dictionary<string,object>)rawStatus).TryGetValue("windowBounds",out rawBounds)&&rawBounds is Dictionary<string,object>){var bounds=(Dictionary<string,object>)rawBounds;double dpr=Convert.ToDouble(bounds["dpr"]);dragGuard.TrackPet(appPid,(int)Math.Round(Convert.ToDouble(bounds["x"])*dpr),(int)Math.Round(Convert.ToDouble(bounds["y"])*dpr),(int)Math.Round(Convert.ToDouble(bounds["width"])*dpr),(int)Math.Round(Convert.ToDouble(bounds["height"])*dpr));}
            if(state=="active")Display("已连接 Codex","大肥鱼正在陪你工作。收起到托盘后，助手会继续运行。","active");
            else if(state=="waiting-for-codex-exit") {
                bool handoff=Program.Text(status,"autoRelaunch","False").Equals("True",StringComparison.OrdinalIgnoreCase);
                Display(handoff?"等待你完整退出 Codex":"需要重开 Codex 才能接入",handoff?"已安排自动接入。请从系统托盘右键 Codex → 退出，助手会随后重开。只关窗口不算退出。":"这次 Codex 未开启宠物接入通道。点“安排接入”，再从托盘完整退出一次，助手会接续打开。",handoff?"handoff-wait":"relaunch-needed");
            }
            else if(state=="watching-for-codex")Display("等待你打开 Codex","助手继续监测。点“打开 Codex”或再次运行大肥鱼入口，即可一起打开并接入。","watching");
            else if(state=="reconnecting")Display("正在重新连接 Codex","连接暂时中断，正在检测当前进程；助手会继续等待并重新接入。","starting");
            else if(state=="waiting-for-selected-pet")Display("等待已固定的宠物","请显示已固定的宠物，或恢复自动跟随当前宠物。","waiting");
            else if(state=="waiting-or-failed")Display("暂未接入","角色暂未加载完成。可以重试连接，或查看日志了解原因。","error");
            else if(state=="expired")Display("等待已结束","本次等候已超时。准备好后，点击启用重新连接。","paused");
            else Display("正在等待宠物窗口","请确认已在 Codex 中选择并显示一个宠物。","waiting");
        } catch(IOException) {} catch(ArgumentException) {} catch(InvalidOperationException) {} catch(KeyNotFoundException) {}
    }
    private void Shutdown() {
        if(closing)return;
        RememberPanelPosition();
        dragGuard.Configure(0,false);
        foreach(Form owned in panel.OwnedForms)owned.Close();
        if(busy) { exitRequested=true;Display("正在退出","当前操作完成后会退出，并恢复原宠物。","starting");return; }
        exitRequested=false;Display("正在退出","正在恢复原宠物，Codex 会继续运行。","starting");
        Work(StopController,delegate { closing=true;timer.Stop();tray.Visible=false;panel.AllowClose=true;panel.Close();ExitThread(); });
    }
    protected override void Dispose(bool disposing) { if(disposing) { dragGuard.Dispose();timer.Dispose();tray.Dispose();trayMenu.Dispose();if(trayIcon!=null)trayIcon.Dispose();panel.Dispose();if(controller!=null)controller.Dispose(); }base.Dispose(disposing); }
}
