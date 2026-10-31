using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal static class Palette {
    internal static Color Background=Color.FromArgb(242,246,252),Surface=Color.White;
    internal static Color Ink=Color.FromArgb(27,42,68),Muted=Color.FromArgb(103,121,145),Line=Color.FromArgb(220,229,241);
    private static Color previousMuted=Muted;
    internal static bool Dark;
    internal static bool Configure(string theme) {
        bool dark=theme=="dark";
        if(theme=="system")try { using(var key=Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize"))dark=Convert.ToInt32(key.GetValue("AppsUseLightTheme",1))==0; } catch {}
        if(dark==Dark)return false;previousMuted=Muted;Dark=dark;
        Background=dark?Color.FromArgb(17,24,35):Color.FromArgb(242,246,252);Surface=dark?Color.FromArgb(26,36,51):Color.White;
        Ink=dark?Color.FromArgb(230,237,247):Color.FromArgb(27,42,68);Muted=dark?Color.FromArgb(157,174,197):Color.FromArgb(103,121,145);Line=dark?Color.FromArgb(48,64,88):Color.FromArgb(220,229,241);return true;
    }
    internal static void Apply(Control parent) {
        parent.BackColor=parent is CardPanel?Color.Transparent:parent.Parent is CardPanel?Surface:Background;
        if(parent is Label&&!(parent is LinkLabel)) { bool muted=Object.Equals(parent.Tag,"muted")||parent.ForeColor==previousMuted||parent.ForeColor==Muted;if(muted)parent.Tag="muted";parent.ForeColor=muted?Muted:Ink; }
        else parent.ForeColor=Ink;
        var link=parent as LinkLabel;if(link!=null)link.LinkColor=link.ActiveLinkColor=link.VisitedLinkColor=Accent;
        if(parent is ComboBox)parent.BackColor=Surface;
        foreach(Control control in parent.Controls)Apply(control);parent.Invalidate();
    }
    internal static readonly Color Blue=Color.FromArgb(44,105,224);
    internal static Color Accent {get{return Dark?Color.FromArgb(148,184,255):Blue;}}
    internal static readonly Color Green=Color.FromArgb(28,141,99);
    internal static readonly Color Amber=Color.FromArgb(183,123,23);
    internal static readonly Color Red=Color.FromArgb(184,70,74);
    internal static Font Font(float size,bool bold=false) { return new Font("Microsoft YaHei UI",size,bold?FontStyle.Bold:FontStyle.Regular,GraphicsUnit.Point); }
    internal static GraphicsPath Rounded(RectangleF rectangle,float radius) {
        float diameter=Math.Min(radius*2,Math.Min(rectangle.Width,rectangle.Height));
        var path=new GraphicsPath();
        path.AddArc(rectangle.X,rectangle.Y,diameter,diameter,180,90);
        path.AddArc(rectangle.Right-diameter,rectangle.Y,diameter,diameter,270,90);
        path.AddArc(rectangle.Right-diameter,rectangle.Bottom-diameter,diameter,diameter,0,90);
        path.AddArc(rectangle.X,rectangle.Bottom-diameter,diameter,diameter,90,90);
        path.CloseFigure();return path;
    }
    internal static Label Label(Control parent,string text,int x,int y,int width,int height,float size=10,bool bold=false) {
        var label=new Label { Text=text,Bounds=new Rectangle(x,y,width,height),Font=Font(size,bold),ForeColor=Ink,BackColor=parent is CardPanel?Surface:parent.BackColor,AutoEllipsis=true };
        parent.Controls.Add(label);return label;
    }
    internal static LinkLabel Link(Control parent,string text,int x,int y,int width,Action action) {
        var link=new LinkLabel { Text=text,Bounds=new Rectangle(x,y,width,30),Font=Font(9.5f),LinkColor=Blue,ActiveLinkColor=Blue,VisitedLinkColor=Blue,LinkBehavior=LinkBehavior.HoverUnderline,TextAlign=ContentAlignment.MiddleLeft,BackColor=parent is CardPanel?Surface:parent.BackColor };
        link.LinkClicked+=delegate { action(); };parent.Controls.Add(link);return link;
    }
}

internal sealed class CardPanel : Panel {
    internal CardPanel() { SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent; }
    protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Parent==null?Palette.Background:Parent.BackColor); }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using(var path=Palette.Rounded(new RectangleF(0.5f,0.5f,Width-1,Height-1),16*e.Graphics.DpiX/96))
        using(var brush=new SolidBrush(Palette.Surface))
        using(var pen=new Pen(Palette.Line)) { e.Graphics.FillPath(brush,path);e.Graphics.DrawPath(pen,path); }
        base.OnPaint(e);
    }
}

internal sealed class SoftButton : Button {
    private bool hover;
    private bool primary;
    internal bool Primary { get { return primary; } set { primary=value;Invalidate(); } }
    internal SoftButton(string text,int x,int y,int width,int height,bool accent=false) {
        Text=text;AccessibleName=text;AccessibleRole=AccessibleRole.PushButton;Bounds=new Rectangle(x,y,width,height);primary=accent;
        Font=Palette.Font(9.5f);Cursor=Cursors.Hand;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);
    }
    protected override void OnMouseEnter(EventArgs e) { hover=true;Invalidate();base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover=false;Invalidate();base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.Clear(Parent is CardPanel?Palette.Surface:Palette.Background);
        Color fill=!Enabled?(Palette.Dark?Color.FromArgb(35,47,64):Color.FromArgb(235,240,248)):primary?(hover?Color.FromArgb(34,88,194):Palette.Blue):(hover?(Palette.Dark?Color.FromArgb(38,54,76):Color.FromArgb(235,242,253)):Palette.Surface);
        Color text=!Enabled?Palette.Muted:primary?Color.White:Palette.Ink;
        using(var path=Palette.Rounded(new RectangleF(0.5f,0.5f,Width-1,Height-1),10*e.Graphics.DpiX/96))
        using(var brush=new SolidBrush(fill))
        using(var pen=new Pen(primary&&Enabled?fill:Palette.Line)) { e.Graphics.FillPath(brush,path);e.Graphics.DrawPath(pen,path); }
        TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-5,-5),text,fill);
    }
}

internal sealed class StatusDot : Control {
    private Color color=Palette.Muted;
    internal Color DotColor { get { return color; } set { color=value;Invalidate(); } }
    internal StatusDot() { SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);BackColor=Color.White; }
    protected override void OnPaint(PaintEventArgs e) { e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.Clear(BackColor);using(var brush=new SolidBrush(color))e.Graphics.FillEllipse(brush,1,1,Width-2,Height-2); }
}

internal class AppForm : Form {
    private sealed class DesignControl {
        internal Control Control;
        internal Rectangle Bounds;
        internal string Family;
        internal float Points;
        internal FontStyle Style;
    }
    private readonly System.Collections.Generic.List<DesignControl> design=new System.Collections.Generic.List<DesignControl>();
    private Size designSize;
    private bool ready,scaling;
    private Icon largeWindowIcon,smallWindowIcon;
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr first,IntPtr second);
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    internal virtual void RefreshTheme() { Palette.Apply(this);if(IsHandleCreated) { int dark=Palette.Dark?1:0;try { DwmSetWindowAttribute(Handle,20,ref dark,4); } catch {} } }
    protected override bool ShowWithoutActivation { get { return Program.IsPreview; } }
    protected void FinishLayout() {
        AutoScaleMode=AutoScaleMode.None;designSize=ClientSize;SaveControls(this);ready=true;ResumeLayout(true);
        RefreshTheme();if(IsHandleCreated)ApplyDpi(DeviceDpi);
    }
    private void SaveControls(Control parent) {
        foreach(Control control in parent.Controls) {
            design.Add(new DesignControl { Control=control,Bounds=control.Bounds,Family=control.Font.FontFamily.Name,Points=control.Font.SizeInPoints,Style=control.Font.Style });
            SaveControls(control);
        }
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e);if(ready)ApplyDpi(DeviceDpi);RefreshTheme(); }
    protected override void OnDpiChanged(DpiChangedEventArgs e) {
        e.Cancel=true;base.OnDpiChanged(e);if(ready) { ApplyDpi(e.DeviceDpiNew);Location=e.SuggestedRectangle.Location; }
    }
    private void ApplyDpi(int dpi) {
        if(scaling||!ready)return;scaling=true;SuspendLayout();
        try {
            float factor=dpi/96f;
            int desiredHeight=(int)Math.Round(designSize.Height*factor);
            int availableHeight=Screen.FromControl(this).WorkingArea.Height-(Height-ClientSize.Height)-(int)(24*factor);
            int height=Math.Min(desiredHeight,Math.Max((int)(300*factor),availableHeight));
            AutoScrollPosition=Point.Empty;AutoScroll=height<desiredHeight;AutoScrollMinSize=AutoScroll?new Size(0,desiredHeight):Size.Empty;
            ClientSize=new Size((int)Math.Round(designSize.Width*factor),height);
            foreach(var item in design) {
                var bounds=item.Bounds;
                item.Control.SetBounds((int)Math.Round(bounds.X*factor),(int)Math.Round(bounds.Y*factor),(int)Math.Round(bounds.Width*factor),(int)Math.Round(bounds.Height*factor));
                item.Control.Font=new Font(item.Family,item.Points*dpi/72f,item.Style,GraphicsUnit.Pixel);
            }
            var previousLarge=largeWindowIcon;var previousSmall=smallWindowIcon;
            largeWindowIcon=Program.LoadIconSize(Program.IconPixels(dpi,false));smallWindowIcon=Program.LoadIconSize(Program.IconPixels(dpi,true));
            Icon=largeWindowIcon;
            SendMessage(Handle,0x0080,(IntPtr)1,largeWindowIcon.Handle);SendMessage(Handle,0x0080,IntPtr.Zero,smallWindowIcon.Handle);
            if(previousLarge!=null)previousLarge.Dispose();if(previousSmall!=null)previousSmall.Dispose();
        } finally { ResumeLayout(true);scaling=false; }
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing);if(disposing) { if(largeWindowIcon!=null)largeWindowIcon.Dispose();if(smallWindowIcon!=null)smallWindowIcon.Dispose(); } }
}

internal sealed class Dashboard : AppForm {
    internal Action EnableRequested,RestoreRequested,FollowRequested,HideRequested,ExitRequested,LogsRequested,AboutRequested,AppearanceRequested;
    internal Action<int> FpsRequested;
    internal Action<string> ThemeRequested;
    internal Action<bool> DragCompatibilityRequested;
    private readonly Label title,description,metadata;
    private readonly StatusDot dot;
    private readonly SoftButton enable,restore,fps30,fps60,follow,appearance,themeSystem,themeLight,themeDark;
    private readonly CheckBox dragCompatibility;
    private readonly PictureBox logo;
    private bool closeChoiceOpen,updatingPreferences;
    internal bool AllowClose;
    internal Dashboard() {
        SuspendLayout();AutoScaleMode=AutoScaleMode.None;Font=Palette.Font(10);BackColor=Palette.Background;ForeColor=Palette.Ink;
        ClientSize=new Size(680,684);Text="大肥鱼 · Codex 宠物助手";Icon=Program.AppIcon;
        FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
        logo=new PictureBox {Image=Program.LoadLogo(),Bounds=new Rectangle(28,28,48,48),SizeMode=PictureBoxSizeMode.Zoom};Controls.Add(logo);
        Palette.Label(this,"大肥鱼",92,23,420,36,21,true);
        var subtitle=Palette.Label(this,"你的 Codex 桌面伙伴",94,65,420,25,9.5f);subtitle.ForeColor=Palette.Muted;
        var badge=Palette.Label(this,"v"+Program.Version,550,38,100,25,9);badge.TextAlign=ContentAlignment.MiddleRight;badge.ForeColor=Palette.Muted;
        var status=new CardPanel {Bounds=new Rectangle(28,112,624,184)};Controls.Add(status);
        dot=new StatusDot {Bounds=new Rectangle(20,26,10,10)};status.Controls.Add(dot);
        title=Palette.Label(status,"准备连接",42,18,560,30,14.5f,true);
        description=Palette.Label(status,"正在准备大肥鱼的运行环境。",20,58,584,40,9.5f);description.ForeColor=Palette.Muted;
        metadata=Palette.Label(status,"本机连接 · 自动跟随当前宠物",20,102,584,22,8.5f);metadata.ForeColor=Palette.Muted;
        enable=new SoftButton("启用大肥鱼",20,136,286,32,true);enable.Name="EnablePet";enable.Click+=delegate {if(EnableRequested!=null)EnableRequested();};status.Controls.Add(enable);
        restore=new SoftButton("恢复原宠物",318,136,286,32);restore.Name="RestorePet";restore.Click+=delegate {if(RestoreRequested!=null)RestoreRequested();};status.Controls.Add(restore);
        var looks=new CardPanel {Bounds=new Rectangle(28,312,300,228)};Controls.Add(looks);
        Palette.Label(looks,"外观与表情",20,18,260,28,11,true);
        var themeLabel=Palette.Label(looks,"界面主题",20,56,260,23,9);themeLabel.ForeColor=Palette.Muted;
        themeSystem=new SoftButton("跟随系统",20,83,84,32);themeSystem.Name="ThemeSystem";themeSystem.Click+=delegate {if(ThemeRequested!=null)ThemeRequested("system");};looks.Controls.Add(themeSystem);
        themeLight=new SoftButton("浅色",110,83,82,32);themeLight.Name="ThemeLight";themeLight.Click+=delegate {if(ThemeRequested!=null)ThemeRequested("light");};looks.Controls.Add(themeLight);
        themeDark=new SoftButton("深色",198,83,82,32);themeDark.Name="ThemeDark";themeDark.Click+=delegate {if(ThemeRequested!=null)ThemeRequested("dark");};looks.Controls.Add(themeDark);
        var dressLabel=Palette.Label(looks,"搭配表情、装扮和预览背景",20,139,260,24,9);dressLabel.ForeColor=Palette.Muted;
        appearance=new SoftButton("表情 / 装扮…",20,172,260,36,true);appearance.Name="OpenAppearance";appearance.Click+=delegate {if(AppearanceRequested!=null)AppearanceRequested();};looks.Controls.Add(appearance);
        var settings=new CardPanel {Bounds=new Rectangle(344,312,308,228)};Controls.Add(settings);
        Palette.Label(settings,"播放与窗口",20,18,268,28,11,true);Palette.Label(settings,"动画帧率",20,61,108,24,9.5f);
        fps30=new SoftButton("30 FPS",138,54,70,32);fps30.Name="Fps30";fps30.Click+=delegate {if(FpsRequested!=null)FpsRequested(30);};settings.Controls.Add(fps30);
        fps60=new SoftButton("60 FPS",218,54,70,32);fps60.Name="Fps60";fps60.Click+=delegate {if(FpsRequested!=null)FpsRequested(60);};settings.Controls.Add(fps60);
        dragCompatibility=new CheckBox {Text="拖窗兼容模式",Name="DragCompatibility",Bounds=new Rectangle(20,110,268,28),Font=Palette.Font(10)};dragCompatibility.CheckedChanged+=delegate {if(!updatingPreferences&&DragCompatibilityRequested!=null)DragCompatibilityRequested(dragCompatibility.Checked);};settings.Controls.Add(dragCompatibility);
        var dragHint=Palette.Label(settings,"拖动或缩放 Codex 时暂时收起宠物，松手后自动恢复。",20,148,268,50,9);dragHint.ForeColor=Palette.Muted;
        var binding=new CardPanel {Bounds=new Rectangle(28,556,624,48)};Controls.Add(binding);
        Palette.Label(binding,"当前宠物",20,14,180,24,9.5f);
        follow=new SoftButton("恢复自动跟随",376,9,228,30);follow.Name="FollowPet";follow.Click+=delegate {if(FollowRequested!=null)FollowRequested();};binding.Controls.Add(follow);
        Palette.Link(this,"项目主页",28,633,95,delegate {Program.OpenUrl(Program.Repository);});
        Palette.Link(this,"查看日志",138,633,95,delegate {if(LogsRequested!=null)LogsRequested();});
        Palette.Link(this,"关于",248,633,65,delegate {if(AboutRequested!=null)AboutRequested();});
        Palette.Link(this,"退出助手",328,633,100,delegate {if(ExitRequested!=null)ExitRequested();});
        var hide=new SoftButton("收起到托盘",486,628,166,36);hide.Name="HideToTray";hide.Click+=delegate {if(HideRequested!=null)HideRequested();};Controls.Add(hide);
        FormClosing+=delegate(object sender,FormClosingEventArgs e) {
            if(AllowClose)return;
            e.Cancel=true;if(closeChoiceOpen)return;
            if(e.CloseReason==CloseReason.WindowsShutDown) { if(ExitRequested!=null)ExitRequested();return; }
            closeChoiceOpen=true;
            try {
                using(var choice=new CloseChoice()) {
                    var result=choice.ShowDialog(this);
                    if(result==DialogResult.Yes) { if(HideRequested!=null)HideRequested(); }
                    else if(result==DialogResult.No) { if(ExitRequested!=null)ExitRequested(); }
                }
            } finally { closeChoiceOpen=false; }
        };
        FinishLayout();
    }

    internal void UpdateOptions(string theme,bool compatibility,bool busy) {updatingPreferences=true;try {themeSystem.Primary=theme=="system";themeLight.Primary=theme=="light";themeDark.Primary=theme=="dark";themeSystem.Enabled=themeLight.Enabled=themeDark.Enabled=!busy;dragCompatibility.Checked=compatibility;dragCompatibility.Enabled=!busy;}finally{updatingPreferences=false;}}
    internal void UpdateState(string heading,string detail,string meta,string state,bool busy,int fps,bool automatic) {
        title.Text=heading;description.Text=detail;metadata.Text=meta;
        dot.DotColor=state=="active"?Palette.Green:state=="error"?Palette.Red:state=="paused"?Palette.Muted:Palette.Amber;
        enable.Text=state=="active"?"已启用":busy?"正在处理…":state=="error"?"重试连接":"启用大肥鱼";
        enable.Enabled=!busy&&state!="active";
        restore.Enabled=!busy&&state!="paused";
        fps30.Primary=fps==30;fps60.Primary=fps==60;fps30.Enabled=!busy;fps60.Enabled=!busy;
        appearance.Enabled=!busy;
        follow.Text=automatic?"已自动跟随":"恢复自动跟随";follow.Enabled=!busy&&!automatic;
    }
    protected override void Dispose(bool disposing) { if(disposing&&logo.Image!=null)logo.Image.Dispose();base.Dispose(disposing); }
}

internal sealed class CloseChoice : AppForm {
    internal CloseChoice() {
        SuspendLayout();AutoScaleMode=AutoScaleMode.None;Font=Palette.Font(10);
        BackColor=Palette.Background;ClientSize=new Size(520,260);Text="关闭控制面板";Icon=Program.AppIcon;
        FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
        Palette.Label(this,"选择关闭方式",24,23,472,35,17,true);
        var text=Palette.Label(this,"收起到托盘：大肥鱼继续在后台陪伴。\n退出助手：恢复原宠物，Codex 会继续运行。",24,80,472,65,10);text.ForeColor=Palette.Muted;
        var hide=new SoftButton("收起到托盘",24,194,180,40,true);hide.Name="ConfirmHide";hide.DialogResult=DialogResult.Yes;Controls.Add(hide);
        var exit=new SoftButton("退出助手",216,194,172,40);exit.Name="ConfirmExit";exit.DialogResult=DialogResult.No;Controls.Add(exit);
        var cancel=new SoftButton("取消",400,194,96,40);cancel.Name="CancelClose";cancel.DialogResult=DialogResult.Cancel;Controls.Add(cancel);
        AcceptButton=hide;CancelButton=cancel;FinishLayout();
    }
}

internal sealed class CoreConsent : AppForm {
    internal CoreConsent() {
        SuspendLayout();AutoScaleMode=AutoScaleMode.None;Font=Palette.Font(10);
        BackColor=Palette.Background;ClientSize=new Size(540,418);Text="首次准备 · 大肥鱼";Icon=Program.AppIcon;
        FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
        Palette.Label(this,"准备角色动画组件",24,22,492,36,17,true);
        var intro=Palette.Label(this,"需要下载 Live2D Cubism Core，约 203 KB。\n准备完成后，以后使用无需重复下载。",24,71,492,54,10);intro.ForeColor=Palette.Muted;
        var card=new CardPanel { Bounds=new Rectangle(24,142,492,90) };Controls.Add(card);
        Palette.Label(card,"来源",18,15,70,25,10,true);Palette.Label(card,"Live2D 官方",100,15,370,25,10);
        Palette.Label(card,"用途",18,51,70,25,10,true);Palette.Label(card,"在本机绘制角色动画",100,51,370,25,10);
        Palette.Link(this,"阅读 Live2D 许可协议  ↗",24,245,492,delegate { Program.OpenUrl(Program.CoreLicense); });
        var agree=new CheckBox { Text="我已阅读并同意 Live2D 许可协议",Bounds=new Rectangle(26,287,490,37),Font=Palette.Font(10),BackColor=Palette.Background };
        Controls.Add(agree);
        var cancel=new SoftButton("暂不启用",24,352,234,40);cancel.DialogResult=DialogResult.Cancel;Controls.Add(cancel);
        var accept=new SoftButton("同意并准备",270,352,246,40,true);accept.DialogResult=DialogResult.OK;accept.Enabled=false;Controls.Add(accept);
        agree.CheckedChanged+=delegate { accept.Enabled=agree.Checked; };AcceptButton=accept;CancelButton=cancel;
        FinishLayout();
    }
}

internal sealed class AboutDialog : AppForm {
    internal AboutDialog() {
        SuspendLayout();AutoScaleMode=AutoScaleMode.None;Font=Palette.Font(10);
        BackColor=Palette.Background;ClientSize=new Size(540,430);Text="关于大肥鱼";Icon=Program.AppIcon;
        FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
        Palette.Label(this,"大肥鱼 · Codex 宠物助手",24,22,492,37,17,true);
        var version=Palette.Label(this,"v"+Program.Version+"  ·  作者 Chris-Geeky",24,68,492,26,10);version.ForeColor=Palette.Muted;
        var card=new CardPanel { Bounds=new Rectangle(24,112,492,115) };Controls.Add(card);
        Palette.Label(card,"数字签名",18,16,100,27,10,true);Palette.Label(card,Program.SignatureDescription(),130,16,342,30,10);
        var note=Palette.Label(card,"当前为预览版，签名状态如上。\n发布页附有 SHA-256 文件，可核对下载完整性。",18,57,454,47,9);note.ForeColor=Palette.Muted;
        var boundary=Palette.Label(this,"通过本机连接接入 Codex，不改写其安装文件。\n退出助手会恢复原宠物；关闭连接端口需完整退出 Codex。",24,246,492,52,9.5f);boundary.ForeColor=Palette.Muted;
        Palette.Link(this,"项目主页与源码  ↗",24,307,250,delegate { Program.OpenUrl(Program.Repository); });
        Palette.Link(this,"许可与素材署名  ↗",276,307,240,delegate { Program.OpenLocalFile(System.IO.Path.Combine(Program.Root,"NOTICE.md")); });
        var close=new SoftButton("知道了",328,365,188,40,true);close.DialogResult=DialogResult.OK;Controls.Add(close);AcceptButton=close;CancelButton=close;
        FinishLayout();
    }
}
