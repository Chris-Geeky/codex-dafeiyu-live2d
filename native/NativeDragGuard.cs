using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

// Out-of-context events, scoped to the selected Codex process. No input hook,
// injected code, cursor movement, or native window-position mutation.
internal sealed class NativeDragGuard : IDisposable {
    private delegate void EventCallback(IntPtr hook,uint evt,IntPtr window,int objectId,int childId,uint thread,uint time);
    private delegate bool WindowCallback(IntPtr window,IntPtr parameter);
    [StructLayout(LayoutKind.Sequential)] private struct Bounds { public int left,top,right,bottom; }
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
    private readonly Action<bool> changed;
    private readonly EventCallback callback;
    private readonly Timer watchdog=new Timer();
    private IntPtr hook,window;
    private IntPtr petWindow;
    private bool fastHidden;
    private uint process,thread;
    private bool moving;
    internal bool Registered { get { return hook!=IntPtr.Zero; } }
    internal int FastHideCount {get;private set;}
    internal NativeDragGuard(Action<bool> action) { changed=action;callback=Receive;watchdog.Interval=250;watchdog.Tick+=delegate { CheckEnded(); }; }
    internal void Configure(uint target,bool enabled) {
        target=enabled?target:0;if(target==process&&((target==0)||Registered))return;
        if(hook!=IntPtr.Zero)UnhookWinEvent(hook);hook=IntPtr.Zero;SetMoving(false);petWindow=IntPtr.Zero;process=target;
        if(target!=0){hook=SetWinEventHook(0xA,0xB,IntPtr.Zero,callback,target,0,0);if(hook==IntPtr.Zero)Program.UiLog("Window drag compatibility: native event registration unavailable.");}
    }
    internal void TrackPet(uint target,int x,int y,int width,int height) {
        if(target==0||target!=process||moving)return;
        uint owner;if(petWindow!=IntPtr.Zero&&IsWindow(petWindow)){GetWindowThreadProcessId(petWindow,out owner);if(owner==target&&(GetStyle(petWindow,-20).ToInt64()&0x08000080)!=0)return;}
        petWindow=IntPtr.Zero;long best=120;
        EnumWindows(delegate(IntPtr candidate,IntPtr ignored){GetWindowThreadProcessId(candidate,out owner);Bounds r;if(owner!=target||!IsWindowVisible(candidate)||(GetStyle(candidate,-20).ToInt64()&0x08000080)==0||!GetWindowRect(candidate,out r))return true;long score=Math.Abs((long)r.left-x)+Math.Abs((long)r.top-y)+Math.Abs((long)(r.right-r.left)-width)+Math.Abs((long)(r.bottom-r.top)-height);if(score<best){best=score;petWindow=candidate;}return true;},IntPtr.Zero);
    }
    private bool ValidPet(){uint owner;if(petWindow==IntPtr.Zero||!IsWindow(petWindow))return false;GetWindowThreadProcessId(petWindow,out owner);return owner==process&&(GetStyle(petWindow,-20).ToInt64()&0x08000080)!=0;}
    private void SetMoving(bool value) {
        if(moving==value)return;moving=value;
        if(value){if(ValidPet()&&IsWindowVisible(petWindow))fastHidden=ShowWindowAsync(petWindow,0);if(fastHidden)FastHideCount++;watchdog.Start();}
        else{watchdog.Stop();if(fastHidden&&ValidPet())ShowWindowAsync(petWindow,4);fastHidden=false;}
        try { changed(value); } catch { Program.UiLog("Window drag compatibility: controller pipe unavailable."); }
    }
    private void Receive(IntPtr source,uint evt,IntPtr target,int objectId,int childId,uint eventThread,uint time) {
        if(target==IntPtr.Zero||objectId!=0||childId!=0)return;
        uint owner;GetWindowThreadProcessId(target,out owner);if(owner!=process)return;
        if(evt==0xB){if(window==target)SetMoving(false);return;}
        if(evt!=0xA||(GetStyle(target,-20).ToInt64()&0x08000080)!=0)return;
        window=target;thread=eventThread;SetMoving(true);
    }
    private void CheckEnded() { var info=new GuiInfo {size=(uint)Marshal.SizeOf(typeof(GuiInfo))};if(!GetGUIThreadInfo(thread,ref info)||(info.flags&2)==0||info.movesize!=window)SetMoving(false); }
    public void Dispose() { Configure(0,false);watchdog.Dispose();GC.KeepAlive(callback); }
}
