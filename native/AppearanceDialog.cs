using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

internal sealed class AppearanceDialog : AppForm {
    private sealed class Choice {
        internal string Id,Label;
        public override string ToString() { return Label; }
    }
    private readonly ComboBox expression;
    private readonly CheckBox automatic;
    private readonly Dictionary<string,ComboBox> accessories=new Dictionary<string,ComboBox>();
    private readonly SoftButton apply,reset,close,effectButton;
    private readonly ComboBox effects;
    private readonly Label message;
    private bool working;

    internal AppearanceDialog(Dictionary<string,object> selection) {
        SuspendLayout();AutoScaleMode=AutoScaleMode.None;Font=Palette.Font(10);
        BackColor=Palette.Background;ClientSize=new Size(600,770);Text="表情与装扮 · 大肥鱼";Icon=Program.AppIcon;
        FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;ShowInTaskbar=false;
        Palette.Label(this,"表情与装扮",24,20,552,35,17,true);
        var intro=Palette.Label(this,"基础设置：选择后点“应用”，即可在宠物上查看。\n窗口内实时预览需要系统 WebView2 运行库。",24,66,552,46,10);intro.ForeColor=Palette.Muted;
        var catalog=Program.LoadAppearanceCatalog();
        Palette.Label(this,"表情",24,120,190,25,10,true);
        expression=CreateChoices((IEnumerable)catalog["expressions"],"ExpressionChoice",238,115,338);
        expression.AccessibleName="表情";
        automatic=new CheckBox { Text="自动播放轻松的小表情（跟随状态时生效）",Bounds=new Rectangle(26,162,550,30),Font=Palette.Font(10),BackColor=Palette.Background,Name="AutoExpressions" };Controls.Add(automatic);
        expression.SelectedIndexChanged+=delegate { automatic.Enabled=!working&&((Choice)expression.SelectedItem).Id=="auto"; };
        int index=0;
        foreach(Dictionary<string,object> group in (IEnumerable)catalog["groups"]) {
            int x=index%2==0?24:312,y=214+index/2*68;string id=Program.Text(group,"id");
            Palette.Label(this,Program.Text(group,"label"),x,y,264,24,10,true);
            accessories[id]=CreateChoices((IEnumerable)group["options"],"Accessory_"+id,x,y+26,264);accessories[id].AccessibleName=Program.Text(group,"label");index++;
        }
        var hint=Palette.Label(this,"手势在动作结束后显示；手机配色在拿出手机时可见。",24,559,552,28,9);hint.ForeColor=Palette.Muted;
        effects=CreateChoices((IEnumerable)catalog["effects"],"DesktopEffect",24,600,360);effects.AccessibleName="桌面效果";
        effectButton=new SoftButton("播放效果",396,596,180,38);effectButton.Click+=delegate { PlayEffect(); };Controls.Add(effectButton);
        message=Palette.Label(this,"",24,650,552,36,9);message.ForeColor=Palette.Muted;
        reset=new SoftButton("恢复默认",24,708,170,38);reset.Name="ResetAppearance";reset.Click+=delegate { SetSelection(null);ApplyCurrent(); };Controls.Add(reset);
        close=new SoftButton("关闭",210,708,170,38);close.DialogResult=DialogResult.Cancel;Controls.Add(close);
        apply=new SoftButton("应用",396,708,180,38,true);apply.Name="ApplyAppearance";apply.Click+=delegate { ApplyCurrent(); };Controls.Add(apply);
        AcceptButton=apply;CancelButton=close;FormClosing+=delegate(object sender,FormClosingEventArgs e) { if(working)e.Cancel=true; };
        SetSelection(selection);FinishLayout();
    }
    private ComboBox CreateChoices(IEnumerable items,string name,int x,int y,int width) {
        var combo=new ComboBox { Name=name,Bounds=new Rectangle(x,y,width,34),DropDownStyle=ComboBoxStyle.DropDownList,Font=Palette.Font(10),FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=Palette.Ink };
        foreach(Dictionary<string,object> item in items)combo.Items.Add(new Choice { Id=Program.Text(item,"id"),Label=Program.Text(item,"label") });
        combo.SelectedIndex=0;Controls.Add(combo);return combo;
    }
    private void SelectChoice(ComboBox combo,string id) { foreach(Choice choice in combo.Items)if(choice.Id==id) { combo.SelectedItem=choice;return; }combo.SelectedIndex=0; }
    private void SetSelection(Dictionary<string,object> selection) {
        SelectChoice(expression,Program.Text(selection,"expression","auto"));
        object auto;automatic.Checked=selection==null||!selection.TryGetValue("autoExpressions",out auto)||Convert.ToBoolean(auto);
        object raw;var choices=selection!=null&&selection.TryGetValue("accessories",out raw)?raw as Dictionary<string,object>:null;
        foreach(var item in accessories)SelectChoice(item.Value,Program.Text(choices,item.Key,"none"));
        automatic.Enabled=((Choice)expression.SelectedItem).Id=="auto";
    }
    internal Dictionary<string,object> Selection() {
        var choices=new Dictionary<string,object>();foreach(var item in accessories)choices[item.Key]=((Choice)item.Value.SelectedItem).Id;
        return new Dictionary<string,object> { {"expression",((Choice)expression.SelectedItem).Id},{"autoExpressions",automatic.Checked},{"accessories",choices} };
    }
    private void SetWorking(bool value) {
        working=value;apply.Enabled=reset.Enabled=close.Enabled=expression.Enabled=effects.Enabled=effectButton.Enabled=!value;
        automatic.Enabled=!value&&((Choice)expression.SelectedItem).Id=="auto";
        foreach(var combo in accessories.Values)combo.Enabled=!value;
    }
    private async void PlayEffect() {
        if(working||Program.IsPreview)return;string id=((Choice)effects.SelectedItem).Id;SetWorking(true);
        try { string output=await Task.Run(delegate { return Program.Run("appearance.mjs","effect "+id); });if(IsDisposed)return;var result=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(output);message.Text=Convert.ToBoolean(result["applied"])?"已播放桌面效果。":"请先启用大肥鱼，再播放效果。"; }
        catch(Exception error) { if(!IsDisposed)message.Text=error.Message; }
        finally { if(!IsDisposed)SetWorking(false); }
    }
    private async void ApplyCurrent() {
        if(working||Program.IsPreview)return;
        string encoded=Convert.ToBase64String(Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(Selection())));
        SetWorking(true);message.ForeColor=Palette.Muted;message.Text="正在应用…";
        try {
            string output=await Task.Run(delegate { return Program.Run("appearance.mjs","apply "+encoded); });
            if(IsDisposed)return;
            var result=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(output);
            message.ForeColor=Palette.Green;
            message.Text=Convert.ToBoolean(result["applied"])?"已保存并应用到大肥鱼。":"已保存；启用或重新连接大肥鱼后生效。";
        } catch(Exception error) { Program.UiLog(error.ToString());if(!IsDisposed) { message.ForeColor=Palette.Red;message.Text="应用失败："+error.Message; } }
        finally { if(!IsDisposed)SetWorking(false); }
    }
}
