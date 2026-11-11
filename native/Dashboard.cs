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
    internal static Color Chrome {get{return Dark?Color.FromArgb(23,32,47):Color.FromArgb(231,239,250);}}
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
    internal bool InCaption;
    internal bool Primary { get { return primary; } set { primary=value;Invalidate(); } }
    internal SoftButton(string text,int x,int y,int width,int height,bool accent=false) {
        Text=text;AccessibleName=text;AccessibleRole=AccessibleRole.PushButton;Bounds=new Rectangle(x,y,width,height);primary=accent;
        Font=Palette.Font(9.5f);Cursor=Cursors.Hand;FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);
    }
    protected override void OnMouseEnter(EventArgs e) { hover=true;Invalidate();base.OnMouseEnter(e); }
    protected override void OnMouseLeave(EventArgs e) { hover=false;Invalidate();base.OnMouseLeave(e); }
    protected override void OnTextChanged(EventArgs e) {AccessibleName=Text;base.OnTextChanged(e);}
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.Clear(InCaption?Palette.Chrome:Parent is CardPanel?Palette.Surface:Palette.Background);
        Color fill=!Enabled?(Palette.Dark?Color.FromArgb(35,47,64):Color.FromArgb(235,240,248)):primary?(hover?Color.FromArgb(34,88,194):Palette.Blue):(hover?(Palette.Dark?Color.FromArgb(38,54,76):Color.FromArgb(235,242,253)):Palette.Surface);
        Color text=!Enabled?Palette.Muted:primary?Color.White:Palette.Ink;
        using(var path=Palette.Rounded(new RectangleF(0.5f,0.5f,Width-1,Height-1),10*e.Graphics.DpiX/96))
        using(var brush=new SolidBrush(fill))
        using(var pen=new Pen(primary&&Enabled?fill:Palette.Line)) { e.Graphics.FillPath(brush,path);e.Graphics.DrawPath(pen,path); }
        TextRenderer.DrawText(e.Graphics,Text,Font,ClientRectangle,text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-5,-5),text,fill);
    }
}

internal sealed class CaptionButton : Button {
    private readonly bool closes;
    private bool hover;
    internal CaptionButton(bool close,int x,int height) {
        closes=close;Name=close?"CaptionClose":"CaptionMinimize";Text=close?"关闭":"最小化";AccessibleName=Text;AccessibleRole=AccessibleRole.PushButton;
        Bounds=new Rectangle(x,0,44,height);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);
    }
    protected override void OnMouseEnter(EventArgs e) {hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e) {hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e) {
        Color background=hover?(closes?Color.FromArgb(196,43,53):Palette.Dark?Color.FromArgb(43,56,75):Color.FromArgb(209,222,240)):Palette.Chrome;
        Color ink=hover&&closes?Color.White:Palette.Ink;
        var g=e.Graphics;g.Clear(background);g.SmoothingMode=SmoothingMode.AntiAlias;
        float dpi=g.DpiX/96f,x=Width/2f,y=Height/2f,half=5*dpi;
        using(var pen=new Pen(ink,1.1f*dpi)){
            if(closes){g.DrawLine(pen,x-half,y-half,x+half,y+half);g.DrawLine(pen,x+half,y-half,x-half,y+half);}
            else g.DrawLine(pen,x-half,y,x+half,y);
        }
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(ClientRectangle,-5,-5),ink,background);
    }
}

internal sealed class SettingsSwitch : CheckBox {
    private bool hover;
    internal SettingsSwitch(string text,string name,int x,int y,int width) {
        Text=text;Name=name;AccessibleName=text;AccessibleRole=AccessibleRole.CheckButton;
        Bounds=new Rectangle(x,y,width,36);Font=Palette.Font(9.5f);Cursor=Cursors.Hand;AutoSize=false;
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);
    }
    protected override void OnMouseEnter(EventArgs e) {hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e) {hover=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnCheckedChanged(EventArgs e) {Invalidate();base.OnCheckedChanged(e);}
    protected override void OnPaint(PaintEventArgs e) {
        float scale=e.Graphics.DpiX/96f;float width=44*scale,height=24*scale;
        var track=new RectangleF(Width-width, (Height-height)/2,width,height);
        Color background=Parent is CardPanel?Palette.Surface:Palette.Background;
        e.Graphics.Clear(background);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;
        Color fill=Checked?(Enabled?Palette.Blue:Palette.Dark?Color.FromArgb(53,76,111):Color.FromArgb(159,185,229)):
            Palette.Dark?Color.FromArgb(48,64,88):Color.FromArgb(209,220,235);
        if(hover&&Enabled)fill=Checked?Color.FromArgb(37,91,198):Palette.Dark?Color.FromArgb(61,79,105):Color.FromArgb(190,207,229);
        using(var path=Palette.Rounded(track,height/2))using(var brush=new SolidBrush(fill))e.Graphics.FillPath(brush,path);
        float knob=18*scale,padding=3*scale;
        using(var brush=new SolidBrush(Enabled?Color.White:Palette.Dark?Color.FromArgb(156,171,192):Color.FromArgb(241,245,251)))
            e.Graphics.FillEllipse(brush,Checked?track.Right-padding-knob:track.Left+padding,track.Top+padding,knob,knob);
        var textRect=new Rectangle(0,0,Math.Max(0,(int)(track.Left-12*scale)),Height);
        TextRenderer.DrawText(e.Graphics,Text,Font,textRect,Enabled?Palette.Ink:Palette.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-2,-2),Palette.Accent,background);
    }
}

internal sealed class SizeSlider : Control {
    internal const int Minimum=60,Maximum=130,DefaultPercent=100;
    internal event Action<int> ValueChanged,Committed;
    private readonly Timer settle=new Timer {Interval=220};
    private int value=DefaultPercent;
    private bool dragging;
    internal int Value {
        get{return value;}
        set{
            int next=Math.Max(Minimum,Math.Min(Maximum,value));
            if(next==this.value)return;
            this.value=next;Invalidate();
            if(IsHandleCreated)AccessibilityNotifyClients(AccessibleEvents.ValueChange,-1);
            if(ValueChanged!=null)ValueChanged(next);
        }
    }
    internal bool Editing {get{return dragging||settle.Enabled;}}
    internal SizeSlider(int x,int y,int width) {
        Name="PetSizeSlider";AccessibleName="宠物大小";AccessibleRole=AccessibleRole.Slider;
        Bounds=new Rectangle(x,y,width,30);TabStop=true;Cursor=Cursors.Hand;
        SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint|ControlStyles.Selectable,true);
        settle.Tick+=delegate{Commit();};
    }
    private void Commit() {settle.Stop();if(Committed!=null)Committed(Value);}
    internal void RestoreDefault() {Value=DefaultPercent;Commit();}
    private void FromMouse(int x) {
        float inset=9*DeviceDpi/96f;
        double position=Math.Max(0,Math.Min(1,(x-inset)/Math.Max(1,Width-2*inset)));
        Value=Minimum+(int)Math.Round(position*(Maximum-Minimum));
    }
    protected override void OnMouseDown(MouseEventArgs e) {
        base.OnMouseDown(e);
        if(e.Button!=MouseButtons.Left||!Enabled)return;
        Focus();settle.Stop();dragging=true;Capture=true;FromMouse(e.X);
    }
    protected override void OnMouseMove(MouseEventArgs e) {base.OnMouseMove(e);if(dragging)FromMouse(e.X);}
    protected override void OnMouseUp(MouseEventArgs e) {
        if(dragging&&e.Button==MouseButtons.Left){FromMouse(e.X);dragging=false;Capture=false;Commit();}
        base.OnMouseUp(e);
    }
    protected override void OnMouseCaptureChanged(EventArgs e) {
        base.OnMouseCaptureChanged(e);
        if(dragging&&!Capture){dragging=false;Commit();}
    }
    protected override bool IsInputKey(Keys keyData) {
        switch(keyData&Keys.KeyCode){
            case Keys.Left:case Keys.Right:case Keys.Up:case Keys.Down:case Keys.Home:case Keys.End:case Keys.PageUp:case Keys.PageDown:return true;
            default:return base.IsInputKey(keyData);
        }
    }
    protected override void OnKeyDown(KeyEventArgs e) {
        base.OnKeyDown(e);
        if(e.Modifiers!=Keys.None||dragging)return;
        int next=Value;
        switch(e.KeyCode){
            case Keys.Left:case Keys.Down:next--;break;
            case Keys.Right:case Keys.Up:next++;break;
            case Keys.PageDown:next-=10;break;
            case Keys.PageUp:next+=10;break;
            case Keys.Home:next=Minimum;break;
            case Keys.End:next=Maximum;break;
            default:return;
        }
        e.Handled=true;e.SuppressKeyPress=true;
        if(next==Value)return;
        settle.Stop();settle.Start();Value=next;
    }
    protected override void OnGotFocus(EventArgs e) {base.OnGotFocus(e);Invalidate();}
    protected override void OnLostFocus(EventArgs e) {base.OnLostFocus(e);Invalidate();}
    protected override void OnPaint(PaintEventArgs e) {
        var g=e.Graphics;float dpi=g.DpiX/96f,pad=9*dpi,y=Height/2f,radius=7*dpi,line=4*dpi;
        float width=Math.Max(1,Width-2*pad),x=pad+width*(Value-Minimum)/(Maximum-Minimum);
        g.Clear(Palette.Surface);g.SmoothingMode=SmoothingMode.AntiAlias;
        using(var track=Palette.Rounded(new RectangleF(pad,y-line/2,width,line),line/2))
        using(var brush=new SolidBrush(Palette.Dark?Color.FromArgb(53,72,99):Color.FromArgb(208,222,241)))g.FillPath(brush,track);
        if(x>pad)using(var fill=Palette.Rounded(new RectangleF(pad,y-line/2,x-pad,line),line/2))
        using(var brush=new SolidBrush(Enabled?Palette.Blue:Palette.Muted))g.FillPath(brush,fill);
        float mark=pad+width*(DefaultPercent-Minimum)/(Maximum-Minimum);
        using(var pen=new Pen(Palette.Muted,dpi))g.DrawLine(pen,mark,y-5*dpi,mark,y+5*dpi);
        using(var pen=new Pen(Enabled?Palette.Accent:Palette.Muted,1.5f*dpi))
        using(var brush=new SolidBrush(Palette.Dark?Color.FromArgb(226,237,255):Color.White)) {
            g.FillEllipse(brush,x-radius,y-radius,radius*2,radius*2);
            g.DrawEllipse(pen,x-radius,y-radius,radius*2,radius*2);
        }
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,Rectangle.Inflate(ClientRectangle,-2,-2),Palette.Accent,Palette.Surface);
    }
    protected override AccessibleObject CreateAccessibilityInstance() {return new SizeAccessible(this);}
    private sealed class SizeAccessible : ControlAccessibleObject {
        private readonly SizeSlider slider;
        internal SizeAccessible(SizeSlider owner):base(owner){slider=owner;}
        public override string Value {
            get{return slider.Value+"%";}
            set{
                int next;
                if(slider.Enabled&&Int32.TryParse((value??"").Trim().TrimEnd('%'),out next)&&next>=Minimum&&next<=Maximum){
                    slider.Value=next;slider.Commit();
                }
            }
        }
    }
    protected override void Dispose(bool disposing) {if(disposing)settle.Dispose();base.Dispose(disposing);}
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
    protected virtual bool UsesCustomCaption {get{return false;}}
    protected virtual Panel ScrollingContent {get{return null;}}
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
        var scrolling=ScrollingContent;
        if(scrolling!=null)scrolling.SuspendLayout();
        try {
            float factor=dpi/96f;
            int desiredHeight=(int)Math.Round(designSize.Height*factor);
            int availableHeight=Screen.FromControl(this).WorkingArea.Height-(Height-ClientSize.Height)-(int)(24*factor);
            int height=Math.Min(desiredHeight,Math.Max((int)(300*factor),availableHeight));
            if(scrolling==null){
                AutoScrollPosition=Point.Empty;AutoScroll=height<desiredHeight;AutoScrollMinSize=AutoScroll?new Size(0,desiredHeight):Size.Empty;
            }else{
                scrolling.AutoScrollPosition=Point.Empty;scrolling.AutoScroll=false;scrolling.AutoScrollMinSize=Size.Empty;
            }
            var targetSize=new Size((int)Math.Round(designSize.Width*factor),height);
            if(UsesCustomCaption)Size=targetSize;else ClientSize=targetSize;
            foreach(var item in design) {
                var bounds=item.Bounds;
                item.Control.SetBounds((int)Math.Round(bounds.X*factor),(int)Math.Round(bounds.Y*factor),(int)Math.Round(bounds.Width*factor),(int)Math.Round(bounds.Height*factor));
                item.Control.Font=UsesCustomCaption?new Font(item.Family,item.Points,item.Style,GraphicsUnit.Point):
                    new Font(item.Family,item.Points*dpi/72f,item.Style,GraphicsUnit.Pixel);
            }
            if(scrolling!=null){
                int fullHeight=scrolling.Height;
                scrolling.Height=Math.Max(0,height-scrolling.Top);
                scrolling.AutoScroll=scrolling.Height<fullHeight;
                if(scrolling.AutoScroll)scrolling.AutoScrollMinSize=new Size(0,fullHeight);
            }
            var previousLarge=largeWindowIcon;var previousSmall=smallWindowIcon;
            largeWindowIcon=Program.LoadIconSize(Program.IconPixels(dpi,false));smallWindowIcon=ShowIcon?Program.LoadIconSize(Program.IconPixels(dpi,true)):null;
            Icon=largeWindowIcon;
            SendMessage(Handle,0x0080,(IntPtr)1,largeWindowIcon.Handle);SendMessage(Handle,0x0080,IntPtr.Zero,smallWindowIcon==null?IntPtr.Zero:smallWindowIcon.Handle);
            if(previousLarge!=null)previousLarge.Dispose();if(previousSmall!=null)previousSmall.Dispose();
        } finally { if(scrolling!=null)scrolling.ResumeLayout(true);ResumeLayout(true);scaling=false; }
    }
    protected override void Dispose(bool disposing) { base.Dispose(disposing);if(disposing) { if(largeWindowIcon!=null)largeWindowIcon.Dispose();if(smallWindowIcon!=null)smallWindowIcon.Dispose(); } }
}

internal sealed class Dashboard : AppForm {
    private const int CaptionHeight=48;
    private readonly Panel content;
    private readonly CaptionButton minimize,closeWindow;
    private readonly Font captionTitle=Palette.Font(9.5f,true),captionVersion=Palette.Font(8.5f);
    internal Action EnableRequested,RestoreRequested,FollowRequested,HideRequested,ExitRequested,LogsRequested,AboutRequested,AppearanceRequested;
    internal Action<string,bool> CloseActionRequested;
    internal string CloseBehavior="ask";
    internal Action<int> FpsRequested,PetSizeRequested;
    internal Action<string> ThemeRequested,ClosePreferenceRequested;
    internal Action<string> AvoidanceTriggerRequested;
    internal Action<bool> DragCompatibilityRequested,EarlyAvoidanceRequested;
    private readonly Label title,description,metadata,dragHint,sizeValue;
    private readonly StatusDot dot;
    private readonly SoftButton enable,restore,fps30,fps60,follow,appearance,themeSystem,themeLight,themeDark;
    private readonly SoftButton resetSize,more;
    private readonly SizeSlider sizeSlider;
    private DashboardMenu options;
    private int optionsDpi;
    private readonly CheckBox dragCompatibility,earlyAvoidance;
    private readonly SoftButton avoidPress,avoidHover;
    private readonly PictureBox logo;
    private bool closeChoiceOpen,updatingPreferences;
    internal bool AllowClose;
    protected override bool UsesCustomCaption {get{return true;}}
    protected override Panel ScrollingContent {get{return content;}}
    internal Dashboard() {
        SuspendLayout();AutoScaleMode=AutoScaleMode.None;Font=Palette.Font(10);BackColor=Palette.Background;ForeColor=Palette.Ink;DoubleBuffered=true;
        FormBorderStyle=FormBorderStyle.None;MaximizeBox=false;StartPosition=FormStartPosition.CenterScreen;
        ClientSize=new Size(680,738);Text="大肥鱼 · 设置";Icon=Program.AppIcon;ShowIcon=false;
        more=new SoftButton("更多选项  ▾",222,8,116,32);more.Name="DashboardMore";more.InCaption=true;more.AccessibleName="更多选项";more.AccessibleRole=AccessibleRole.ButtonDropDown;
        more.Click+=delegate{ShowOptionsMenu();};Controls.Add(more);
        minimize=new CaptionButton(false,592,CaptionHeight);minimize.Click+=delegate{WindowState=FormWindowState.Minimized;};Controls.Add(minimize);
        closeWindow=new CaptionButton(true,636,CaptionHeight);closeWindow.Click+=delegate{Close();};Controls.Add(closeWindow);
        content=new Panel {Name="DashboardContent",Bounds=new Rectangle(0,CaptionHeight,680,690)};Controls.Add(content);
        logo=new PictureBox {Image=Program.LoadLogo(),Bounds=new Rectangle(28,17,48,48),SizeMode=PictureBoxSizeMode.Zoom};content.Controls.Add(logo);
        Palette.Label(content,"大肥鱼",92,13,330,36,21,true);
        var subtitle=Palette.Label(content,"Codex 宠物助手",94,52,340,25,9.5f);subtitle.ForeColor=Palette.Muted;
        var status=new CardPanel {Bounds=new Rectangle(28,86,624,184)};content.Controls.Add(status);
        dot=new StatusDot {Bounds=new Rectangle(20,26,10,10)};status.Controls.Add(dot);
        title=Palette.Label(status,"准备连接",42,18,560,30,14.5f,true);
        description=Palette.Label(status,"正在准备大肥鱼的运行环境。",20,58,584,40,9.5f);description.ForeColor=Palette.Muted;
        metadata=Palette.Label(status,"本机连接 · 自动跟随当前宠物",20,102,584,22,8.5f);metadata.ForeColor=Palette.Muted;
        enable=new SoftButton("启用大肥鱼",20,136,286,32,true);enable.Name="EnablePet";enable.Click+=delegate {if(EnableRequested!=null)EnableRequested();};status.Controls.Add(enable);
        restore=new SoftButton("恢复原宠物",318,136,286,32);restore.Name="RestorePet";restore.Click+=delegate {if(RestoreRequested!=null)RestoreRequested();};status.Controls.Add(restore);
        var looks=new CardPanel {Bounds=new Rectangle(28,286,300,264)};content.Controls.Add(looks);
        Palette.Label(looks,"外观与表情",20,18,260,28,11,true);
        var themeLabel=Palette.Label(looks,"界面主题",20,56,260,23,9);themeLabel.ForeColor=Palette.Muted;
        themeSystem=new SoftButton("跟随系统",20,83,84,32);themeSystem.Name="ThemeSystem";themeSystem.Click+=delegate {if(ThemeRequested!=null)ThemeRequested("system");};looks.Controls.Add(themeSystem);
        themeLight=new SoftButton("浅色",110,83,82,32);themeLight.Name="ThemeLight";themeLight.Click+=delegate {if(ThemeRequested!=null)ThemeRequested("light");};looks.Controls.Add(themeLight);
        themeDark=new SoftButton("深色",198,83,82,32);themeDark.Name="ThemeDark";themeDark.Click+=delegate {if(ThemeRequested!=null)ThemeRequested("dark");};looks.Controls.Add(themeDark);
        Palette.Label(looks,"宠物大小",20,132,94,24,9).ForeColor=Palette.Muted;
        sizeValue=Palette.Label(looks,"100%",132,127,64,24,10,true);sizeValue.Name="PetSizeValue";sizeValue.TextAlign=ContentAlignment.MiddleRight;
        sizeSlider=new SizeSlider(20,151,260);sizeSlider.ValueChanged+=ShowPetSize;
        sizeSlider.Committed+=RequestPetSize;looks.Controls.Add(sizeSlider);
        resetSize=new SoftButton("恢复默认",207,122,73,29);resetSize.Name="ResetPetSize";resetSize.AccessibleName="恢复宠物大小到 100%";
        resetSize.Click+=delegate {sizeSlider.RestoreDefault();};looks.Controls.Add(resetSize);
        Palette.Label(looks,"60%",20,183,60,17,8).ForeColor=Palette.Muted;
        var maximum=Palette.Label(looks,"130%",220,183,60,17,8);maximum.TextAlign=ContentAlignment.TopRight;maximum.ForeColor=Palette.Muted;
        appearance=new SoftButton("表情 / 装扮…",20,212,260,36,true);appearance.Name="OpenAppearance";appearance.Click+=delegate {if(AppearanceRequested!=null)AppearanceRequested();};looks.Controls.Add(appearance);
        var settings=new CardPanel {Bounds=new Rectangle(344,286,308,264)};content.Controls.Add(settings);
        Palette.Label(settings,"播放与窗口",20,18,268,28,11,true);Palette.Label(settings,"动画帧率",20,61,108,24,9.5f);
        fps30=new SoftButton("30 FPS",138,54,70,32);fps30.Name="Fps30";fps30.Click+=delegate {if(FpsRequested!=null)FpsRequested(30);};settings.Controls.Add(fps30);
        fps60=new SoftButton("60 FPS",218,54,70,32);fps60.Name="Fps60";fps60.Click+=delegate {if(FpsRequested!=null)FpsRequested(60);};settings.Controls.Add(fps60);
        dragCompatibility=new SettingsSwitch("拖窗兼容模式","DragCompatibility",20,98,268);dragCompatibility.CheckedChanged+=delegate {if(!updatingPreferences&&DragCompatibilityRequested!=null)DragCompatibilityRequested(dragCompatibility.Checked);};settings.Controls.Add(dragCompatibility);
        earlyAvoidance=new SettingsSwitch("提前避让","EarlyAvoidance",20,134,268);earlyAvoidance.CheckedChanged+=delegate {if(!updatingPreferences&&EarlyAvoidanceRequested!=null)EarlyAvoidanceRequested(earlyAvoidance.Checked);};settings.Controls.Add(earlyAvoidance);
        avoidPress=new SoftButton("按下时（推荐）",20,178,130,32);avoidPress.Name="AvoidPress";avoidPress.Click+=delegate{if(!updatingPreferences&&!avoidPress.Primary&&AvoidanceTriggerRequested!=null)AvoidanceTriggerRequested("press");};settings.Controls.Add(avoidPress);
        avoidHover=new SoftButton("移入时",158,178,130,32);avoidHover.Name="AvoidHover";avoidHover.Click+=delegate{if(!updatingPreferences&&!avoidHover.Primary&&AvoidanceTriggerRequested!=null)AvoidanceTriggerRequested("hover");};settings.Controls.Add(avoidHover);
        dragHint=Palette.Label(settings,"拖动时收起宠物，松手后恢复。",20,222,268,32,8.5f);dragHint.ForeColor=Palette.Muted;
        var binding=new CardPanel {Bounds=new Rectangle(28,566,624,48)};content.Controls.Add(binding);
        Palette.Label(binding,"当前宠物",20,14,180,24,9.5f);
        follow=new SoftButton("恢复自动跟随",376,9,228,30);follow.Name="FollowPet";follow.Click+=delegate {if(FollowRequested!=null)FollowRequested();};binding.Controls.Add(follow);
        var exit=new SoftButton("退出助手",352,638,124,36);exit.Name="ExitAssistant";exit.Click+=delegate {if(ExitRequested!=null)ExitRequested();};content.Controls.Add(exit);
        var hide=new SoftButton("收起到托盘",488,638,164,36,true);hide.Name="HideToTray";hide.Click+=delegate {if(HideRequested!=null)HideRequested();};content.Controls.Add(hide);
        FormClosing+=delegate(object sender,FormClosingEventArgs e) {
            if(AllowClose)return;
            e.Cancel=true;if(closeChoiceOpen)return;
            if(e.CloseReason==CloseReason.WindowsShutDown) { if(ExitRequested!=null)ExitRequested();return; }
            if(CloseBehavior=="tray"||CloseBehavior=="exit") {
                if(CloseActionRequested!=null)CloseActionRequested(CloseBehavior,false);
                return;
            }
            closeChoiceOpen=true;
            try {
                using(var choice=new CloseChoice()) {
                    var result=choice.ShowDialog(this);
                    if((result==DialogResult.Yes||result==DialogResult.No)&&CloseActionRequested!=null)
                        CloseActionRequested(result==DialogResult.Yes?"tray":"exit",choice.RememberChoice);
                }
            } finally { closeChoiceOpen=false; }
        };
        FinishLayout();
    }

    protected override CreateParams CreateParams {
        get{
            var parameters=base.CreateParams;
            parameters.Style|=0x000A0000; // WS_SYSMENU | WS_MINIMIZEBOX, no native caption
            return parameters;
        }
    }

    protected override bool ProcessCmdKey(ref Message message,Keys keyData) {
        if(keyData==(Keys.Alt|Keys.F4)){Close();return true;}
        return base.ProcessCmdKey(ref message,keyData);
    }

    protected override void WndProc(ref Message message) {
        if(message.Msg==0x0084&&content!=null){ // WM_NCHITTEST
            long point=message.LParam.ToInt64();
            var client=PointToClient(new Point(unchecked((short)point),unchecked((short)(point>>16))));
            if(ClientRectangle.Contains(client)){
                bool drag=client.Y<content.Top&&!more.Bounds.Contains(client)&&!minimize.Bounds.Contains(client)&&!closeWindow.Bounds.Contains(client);
                message.Result=(IntPtr)(drag?2:1);return; // HTCAPTION or HTCLIENT
            }
        }
        base.WndProc(ref message);
    }

    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e);if(content==null)return;
        float scale=DeviceDpi/96f;
        int height=content.Top;
        using(var brush=new SolidBrush(Palette.Chrome))e.Graphics.FillRectangle(brush,0,0,ClientSize.Width,height);
        using(var pen=new Pen(Palette.Line))e.Graphics.DrawLine(pen,0,height-1,ClientSize.Width,height-1);
        const TextFormatFlags flags=TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.NoPadding;
        TextRenderer.DrawText(e.Graphics,Text,captionTitle,new Rectangle((int)Math.Round(20*scale),0,(int)Math.Round(114*scale),height),Palette.Ink,flags);
        TextRenderer.DrawText(e.Graphics,"v"+Program.Version,captionVersion,new Rectangle((int)Math.Round(145*scale),0,(int)Math.Round(68*scale),height),Palette.Muted,flags);
    }

    private void ShowPetSize(int percent) {sizeValue.Text=percent+"%";}
    private void RequestPetSize(int percent) {if(!updatingPreferences&&PetSizeRequested!=null)PetSizeRequested(percent);}
    internal DashboardMenu ShowOptionsMenu() {
        if(options==null||optionsDpi!=DeviceDpi){
            if(options!=null)options.Dispose();optionsDpi=DeviceDpi;
            options=new DashboardMenu(optionsDpi,delegate{if(AboutRequested!=null)AboutRequested();},
                delegate{Program.OpenUrl(Program.Repository);},delegate{if(LogsRequested!=null)LogsRequested();},
                delegate(string choice){if(ClosePreferenceRequested!=null)ClosePreferenceRequested(choice);});
        }
        options.UpdateCloseBehavior(CloseBehavior);
        options.Show(more,new Point(more.Width,more.Height+Math.Max(1,DeviceDpi/16)),ToolStripDropDownDirection.BelowLeft);
        return options;
    }

    internal void UpdateOptions(string theme,bool compatibility,bool early,string trigger,int petSize,bool busy) {
        updatingPreferences=true;
        try {
            themeSystem.Primary=theme=="system";themeLight.Primary=theme=="light";themeDark.Primary=theme=="dark";
            themeSystem.Enabled=themeLight.Enabled=themeDark.Enabled=!busy;
            if(!sizeSlider.Editing)sizeSlider.Value=petSize;
            sizeSlider.Enabled=resetSize.Enabled=!busy;
            dragCompatibility.Checked=compatibility;dragCompatibility.Enabled=!busy;
            earlyAvoidance.Checked=early;earlyAvoidance.Enabled=compatibility&&!busy;
            avoidPress.Primary=trigger!="hover";avoidHover.Primary=trigger=="hover";
            avoidPress.Enabled=avoidHover.Enabled=compatibility&&early&&!busy;
            avoidPress.AccessibleDescription=avoidPress.Primary?"已选择":"未选择";avoidHover.AccessibleDescription=avoidHover.Primary?"已选择":"未选择";
            dragHint.Text=!compatibility?"已关闭拖窗兼容与提前避让。":early?(trigger=="hover"?"移入顶部／边框先收起，移开后恢复。":"按住顶部／边框时收起，松手后恢复。") :"拖动时收起宠物，松手后恢复。";
        }finally{updatingPreferences=false;}
    }
    internal void UpdateState(string heading,string detail,string meta,string state,bool busy,int fps,bool automatic) {
        title.Text=heading;description.Text=detail;metadata.Text=meta;
        dot.DotColor=state=="active"?Palette.Green:state=="error"?Palette.Red:state=="paused"?Palette.Muted:Palette.Amber;
        enable.Text=state=="active"?"已启用":busy?"正在处理…":state=="watching"?"打开 Codex":state=="relaunch-needed"?"安排接入":state=="handoff-wait"?"等待 Codex 退出":state=="error"?"重试连接":"启用大肥鱼";
        enable.Enabled=!busy&&state!="active"&&state!="handoff-wait";
        restore.Text=state=="handoff-wait"||state=="relaunch-needed"||state=="watching"?"停止自动接入":"恢复原宠物";
        restore.Enabled=!busy&&state!="paused";
        fps30.Primary=fps==30;fps60.Primary=fps==60;fps30.Enabled=!busy;fps60.Enabled=!busy;
        appearance.Enabled=!busy;
        follow.Text=automatic?"已自动跟随":"恢复自动跟随";follow.Enabled=!busy&&!automatic;
    }
    protected override void Dispose(bool disposing) { if(disposing){if(options!=null)options.Dispose();if(logo.Image!=null)logo.Image.Dispose();captionTitle.Dispose();captionVersion.Dispose();}base.Dispose(disposing); }
}

internal sealed class CloseChoice : AppForm {
    private readonly SettingsSwitch remember;
    internal bool RememberChoice {get{return remember.Checked;}}
    internal CloseChoice() {
        SuspendLayout();AutoScaleMode=AutoScaleMode.None;Font=Palette.Font(10);
        BackColor=Palette.Background;ClientSize=new Size(520,260);Text="关闭控制面板";Icon=Program.AppIcon;
        FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
        Palette.Label(this,"选择关闭方式",24,23,472,35,17,true);
        var text=Palette.Label(this,"收起到托盘：大肥鱼继续在后台陪伴。\n退出助手：恢复原宠物，Codex 会继续运行。",24,80,472,65,10);text.ForeColor=Palette.Muted;
        remember=new SettingsSwitch("记住这次选择","RememberCloseChoice",24,141,472);Controls.Add(remember);
        var hide=new SoftButton("收起到托盘",24,194,230,40,true);hide.Name="ConfirmHide";hide.DialogResult=DialogResult.Yes;Controls.Add(hide);
        var exit=new SoftButton("退出助手",266,194,230,40);exit.Name="ConfirmExit";exit.DialogResult=DialogResult.No;Controls.Add(exit);
        AcceptButton=hide;FinishLayout();
    }
    protected override bool ProcessDialogKey(Keys keyData) {
        if(keyData==Keys.Escape){DialogResult=DialogResult.Cancel;return true;}
        return base.ProcessDialogKey(keyData);
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
