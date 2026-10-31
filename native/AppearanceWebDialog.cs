using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

internal sealed class AppearanceWebDialog : AppForm {
    private WebView2 web;
    private Process server;
    private string origin;
    private bool ready,working,disposed;
    private readonly bool liveTest;
    private readonly Dictionary<string,object> initial;
    internal bool Ready { get { return ready; } }
    internal static bool IsAvailable() { try { return !String.IsNullOrEmpty(CoreWebView2Environment.GetAvailableBrowserVersionString()); } catch { return false; } }
    internal AppearanceWebDialog(Dictionary<string,object> selection,bool test=false) {
        liveTest=test;initial=selection;SuspendLayout();AutoScaleMode=AutoScaleMode.None;Font=Palette.Font(10);
        BackColor=Palette.Background;ClientSize=new Size(980,780);Text="表情与装扮 · 大肥鱼";Icon=Program.AppIcon;
        FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
        if(Program.IsPreview&&!liveTest) {
            using(var stream=typeof(Program).Assembly.GetManifestResourceStream("Dafeiyu.AppearancePreview"))using(var image=Image.FromStream(stream))Controls.Add(new PictureBox { Image=new Bitmap(image),Bounds=new Rectangle(0,0,980,780),SizeMode=PictureBoxSizeMode.StretchImage });
        } else { web=new WebView2 { Bounds=new Rectangle(0,0,980,780),DefaultBackgroundColor=Palette.Background };Controls.Add(web);Shown+=delegate { Initialize(); }; }
        FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(working)e.Cancel=true; };FinishLayout();
    }
    private async void Initialize() {
        try {
            var start=Program.Command("preview-server.mjs",Process.GetCurrentProcess().Id.ToString());start.RedirectStandardInput=true;
            server=new Process { StartInfo=start };server.Start();server.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e) { if(e.Data!=null)Program.UiLog("Preview resource server: "+e.Data); };server.BeginErrorReadLine();
            string line=await server.StandardOutput.ReadLineAsync();if(disposed)return;
            var endpoint=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(line);origin=Program.Text(endpoint,"url");
            var uri=new Uri(origin);if(uri.Scheme!="http"||uri.Host!="127.0.0.1")throw new InvalidOperationException("Invalid local preview endpoint");
            var options=new CoreWebView2EnvironmentOptions();
            if(liveTest)options.AdditionalBrowserArguments="--disable-backgrounding-occluded-windows --disable-renderer-backgrounding --disable-background-timer-throttling --disable-features=CalculateNativeWinOcclusion";
            var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Program.Data,"preview-webview"),options);if(disposed)return;
            await web.EnsureCoreWebView2Async(environment);if(disposed)return;
            web.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;web.CoreWebView2.Settings.AreDevToolsEnabled=false;web.CoreWebView2.Settings.AreHostObjectsAllowed=false;web.CoreWebView2.Settings.IsStatusBarEnabled=false;web.CoreWebView2.Settings.IsZoomControlEnabled=false;
            web.CoreWebView2.NavigationStarting+=delegate(object sender,CoreWebView2NavigationStartingEventArgs e) { if(!e.Uri.StartsWith(origin,StringComparison.Ordinal))e.Cancel=true; };
            web.CoreWebView2.NewWindowRequested+=delegate(object sender,CoreWebView2NewWindowRequestedEventArgs e) { e.Handled=true; };
            web.CoreWebView2.PermissionRequested+=delegate(object sender,CoreWebView2PermissionRequestedEventArgs e) { e.State=CoreWebView2PermissionState.Deny; };
            web.CoreWebView2.DownloadStarting+=delegate(object sender,CoreWebView2DownloadStartingEventArgs e) { e.Cancel=true; };
            web.CoreWebView2.WebMessageReceived+=Receive;
            web.CoreWebView2.Navigate(origin);
        } catch(Exception error) { if(disposed)return;Program.UiLog("Preview initialization: "+error.GetType().Name);web.Visible=false;Palette.Label(this,"预览暂未准备好，可打开基础设置继续使用。",24,30,850,40,11);var fallback=new SoftButton("打开基础设置",24,90,220,40,true);fallback.Click+=delegate { using(var dialog=new AppearanceDialog(initial))dialog.ShowDialog(this); };Controls.Add(fallback); }
    }
    private void Send(object value) { if(!disposed&&web.CoreWebView2!=null)web.CoreWebView2.PostWebMessageAsJson(new JavaScriptSerializer().Serialize(value)); }
    private async void Receive(object sender,CoreWebView2WebMessageReceivedEventArgs e) {
        if(disposed||!e.Source.StartsWith(origin,StringComparison.Ordinal)||e.WebMessageAsJson.Length>32768)return;
        bool ownsWork=false;
        try {
            var data=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(e.WebMessageAsJson);string type=Program.Text(data,"type");
            if(type=="preview-status") { ready=true;var status=(Dictionary<string,object>)data["status"];status["hostPid"]=Process.GetCurrentProcess().Id;status["readOnly"]=liveTest;try { Directory.CreateDirectory(Path.Combine(Program.Data,"sessions"));File.WriteAllText(Path.Combine(Program.Data,"sessions","preview-status.json"),new JavaScriptSerializer().Serialize(status),Encoding.UTF8); } catch(IOException) {} catch(UnauthorizedAccessException) {}return; }
            if(type=="close") { if(!working)Close();return; }
            if(working)return;
            if(liveTest) { Send(new {type="error",message="界面预览模式不会更改宠物。"});return; }
            if(type=="preferences") {
                working=true;ownsWork=true;string encoded=Convert.ToBase64String(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(data["preferences"])));
                string preferenceOutput=await Task.Run(delegate{return Program.Run("preferences.mjs","set "+encoded);});if(disposed)return;
                var preferences=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(preferenceOutput);Program.ApplyTheme(Program.Text(preferences,"theme","system"));Send(new {type="preferences",preferences});return;
            }
            string command;
            if(type=="apply")command="apply "+Convert.ToBase64String(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(data["selection"])));
            else if(type=="effect") {
                string id=Program.Text(data,"id");if(id!="draw-undo"&&id!="menu-press"&&id!="ketchup-squeeze")throw new InvalidOperationException("Unknown effect");command="effect "+id;
            } else return;
            working=true;ownsWork=true;string output=await Task.Run(delegate { return Program.Run("appearance.mjs",command); });if(disposed)return;
            var result=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(output);result["type"]=type=="apply"?"applied":"effect-result";Send(result);
        } catch(Exception error) { Program.UiLog("Appearance action: "+error.GetType().Name);Send(new {type="error",message="操作暂未完成，可重新应用或查看日志。"}); }
        finally { if(ownsWork)working=false; }
    }
    internal async Task CaptureAsync(string file) { using(var stream=File.Create(file))await web.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream); }
    protected override void Dispose(bool disposing) {
        disposed=true;if(disposing) { if(web!=null) { web.Dispose();try { File.WriteAllText(Path.Combine(Program.Data,"sessions","preview-status.json"),"{\"ready\":false,\"closed\":true}",Encoding.UTF8); } catch {} }if(server!=null) { try { server.StandardInput.Close();if(!server.WaitForExit(1500))server.Kill(); } catch {}server.Dispose(); } }base.Dispose(disposing);
    }
}
