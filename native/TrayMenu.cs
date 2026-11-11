using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Keep ordinary ToolStrip commands and keyboard navigation; paint with the
// same palette as the panel rather than the legacy Windows menu gradient.
internal sealed class PetTrayMenu : ContextMenuStrip {
    internal Action PanelRequested,EnableRequested,RestoreRequested,FollowRequested,AppearanceRequested,LogsRequested,ExitRequested;
    internal Action<int> FpsRequested;
    internal Action<string> CloseBehaviorRequested;
    private readonly ToolStripMenuItem enable,restore,follow,appearance,fps30,fps60;
    private readonly ToolStripMenuItem closeAsk,closeTray,closeExit;
    private readonly TrayHeading header;
    private readonly float scale;
    internal PetTrayMenu() {
        scale=Program.SystemDpi()/96f;Font=Palette.Font(9.5f);
        ShowImageMargin=false;ShowCheckMargin=false;Padding=new Padding(Px(8));
        Renderer=new PetMenuRenderer(scale);DropShadowEnabled=true;
        header=new TrayHeading(scale);
        Items.Add(new ToolStripControlHost(header) {AutoSize=false,Size=new Size(Px(244),Px(58)),Margin=Padding.Empty,Padding=Padding.Empty});
        Items.Add(new ToolStripSeparator());
        Add("打开控制面板","panel",delegate {if(PanelRequested!=null)PanelRequested();});
        enable=Add("启用大肥鱼","play",delegate {if(EnableRequested!=null)EnableRequested();});
        appearance=Add("表情与装扮…","looks",delegate {if(AppearanceRequested!=null)AppearanceRequested();});
        Items.Add(new ToolStripSeparator());
        follow=Add("自动跟随当前宠物","follow",delegate {if(FollowRequested!=null)FollowRequested();});
        var rates=Add("动画帧率","fps",null);
        fps30=Sub(rates,"30 FPS",delegate {if(FpsRequested!=null)FpsRequested(30);});
        fps60=Sub(rates,"60 FPS",delegate {if(FpsRequested!=null)FpsRequested(60);});
        restore=Add("恢复原宠物","restore",delegate {if(RestoreRequested!=null)RestoreRequested();});
        Items.Add(new ToolStripSeparator());
        Add("查看日志","logs",delegate {if(LogsRequested!=null)LogsRequested();});
        var close=Add("关闭面板时","close",null);
        closeAsk=Sub(close,"每次询问",delegate {if(CloseBehaviorRequested!=null)CloseBehaviorRequested("ask");});
        closeTray=Sub(close,"收起到托盘",delegate {if(CloseBehaviorRequested!=null)CloseBehaviorRequested("tray");});
        closeExit=Sub(close,"退出助手",delegate {if(CloseBehaviorRequested!=null)CloseBehaviorRequested("exit");});
        UpdateCloseBehavior("ask");
        var exit=Add("退出助手","exit",delegate {if(ExitRequested!=null)ExitRequested();});
        exit.ToolTipText="退出助手并恢复原宠物；Codex 继续运行。";
        Opening+=delegate {RefreshTheme();};RefreshTheme();
    }
    private int Px(float value) {return (int)Math.Round(value*scale);}
    private ToolStripMenuItem Add(string text,string glyph,Action action) {
        var item=new ToolStripMenuItem(text) {Name="Tray"+glyph,Tag=glyph,AutoSize=false,Size=new Size(Px(244),Px(36)),Padding=Padding.Empty};
        if(action!=null)item.Click+=delegate {action();};Items.Add(item);return item;
    }
    private ToolStripMenuItem Sub(ToolStripMenuItem parent,string text,Action action) {
        var item=new ToolStripMenuItem(text) {Tag="check",AutoSize=false,Size=new Size(Px(150),Px(36)),Padding=Padding.Empty};
        item.Click+=delegate {action();};parent.DropDownItems.Add(item);
        var dropdown=(ToolStripDropDownMenu)parent.DropDown;dropdown.ShowImageMargin=false;dropdown.ShowCheckMargin=false;
        dropdown.Padding=new Padding(Px(6));dropdown.Renderer=Renderer;return item;
    }
    internal void UpdateState(string heading,string state,bool busy,int fps,bool automatic) {
        header.UpdateState(heading,state);
        enable.Text=state=="active"?"大肥鱼已启用":state=="watching"?"打开 Codex":state=="handoff-wait"?"等待 Codex 退出":state=="relaunch-needed"?"安排接入":state=="error"?"重试连接":"启用大肥鱼";
        enable.Enabled=!busy&&state!="active"&&state!="handoff-wait";
        restore.Text=state=="handoff-wait"||state=="relaunch-needed"||state=="watching"?"停止自动接入":"恢复原宠物";
        restore.Enabled=!busy&&state!="paused";
        follow.Checked=automatic;follow.Enabled=!busy&&!automatic;
        fps30.Checked=fps==30;fps60.Checked=fps==60;fps30.Enabled=fps60.Enabled=!busy;
        appearance.Enabled=!busy;
        RefreshTheme();
    }
    internal void RefreshTheme() {ApplyPalette(this);header.Invalidate();}
    internal void UpdateCloseBehavior(string value) {
        closeAsk.Checked=value=="ask";closeTray.Checked=value=="tray";closeExit.Checked=value=="exit";
    }
    internal static void ApplyPalette(ToolStrip strip) {
        strip.BackColor=Palette.Surface;strip.ForeColor=Palette.Ink;
        foreach(ToolStripItem item in strip.Items) {
            item.ForeColor=Palette.Ink;
            var submenu=item as ToolStripMenuItem;if(submenu!=null&&submenu.HasDropDownItems)ApplyPalette(submenu.DropDown);
        }
        strip.Invalidate();
    }
    protected override void OnSizeChanged(EventArgs e) {
        base.OnSizeChanged(e);if(scale<=0||Width<2||Height<2)return;
        using(var path=Palette.Rounded(new RectangleF(0,0,Width,Height),Px(10))) {
            var previous=Region;Region=new Region(path);if(previous!=null)previous.Dispose();
        }
    }
}

internal sealed class DashboardMenu : ContextMenuStrip {
    private readonly ToolStripMenuItem closeAsk,closeTray,closeExit;
    private readonly float scale;
    internal DashboardMenu(int dpi,Action version,Action project,Action logs,Action<string> close) {
        scale=dpi/96f;Font=Palette.Font(9.5f);ShowImageMargin=false;ShowCheckMargin=false;Padding=new Padding(Px(7));
        MinimumSize=new Size(Px(184),0);
        Renderer=new PetMenuRenderer(scale);
        Items.Add(Item("版本信息","OptionsVersion","info",version));
        Items.Add(Item("项目主页","OptionsProject","project",project));
        Items.Add(Item("查看日志","OptionsLogs","logs",logs));
        Items.Add(new ToolStripSeparator());
        var behavior=Item("关闭面板时","OptionsClose","close",null);Items.Add(behavior);
        closeAsk=Item("每次询问","OptionsAsk","check",delegate{close("ask");},151);
        closeTray=Item("收起到托盘","OptionsTray","check",delegate{close("tray");},151);
        closeExit=Item("退出助手","OptionsExit","check",delegate{close("exit");},151);
        behavior.DropDownItems.AddRange(new ToolStripItem[]{closeAsk,closeTray,closeExit});
        var dropdown=(ToolStripDropDownMenu)behavior.DropDown;
        dropdown.ShowImageMargin=false;dropdown.ShowCheckMargin=false;dropdown.Padding=new Padding(Px(6));dropdown.Renderer=Renderer;
        dropdown.MinimumSize=new Size(Px(163),0);
        Opening+=delegate{PetTrayMenu.ApplyPalette(this);};PetTrayMenu.ApplyPalette(this);
    }
    private int Px(float logical) {return (int)Math.Round(logical*scale);}
    private ToolStripMenuItem Item(string label,string name,string glyph,Action action,int width=170) {
        var item=new ToolStripMenuItem(label){Name=name,Tag=glyph,AutoSize=false,Size=new Size(Px(width),Px(36)),Padding=Padding.Empty};
        if(action!=null)item.Click+=delegate{action();};
        return item;
    }
    internal void UpdateCloseBehavior(string value) {
        closeAsk.Checked=value=="ask";closeTray.Checked=value=="tray";closeExit.Checked=value=="exit";
    }
    protected override void OnSizeChanged(EventArgs e) {
        base.OnSizeChanged(e);if(scale<=0||Width<2||Height<2)return;
        using(var path=Palette.Rounded(new RectangleF(0,0,Width,Height),Px(10))){
            var previous=Region;Region=new Region(path);if(previous!=null)previous.Dispose();
        }
    }
}

internal sealed class TrayHeading : Control {
    private readonly float scale;
    private readonly Image logo=Program.LoadLogo();
    private readonly Font titleFont=Palette.Font(10,true),statusFont=Palette.Font(8.5f);
    private string heading="准备连接",state="starting";
    internal TrayHeading(float dpiScale) {scale=dpiScale;SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);AccessibleName="大肥鱼连接状态";AccessibleRole=AccessibleRole.StaticText;TabStop=false;}
    internal void UpdateState(string title,string value) {heading=title;state=value;AccessibleDescription=title;Invalidate();}
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(Palette.Surface);e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;
        e.Graphics.DrawImage(logo,new Rectangle((int)(9*scale),(int)(8*scale),(int)(34*scale),(int)(34*scale)));
        TextRenderer.DrawText(e.Graphics,"大肥鱼",titleFont,new Rectangle((int)(53*scale),(int)(6*scale),Width-(int)(60*scale),(int)(24*scale)),Palette.Ink,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);
        var color=state=="active"?Palette.Green:state=="error"?Palette.Red:state=="paused"?Palette.Muted:Palette.Amber;
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using(var brush=new SolidBrush(color))e.Graphics.FillEllipse(brush,54*scale,36*scale,5*scale,5*scale);
        TextRenderer.DrawText(e.Graphics,heading,statusFont,new Rectangle((int)(65*scale),(int)(27*scale),Width-(int)(71*scale),(int)(22*scale)),Palette.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
    }
    protected override void Dispose(bool disposing) {if(disposing){logo.Dispose();titleFont.Dispose();statusFont.Dispose();}base.Dispose(disposing);}
}

internal sealed class PetMenuRenderer : ToolStripRenderer {
    private readonly float scale;
    internal PetMenuRenderer(float dpiScale) {scale=dpiScale;}
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) {e.Graphics.Clear(Palette.Surface);}
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        using(var path=Palette.Rounded(new RectangleF(.5f,.5f,e.ToolStrip.Width-1,e.ToolStrip.Height-1),10*scale))
        using(var pen=new Pen(Palette.Line))e.Graphics.DrawPath(pen,path);
    }
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e) {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        if(e.Item.Selected&&e.Item.Enabled)using(var path=Palette.Rounded(new RectangleF(0,0,e.Item.Width,e.Item.Height),6*scale))
            using(var brush=new SolidBrush(Palette.Dark?Color.FromArgb(43,58,80):Color.FromArgb(234,241,253)))e.Graphics.FillPath(brush,path);
        var item=e.Item as ToolStripMenuItem;if(item==null)return;
        string glyph=Convert.ToString(item.Tag);if(glyph=="check"&&!item.Checked)return;
        var color=!item.Enabled?Palette.Muted:glyph=="exit"?Palette.Dark?Color.FromArgb(242,158,161):Palette.Red:Palette.Accent;
        var saved=e.Graphics.Save();e.Graphics.TranslateTransform(11*scale,(e.Item.Height-18*scale)/2);e.Graphics.ScaleTransform(scale,scale);
        using(var pen=new Pen(color,1.55f)) {
            pen.StartCap=pen.EndCap=LineCap.Round;pen.LineJoin=LineJoin.Round;
            if(item.Checked) {e.Graphics.DrawLines(pen,new[]{new PointF(3,9),new PointF(7,13),new PointF(15,5)});}
            else if(glyph=="panel") {e.Graphics.DrawRectangle(pen,2,3,14,12);e.Graphics.DrawLine(pen,2,7,16,7);e.Graphics.DrawLine(pen,7,7,7,15);}
            else if(glyph=="play")e.Graphics.DrawPolygon(pen,new[]{new PointF(5,2),new PointF(15,9),new PointF(5,16)});
            else if(glyph=="looks") {e.Graphics.DrawEllipse(pen,2,3,14,13);e.Graphics.DrawLine(pen,6,1,6,5);e.Graphics.DrawLine(pen,4,3,8,3);e.Graphics.DrawArc(pen,6,7,7,5,0,180);}
            else if(glyph=="follow") {e.Graphics.DrawArc(pen,3,3,12,12,35,280);e.Graphics.DrawLines(pen,new[]{new PointF(11,1),new PointF(15,3),new PointF(13,7)});}
            else if(glyph=="fps") {e.Graphics.DrawLine(pen,3,13,3,7);e.Graphics.DrawLine(pen,9,16,9,2);e.Graphics.DrawLine(pen,15,12,15,5);}
            else if(glyph=="restore") {e.Graphics.DrawArc(pen,4,4,12,11,210,290);e.Graphics.DrawLines(pen,new[]{new PointF(2,2),new PointF(2,7),new PointF(7,7)});}
            else if(glyph=="logs") {e.Graphics.DrawRectangle(pen,4,2,11,14);e.Graphics.DrawLine(pen,7,6,12,6);e.Graphics.DrawLine(pen,7,10,12,10);}
            else if(glyph=="info") {e.Graphics.DrawEllipse(pen,2,2,14,14);e.Graphics.DrawLine(pen,9,8,9,12);e.Graphics.DrawLine(pen,9,5.5f,9,5.6f);}
            else if(glyph=="project") {e.Graphics.DrawLines(pen,new[]{new PointF(8,4),new PointF(3,4),new PointF(3,15),new PointF(14,15),new PointF(14,10)});e.Graphics.DrawLine(pen,8,10,15,3);e.Graphics.DrawLines(pen,new[]{new PointF(10,3),new PointF(15,3),new PointF(15,8)});}
            else if(glyph=="close") {e.Graphics.DrawRectangle(pen,2,3,14,12);e.Graphics.DrawLine(pen,7,6,12,11);e.Graphics.DrawLine(pen,12,6,7,11);}
            else if(glyph=="exit") {e.Graphics.DrawArc(pen,2,2,14,14,-55,290);e.Graphics.DrawLine(pen,9,0,9,8);}
        }
        e.Graphics.Restore(saved);
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {
        var rect=e.Item.ContentRectangle;rect.X=(int)(40*scale);rect.Width=e.Item.Width-rect.X-(int)(24*scale);
        TextRenderer.DrawText(e.Graphics,e.Text,e.TextFont,rect,e.Item.Enabled?Palette.Ink:Palette.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.EndEllipsis);
    }
    protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        float x=e.Item.Width-17*scale,y=e.Item.Height/2f;
        using(var pen=new Pen(e.Item.Enabled?Palette.Muted:Palette.Line,1.4f*scale)) {
            pen.StartCap=pen.EndCap=LineCap.Round;e.Graphics.DrawLines(pen,new[]{new PointF(x-2*scale,y-4*scale),new PointF(x+2*scale,y),new PointF(x-2*scale,y+4*scale)});
        }
    }
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) {}
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) {}
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e) {using(var pen=new Pen(Palette.Line))e.Graphics.DrawLine(pen,10*scale,e.Item.Height/2,e.Item.Width-10*scale,e.Item.Height/2);}
}
