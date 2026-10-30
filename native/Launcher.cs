using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class Program {
    internal static readonly string Root=AppDomain.CurrentDomain.BaseDirectory;
    internal static readonly string Node=Path.Combine(Root,"runtime","node.exe");
    internal static readonly string Data=Environment.GetEnvironmentVariable("DAFEIYU_DATA_DIR") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CodexDafeiyuLive2D");
    internal static EventWaitHandle StartSignal;
    [STAThread] private static int Main(string[] args) {
        if(args.Contains("--self-test")) {
            try { Run("cli.mjs","verify"); return 0; } catch { return 1; }
        }
        bool owned;
        using(var mutex=new Mutex(true,"Local\\CodexDafeiyuLive2DLauncher",out owned)) {
            if(!owned) { try { using(var signal=EventWaitHandle.OpenExisting("Local\\CodexDafeiyuStart"))signal.Set(); } catch { MessageBox.Show("启动器已在系统托盘运行。","Codex 大肥鱼"); } return 0; }
            StartSignal=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\CodexDafeiyuStart");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new PetContext());
        }
        return 0;
    }
    internal static ProcessStartInfo Command(string script,string args) {
        if(!File.Exists(Node))throw new InvalidOperationException("缺少 runtime/node.exe，请完整解压便携包；源码用户请参阅 README。");
        return new ProcessStartInfo(Node,"\""+Path.Combine(Root,script)+"\" "+args) {
            WorkingDirectory=Root,UseShellExecute=false,CreateNoWindow=true,
            RedirectStandardOutput=true,RedirectStandardError=true,
            StandardOutputEncoding=System.Text.Encoding.UTF8,
            StandardErrorEncoding=System.Text.Encoding.UTF8
        };
    }
    internal static string Run(string script,string args) {
        using(var process=new Process { StartInfo=Command(script,args) }) {
            process.Start();
            var output=process.StandardOutput.ReadToEndAsync();
            var error=process.StandardError.ReadToEndAsync();
            if(!process.WaitForExit(45000)) {
                // Only this short-lived helper is terminated, never Codex.
                try { process.Kill(); } catch {}
                throw new TimeoutException("辅助命令超时，请检查网络或运行日志。");
            }
            Task.WaitAll(output,error);
            if(process.ExitCode!=0)throw new InvalidOperationException(error.Result.Trim());
            return output.Result.Trim();
        }
    }
}

internal sealed class PetContext : ApplicationContext {
    private readonly NotifyIcon tray;
    private readonly Control dispatcher=new Control();
    private readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    private Process controller;
    private bool busy;
    private bool closing;
    private bool enabled;
    private string lastStatus;
    private const string License="https://www.live2d.com/eula/live2d-proprietary-software-license-agreement_en.html";
    internal PetContext() {
        dispatcher.CreateControl();
        var menu=new ContextMenuStrip();
        menu.Items.Add("启用大肥鱼",null,delegate { StartPet(); });
        menu.Items.Add("恢复原宠物",null,delegate { RestorePet(); });
        menu.Items.Add("自动跟随当前宠物",null,delegate { Reconfigure("--pet-id auto"); });
        var fps=new ToolStripMenuItem("动画帧率");
        fps.DropDownItems.Add("60 FPS",null,delegate { Reconfigure("--fps 60"); });
        fps.DropDownItems.Add("30 FPS",null,delegate { Reconfigure("--fps 30"); });
        menu.Items.Add(fps);
        menu.Items.Add("查看本机配置和日志",null,delegate { Directory.CreateDirectory(Program.Data); Process.Start("explorer.exe","\""+Program.Data+"\""); });
        menu.Items.Add("退出启动器并恢复原宠物",null,delegate { Shutdown(); });
        tray=new NotifyIcon { Icon=SystemIcons.Application,Text="Codex 大肥鱼",ContextMenuStrip=menu,Visible=true };
        tray.DoubleClick+=delegate { Notice(lastStatus??"启动器正在运行，可右键选择操作。"); };
        timer.Interval=2000;
        timer.Tick+=delegate { Poll(); };
        timer.Start();
        dispatcher.BeginInvoke((Action)StartPet);
    }
    private void Notice(string message) {
        tray.BalloonTipTitle="Codex 大肥鱼";
        tray.BalloonTipText=message;
        tray.ShowBalloonTip(5000);
    }
    private void Work(Action action,Action done) {
        if(busy||closing)return;
        busy=true;
        Task.Run(delegate {
            Exception failure=null;
            try { action(); } catch(Exception error) { failure=error; }
            if(dispatcher.IsDisposed)return;
            dispatcher.BeginInvoke((Action)delegate {
                busy=false;
                if(failure!=null) { MessageBox.Show(failure.Message,"Codex 大肥鱼",MessageBoxButtons.OK,MessageBoxIcon.Information); return; }
                if(done!=null)done();
            });
        });
    }
    private bool PrepareConsent() {
        string environment=Program.Run("launch.mjs","environment");
        if(environment.Contains("\"coreReady\":true"))return true;
        Process.Start(new ProcessStartInfo(License) { UseShellExecute=true });
        return MessageBox.Show("首次使用需要从 Live2D 官方下载 Cubism Core（约 203 KB）。\n\n许可页面已打开。是否同意该许可并下载？\n\n选择“否”会保留原宠物。","Live2D Core 许可",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes;
    }
    private void StartController() {
        var process=new Process { StartInfo=Program.Command("launch.mjs","start --wait-for-exit --open-pet") };
        process.OutputDataReceived+=delegate {};
        process.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e) {
            if(String.IsNullOrWhiteSpace(e.Data))return;
            try { Directory.CreateDirectory(Program.Data); File.AppendAllText(Path.Combine(Program.Data,"launcher.log"),DateTime.UtcNow.ToString("o")+" "+e.Data+Environment.NewLine); } catch {}
        };
        process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();controller=process;enabled=true;
    }
    private void StartPet() {
        if(busy||closing)return;
        if(controller!=null&&!controller.HasExited) { Notice("大肥鱼控制器正在运行。"); return; }
        try { if(!PrepareConsent())return; } catch(Exception error) { MessageBox.Show(error.Message,"Codex 大肥鱼"); return; }
        Notice("正在准备运行库并连接 Codex。若当前 Codex 未开启连接端口，请从系统托盘完整退出；启动器会等候正常退出后重开。");
        Work(delegate { Program.Run("setup.mjs","--accept-live2d-license"); Program.Run("cli.mjs","verify"); },delegate {
            try { StartController(); } catch(Exception error) { MessageBox.Show(error.Message,"Codex 大肥鱼"); }
        });
    }
    private void StopController() {
        Program.Run("launch.mjs","restore");
        if(controller!=null) {
            if(!controller.WaitForExit(12000))throw new InvalidOperationException("控制器仍在退出，请稍后重试。Codex 没有被关闭。");
            controller.Dispose();controller=null;
        }
        enabled=false;
    }
    private void RestorePet() {
        Work(StopController,delegate { lastStatus="已恢复原宠物。"; Notice(lastStatus); });
    }
    private void Reconfigure(string args) {
        Work(delegate { StopController(); Program.Run("launch.mjs","configure "+args); },StartPet);
    }
    private void Poll() {
        if(Program.StartSignal.WaitOne(0)) { StartPet(); return; }
        if(busy||closing||!enabled)return;
        if(controller!=null&&controller.HasExited) {
            enabled=false;
            Notice(controller.ExitCode==0?"Codex 已退出，大肥鱼控制器已停止。下次请从启动器菜单启用。":"控制器未能启动，请查看本机日志。原宠物仍可使用。");
            return;
        }
        Work(delegate {
            var status=Program.Run("launch.mjs","status");
            string message=status.Contains("\"status\": \"active\"")?"大肥鱼已接入 Codex。":
                status.Contains("waiting-for-codex-exit")?"请从系统托盘完整退出 Codex；启动器会正常重开，不会强制关闭你的聊天。":
                status.Contains("waiting-for-selected-pet")?"请显示已固定绑定的宠物，或在托盘选择自动跟随当前宠物。":
                status.Contains("waiting-or-failed")?"当前客户端未能完成接入，请查看日志；可以恢复原宠物。":
                "正在等待原生宠物窗口；请确认已在 Codex 中开启宠物。";
            if(message!=lastStatus)dispatcher.BeginInvoke((Action)delegate { lastStatus=message; Notice(message); });
        },null);
    }
    private void Shutdown() {
        if(busy||closing)return;
        Work(StopController,delegate {
            closing=true;timer.Stop();tray.Visible=false;tray.Dispose();dispatcher.Dispose();ExitThread();
        });
    }
    protected override void Dispose(bool disposing) {
        if(disposing) { timer.Dispose();tray.Dispose();dispatcher.Dispose();if(controller!=null)controller.Dispose(); }
        base.Dispose(disposing);
    }
}
