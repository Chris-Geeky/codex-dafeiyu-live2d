using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

internal static class Palette {
    internal static readonly Color Background=Color.FromArgb(245,248,252);
    internal static readonly Color Ink=Color.FromArgb(22,37,61);
    internal static readonly Color Muted=Color.FromArgb(103,118,140);
    internal static readonly Color Line=Color.FromArgb(225,233,243);
    internal static readonly Color Blue=Color.FromArgb(44,105,224);
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
        var label=new Label { Text=text,Bounds=new Rectangle(x,y,width,height),Font=Font(size,bold),ForeColor=Ink,BackColor=parent is CardPanel?Color.White:parent.BackColor,AutoEllipsis=true };
        parent.Controls.Add(label);return label;
    }
    internal static LinkLabel Link(Control parent,string text,int x,int y,int width,Action action) {
        var link=new LinkLabel { Text=text,Bounds=new Rectangle(x,y,width,30),Font=Font(9.5f),LinkColor=Blue,ActiveLinkColor=Blue,VisitedLinkColor=Blue,LinkBehavior=LinkBehavior.HoverUnderline,TextAlign=ContentAlignment.MiddleLeft,BackColor=parent is CardPanel?Color.White:parent.BackColor };
        link.LinkClicked+=delegate { action(); };parent.Controls.Add(link);return link;
    }
}

internal sealed class CardPanel : Panel {
    internal CardPanel() { SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent; }
    protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Parent==null?Palette.Background:Parent.BackColor); }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using(var path=Palette.Rounded(new RectangleF(0.5f,0.5f,Width-1,Height-1),16*e.Graphics.DpiX/96))
        using(var brush=new SolidBrush(Color.White))
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
        Font=Palette.Font(10,true);Cursor=Cursors.Hand;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);
    }
    protected override void OnMouseEnter(EventArgs e) { hover=true;Invalidate();base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover=false;Invalidate();base.OnMouseLeave(e); }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.Clear(Parent is CardPanel?Color.White:Palette.Background);
        Color fill=!Enabled?Color.FromArgb(239,243,249):primary?(hover?Color.FromArgb(34,88,194):Palette.Blue):(hover?Color.FromArgb(243,247,253):Color.White);
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
    protected override bool ShowWithoutActivation { get { return Program.IsPreview; } }
    protected void FinishLayout() {
        AutoScaleMode=AutoScaleMode.None;designSize=ClientSize;SaveControls(this);ready=true;ResumeLayout(true);
        if(IsHandleCreated)ApplyDpi(DeviceDpi);
    }
    private void SaveControls(Control parent) {
        foreach(Control control in parent.Controls) {
            design.Add(new DesignControl { Control=control,Bounds=control.Bounds,Family=control.Font.FontFamily.Name,Points=control.Font.SizeInPoints,Style=control.Font.Style });
            SaveControls(control);
        }
    }
    protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e);if(ready)ApplyDpi(DeviceDpi); }
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
    private readonly Label title,description,metadata;
    private readonly StatusDot dot;
    private readonly SoftButton enable,restore,fps30,fps60,follow,appearance;
    private readonly PictureBox logo;
    private bool closeChoiceOpen;
    internal bool AllowClose;
    internal Dashboard() {
        SuspendLayout();AutoScaleMode=AutoScaleMode.None;
        Font=Palette.Font(10);BackColor=Palette.Background;ForeColor=Palette.Ink;
        ClientSize=new Size(600,758);Text="大肥鱼 · Codex 宠物助手";Icon=Program.AppIcon;
        FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
        logo=new PictureBox { Image=Program.LoadLogo(),Bounds=new Rectangle(28,24,64,64),SizeMode=PictureBoxSizeMode.Zoom };
        Controls.Add(logo);
        Palette.Label(this,"大肥鱼",108,22,360,46,24,true);
        var subtitle=Palette.Label(this,"Codex 宠物助手  /  v"+Program.Version,111,70,445,26,10);subtitle.ForeColor=Palette.Muted;

        var status=new CardPanel { Bounds=new Rectangle(28,120,544,204) };Controls.Add(status);
        dot=new StatusDot { Bounds=new Rectangle(24,29,12,12) };status.Controls.Add(dot);
        title=Palette.Label(status,"准备连接",48,19,470,34,17,true);
        description=Palette.Label(status,"正在准备大肥鱼的运行环境。",24,62,496,43,10);description.ForeColor=Palette.Muted;
        metadata=Palette.Label(status,"本机连接 · 自动跟随当前宠物",24,109,496,24,9);metadata.ForeColor=Palette.Muted;
        enable=new SoftButton("启用大肥鱼",24,146,242,38,true);enable.Name="EnablePet";enable.Click+=delegate { if(EnableRequested!=null)EnableRequested(); };status.Controls.Add(enable);
        restore=new SoftButton("恢复原宠物",278,146,242,38);restore.Name="RestorePet";restore.Click+=delegate { if(RestoreRequested!=null)RestoreRequested(); };status.Controls.Add(restore);

        var settings=new CardPanel { Bounds=new Rectangle(28,340,544,222) };Controls.Add(settings);
        Palette.Label(settings,"动画设置",24,16,470,28,11,true);
        Palette.Label(settings,"动画帧率",24,60,200,24,10);
        fps30=new SoftButton("30 FPS",270,49,116,36);fps30.Name="Fps30";fps30.Click+=delegate { if(FpsRequested!=null)FpsRequested(30); };settings.Controls.Add(fps30);
        fps60=new SoftButton("60 FPS",398,49,122,36,true);fps60.Name="Fps60";fps60.Click+=delegate { if(FpsRequested!=null)FpsRequested(60); };settings.Controls.Add(fps60);
        Palette.Label(settings,"宠物选择",24,113,200,24,10);
        follow=new SoftButton("跟随当前宠物",328,101,192,36);follow.Name="FollowPet";follow.Click+=delegate { if(FollowRequested!=null)FollowRequested(); };settings.Controls.Add(follow);
        Palette.Label(settings,"角色装扮",24,169,200,24,10);
        appearance=new SoftButton("表情 / 装扮…",328,157,192,36);appearance.Name="OpenAppearance";appearance.Click+=delegate { if(AppearanceRequested!=null)AppearanceRequested(); };settings.Controls.Add(appearance);

        var source=new CardPanel { Bounds=new Rectangle(28,578,544,110) };Controls.Add(source);
        Palette.Label(source,"来源与说明",24,16,290,26,11,true);
        var author=Palette.Label(source,"作者 Chris-Geeky · GitHub 公开源码",24,47,330,24,9);author.ForeColor=Palette.Muted;
        var unofficial=Palette.Label(source,"非官方 Codex 适配器",24,75,320,23,9);unofficial.ForeColor=Palette.Muted;
        Palette.Link(source,"查看项目主页  ↗",367,17,160,delegate { Program.OpenUrl(Program.Repository); });
        Palette.Link(source,"许可与素材署名  ↗",367,58,160,delegate { Program.OpenLocalFile(System.IO.Path.Combine(Program.Root,"NOTICE.md")); });

        Palette.Link(this,"查看日志",28,713,100,delegate { if(LogsRequested!=null)LogsRequested(); });
        Palette.Link(this,"关于",138,713,65,delegate { if(AboutRequested!=null)AboutRequested(); });
        Palette.Link(this,"退出助手",218,713,110,delegate { if(ExitRequested!=null)ExitRequested(); });
        var hide=new SoftButton("收起到托盘",372,708,200,38);hide.Name="HideToTray";hide.Click+=delegate { if(HideRequested!=null)HideRequested(); };Controls.Add(hide);
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
