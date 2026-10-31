import {installCodexLive2D} from './renderer.mjs';
const $=id=>document.getElementById(id),clone=value=>JSON.parse(JSON.stringify(value));
let catalog,selection,api=null,dynamic=true,fps=60,effectId,opened=null,busy=false,lastFrames=0,lastSample=performance.now(),measured=0;
const fields=new Map(),message=$('message');
const notify=(text,error=false)=>{message.textContent=text;message.className=error?'error':'success'};
function defaults(){return {expression:'auto',autoExpressions:true,accessories:Object.fromEntries(catalog.groups.map(g=>[g.id,'none']))}}
function closeMenu(){if(opened){opened.menu.remove();opened.button.setAttribute('aria-expanded','false');opened=null}}
function makeChoice(container,label,items,value,change){
 const wrapper=document.createElement('div');wrapper.className='choice';const button=document.createElement('button');button.type='button';button.setAttribute('role','combobox');button.setAttribute('aria-label',label);button.setAttribute('aria-haspopup','listbox');button.setAttribute('aria-expanded','false');wrapper.append(button);container.append(wrapper);
 let current=value;
 const render=()=>{button.replaceChildren();const text=document.createElement('span');text.className='choice-text';text.textContent=items.find(i=>i.id===current)?.label??items[0].label;const arrow=document.createElement('span');arrow.className='arrow';arrow.textContent='▼';button.append(text,arrow)};
 const choose=id=>{current=id;render();change(id);closeMenu();button.focus()};
 const open=()=>{if(opened?.button===button){closeMenu();return}closeMenu();const menu=document.createElement('div');menu.className='choice-menu';menu.setAttribute('role','listbox');menu.setAttribute('aria-label',label);items.forEach(item=>{const option=document.createElement('button');option.type='button';option.setAttribute('role','option');option.setAttribute('aria-selected',String(item.id===current));option.textContent=item.label;option.onclick=()=>choose(item.id);menu.append(option)});document.body.append(menu);const r=button.getBoundingClientRect(),height=Math.min(250,items.length*35+10),below=innerHeight-r.bottom-10;menu.style.left=r.left+'px';menu.style.width=r.width+'px';menu.style.maxHeight=Math.max(90,below<110?r.top-10:below)+'px';menu.style.top=(below<110?Math.max(8,r.top-height):r.bottom+3)+'px';opened={menu,button};button.setAttribute('aria-expanded','true');const selected=menu.querySelector('[aria-selected=true]');selected?.scrollIntoView({block:'nearest'});selected?.focus()};
 button.onclick=open;button.onkeydown=e=>{if(['ArrowDown','ArrowUp','Enter',' '].includes(e.key)){e.preventDefault();open()}};

 render();return {set(id){current=id;render()},button};
}
document.addEventListener('pointerdown',e=>{if(opened&&!opened.menu.contains(e.target)&&e.target!==opened.button&&!opened.button.contains(e.target))closeMenu()});window.addEventListener('resize',closeMenu);window.addEventListener('scroll',closeMenu);
document.addEventListener('keydown',e=>{if(e.key==='Escape'){e.preventDefault();if(opened){const button=opened.button;closeMenu();button.focus()}else if(!busy)host('close');return}if(!opened||!opened.menu.contains(document.activeElement))return;const options=[...opened.menu.children],at=options.indexOf(document.activeElement);if(e.key==='ArrowDown'||e.key==='ArrowUp'){e.preventDefault();options[(at+(e.key==='ArrowDown'?1:-1)+options.length)%options.length].focus()}if(e.key==='Escape'){e.preventDefault();const button=opened.button;closeMenu();button.focus()}});
function updatePreview(){try{api?.setAppearance(selection)}catch{notify('预览暂未更新，请重试选择。',true)}$('automatic').disabled=selection.expression!=='auto'}
function showSelection(){fields.get('expression')?.set(selection.expression);$('automatic').checked=selection.autoExpressions;for(const group of catalog.groups)fields.get(group.id)?.set(selection.accessories[group.id]??'none');updatePreview()}
function host(type,extra={}){const request={type,...extra};if(window.chrome?.webview)window.chrome.webview.postMessage(request);else window.dispatchEvent(new CustomEvent('dafeiyu-host-request',{detail:request}))}
function working(value){busy=value;for(const id of ['apply','reset','close','play-effect'])$(id).disabled=value}
function apply(){if(busy)return;working(true);notify('正在应用…');host('apply',{selection:clone(selection)})}
function receive(data){if(!data||typeof data!=='object')return;if(data.type==='applied'){working(false);if(data.selection){selection=data.selection;showSelection()}notify(data.applied?'已保存并应用到大肥鱼。':'已保存；启用或重新连接大肥鱼后生效。')}else if(data.type==='effect-result'){working(false);notify(data.applied?'已在宠物上播放。':'请先启用大肥鱼，再播放效果。',!data.applied)}else if(data.type==='error'){working(false);notify(data.message||'操作暂未完成。',true)}}
window.chrome?.webview?.addEventListener('message',event=>receive(event.data));
$('apply').onclick=apply;$('reset').onclick=()=>{selection=defaults();showSelection();apply()};$('close').onclick=()=>host('close');
$('automatic').onchange=()=>{selection.autoExpressions=$('automatic').checked;updatePreview()};
function mode(value){dynamic=value;api?.setPreviewPlaying(value);lastFrames=api?.status().frames??0;lastSample=performance.now();measured=0;$('dynamic').setAttribute('aria-pressed',String(value));$('static').setAttribute('aria-pressed',String(!value));drawFps()}
$('dynamic').onclick=()=>mode(true);$('static').onclick=()=>mode(false);
$('preview-effect').onclick=()=>{if(!api?.playEffect(effectId))notify('预览尚未准备好。',true)};
$('play-effect').onclick=()=>{if(busy)return;working(true);host('effect',{id:effectId})};
function drawFps(){$('fps').textContent=dynamic?`动态 · 目标 ${fps} FPS${measured?' · 实测 '+measured:''}`:'静态 · 修改外观后更新'}
async function script(url){await new Promise((resolve,reject)=>{const e=document.createElement('script');e.src=url;e.onload=resolve;e.onerror=reject;document.head.append(e)})}
async function sync(){
 try{const state=await(await fetch('state.json')).json();if(fps!==state.fps){fps=state.fps;api?.setFps(fps);lastFrames=api?.status().frames??0;lastSample=performance.now();measured=0}$('pet').setAttribute('data-codex-pet-state',state.state)}catch{}
 const status=api?.status();const now=performance.now();if(status){if(now-lastSample>=500){measured=Math.max(0,Math.round((status.frames-lastFrames)*1000/(now-lastSample)));lastFrames=status.frames;lastSample=now;}host('preview-status',{status:{ready:true,frames:status.frames,fps:status.fps,measuredFps:measured,playing:dynamic,selection:clone(selection),lastError:status.lastError}})}drawFps();
}
try{
 const initial=await(await fetch('initial.json')).json();catalog=initial.catalog;selection=initial.selection;fps=initial.fps;
 fields.set('expression',makeChoice($('expression'),'表情',catalog.expressions,selection.expression,id=>{selection.expression=id;updatePreview()}));
 for(const group of catalog.groups){const field=document.createElement('div');field.className='field';const label=document.createElement('label');label.textContent=group.label;field.append(label);$('accessories').append(field);fields.set(group.id,makeChoice(field,group.label,group.options,selection.accessories[group.id]??'none',id=>{selection.accessories[group.id]=id;updatePreview()}))}
 effectId=catalog.effects[0].id;makeChoice($('effect-choice'),'桌面效果',catalog.effects,effectId,id=>effectId=id);showSelection();drawFps();
 window.__dafeiyuEditor={state:()=>({selection:clone(selection),dynamic,fps,ready:!!api,busy}),select(value){selection={...defaults(),...value,accessories:{...defaults().accessories,...value.accessories}};showSelection()},mode,receive,apply,effect:id=>api?.playEffect(id)};
 if(!initial.coreReady)$('loading').textContent='先在控制面板启用大肥鱼，完成动画组件准备。';
 else{await script('core.js');await script('vendor.js');const bundle=await(await fetch('bundle.json')).json();await installCodexLive2D({petId:'preview',allowAnyPet:false,fps,preview:true,appearance:selection,bundle});api=globalThis.__codexLive2D;if(!api.status().active)throw Error('Model not ready');$('loading').style.display='none';lastFrames=api.status().frames;lastSample=performance.now();measured=0;updatePreview();await sync();setInterval(sync,1000)}
}catch{notify('预览暂未加载完成，可关闭后重新打开。',true);$('loading').textContent='预览暂未加载完成。'}
