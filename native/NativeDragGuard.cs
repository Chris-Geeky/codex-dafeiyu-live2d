using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Out-of-context events, scoped to the selected Codex process. No input hook,
// injected code, cursor movement, or native window-position mutation.
internal sealed class NativeDragGuard : IDisposable {
    private delegate void EventCallback(IntPtr hook,uint evt,IntPtr window,int objectId,int childId,uint thread,uint time);
    private delegate bool WindowCallback(IntPtr window,IntPtr parameter);
    [StructLayout(LayoutKind.Sequential)] private struct Bounds { public int left,top,right,bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int x,y; }
    [StructLayout(LayoutKind.Sequential)] private struct GuiInfo { public uint size,flags;public IntPtr active,focus,capture,menu,movesize,caret;public int left,top,right,bottom; }
    [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint min,uint max,IntPtr module,EventCallback callback,uint process,uint thread,uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] private static extern IntPtr GetStyle(IntPtr window,int index);
    [DllImport("user32.dll")] private static extern bool GetGUIThreadInfo(uint thread,ref GuiInfo info);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback,IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window,out Bounds bounds);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window,int command);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] private static extern IntPtr GetAncestor(IntPtr window,uint flags);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr wparam,IntPtr lparam,uint flags,uint timeout,out UIntPtr result);
    private readonly Action<bool> changed;
    private readonly Func<bool> sampleFrame;
    private readonly EventCallback callback;
    private readonly Timer watchdog=new Timer();
    private readonly Timer proximity=new Timer();
    private IntPtr hook,window;
    private IntPtr petWindow;
    private bool fastHidden;
    private uint process,thread;
    private bool moving,avoiding,earlyAvoidance,nearFrame;
    private DateTime lastNear;
    internal bool Registered { get { return hook!=IntPtr.Zero; } }
    internal int FastHideCount {get;private set;}
    internal NativeDragGuard(Action<bool> action) : this(action,null) {}
    internal NativeDragGuard(Action<bool> action,Func<bool> frameSampler) {
        changed=action;sampleFrame=frameSampler??PointerNearFrame;callback=Receive;
        watchdog.Interval=250;watchdog.Tick+=delegate { CheckEnded(); };
        proximity.Interval=32;proximity.Tick+=delegate { CheckProximity(); };
    }
    internal void Configure(uint target,bool enabled) {
        target=enabled?target:0;if(target==process&&((target==0)||Registered))return;
        if(hook!=IntPtr.Zero)UnhookWinEvent(hook);hook=IntPtr.Zero;nearFrame=false;SetMoving(false);PublishAvoidance();petWindow=IntPtr.Zero;process=target;
        if(target!=0){hook=SetWinEventHook(0xA,0xB,IntPtr.Zero,callback,target,0,0);if(hook==IntPtr.Zero)Program.UiLog("Window drag compatibility: native event registration unavailable.");}
        UpdateProximityTimer();
    }
    internal void SetEarlyAvoidance(bool value) {if(earlyAvoidance==value)return;earlyAvoidance=value;if(!value){nearFrame=false;PublishAvoidance();}UpdateProximityTimer();}
    private void UpdateProximityTimer(){if(earlyAvoidance&&process!=0)proximity.Start();else proximity.Stop();}
    internal void TrackPet(uint target,int x,int y,int width,int height) {
        if(target==0||target!=process||avoiding)return;
        uint owner;if(petWindow!=IntPtr.Zero&&IsWindow(petWindow)){GetWindowThreadProcessId(petWindow,out owner);if(owner==target&&(GetStyle(petWindow,-20).ToInt64()&0x08000080)!=0)return;}
        petWindow=IntPtr.Zero;long best=120;
        EnumWindows(delegate(IntPtr candidate,IntPtr ignored){GetWindowThreadProcessId(candidate,out owner);Bounds r;if(owner!=target||!IsWindowVisible(candidate)||(GetStyle(candidate,-20).ToInt64()&0x08000080)==0||!GetWindowRect(candidate,out r))return true;long score=Math.Abs((long)r.left-x)+Math.Abs((long)r.top-y)+Math.Abs((long)(r.right-r.left)-width)+Math.Abs((long)(r.bottom-r.top)-height);if(score<best){best=score;petWindow=candidate;}return true;},IntPtr.Zero);
    }
    private bool ValidPet(){uint owner;if(petWindow==IntPtr.Zero||!IsWindow(petWindow))return false;GetWindowThreadProcessId(petWindow,out owner);return owner==process&&(GetStyle(petWindow,-20).ToInt64()&0x08000080)!=0;}
    private void SetMoving(bool value) {
        if(moving==value)return;moving=value;
        if(value)watchdog.Start();else watchdog.Stop();PublishAvoidance();
    }
    private void PublishAvoidance() {
        bool value=moving||(earlyAvoidance&&nearFrame);if(avoiding==value)return;avoiding=value;
        if(value){if(ValidPet()&&IsWindowVisible(petWindow))fastHidden=ShowWindowAsync(petWindow,0);if(fastHidden)FastHideCount++;}
        else{if(fastHidden&&ValidPet())ShowWindowAsync(petWindow,4);fastHidden=false;}
        try { changed(value); } catch { Program.UiLog("Window drag compatibility: controller pipe unavailable."); }
    }
    private void CheckProximity() {
        bool near=false;try{near=sampleFrame();}catch{near=false;}
        if(near){nearFrame=true;lastNear=DateTime.UtcNow;}
        // Keep a drag hidden across child windows and brief gaps at frame edges.
        else if(!moving&&(!avoiding||(GetAsyncKeyState(1)&0x8000)==0)&&(DateTime.UtcNow-lastNear).TotalMilliseconds>=200)nearFrame=false;
        PublishAvoidance();
    }
    internal static bool IsFrameArea(int hit,int x,int y,int left,int top,int right,int bottom,int dpi) {
        if(x<left||x>=right||y<top||y>=bottom)return false;
        if(hit==2||(hit>=10&&hit<=18))return true;
        if(hit!=1&&hit!=3&&hit!=8&&hit!=9&&hit!=20&&hit!=21)return false;
        int edge=Math.Max(4,(int)Math.Round(8*Math.Max(96,dpi)/96.0)),header=(int)Math.Round(48*Math.Max(96,dpi)/96.0);
        return y-top<header||x-left<edge||right-x<=edge||bottom-y<=edge;
    }
    private bool PointerNearFrame() {
        Point point;if(!GetCursorPos(out point))return false;IntPtr target=GetAncestor(WindowFromPoint(point),2);if(target==IntPtr.Zero)return false;
        uint owner;GetWindowThreadProcessId(target,out owner);if(owner!=process||(GetStyle(target,-20).ToInt64()&0x08000080)!=0)return false;
        Bounds r;if(!GetWindowRect(target,out r))return false;uint dpi=GetDpiForWindow(target);if(dpi==0)dpi=96;
        if(!IsFrameArea(1,point.x,point.y,r.left,r.top,r.right,r.bottom,(int)dpi))return false;
        // Query only the selected Codex frame, with a bounded wait. This does not forward input.
        UIntPtr result;int packed=unchecked((point.x&0xffff)|((point.y&0xffff)<<16));
        if(SendMessageTimeout(target,0x84,IntPtr.Zero,new IntPtr(packed),0x23,12,out result)==IntPtr.Zero)return false;
        int hit=unchecked((int)result.ToUInt64());return IsFrameArea(hit,point.x,point.y,r.left,r.top,r.right,r.bottom,(int)dpi);
    }
    private void Receive(IntPtr source,uint evt,IntPtr target,int objectId,int childId,uint eventThread,uint time) {
        if(target==IntPtr.Zero||objectId!=0||childId!=0)return;
        uint owner;GetWindowThreadProcessId(target,out owner);if(owner!=process)return;
        if(evt==0xB){if(window==target)SetMoving(false);return;}
        if(evt!=0xA||(GetStyle(target,-20).ToInt64()&0x08000080)!=0)return;
        window=target;thread=eventThread;SetMoving(true);
    }
    private void CheckEnded() { var info=new GuiInfo {size=(uint)Marshal.SizeOf(typeof(GuiInfo))};if(!GetGUIThreadInfo(thread,ref info)||(info.flags&2)==0||info.movesize!=window)SetMoving(false); }
    public void Dispose() { Configure(0,false);watchdog.Dispose();proximity.Dispose();GC.KeepAlive(callback); }
}
