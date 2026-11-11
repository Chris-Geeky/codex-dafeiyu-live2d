// Render the distributed model in an isolated Edge tab. No running Codex is used.
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {spawn} from 'node:child_process';
import {CdpConnection,listTargets} from '../cdp.mjs';
import {installOnConnection,packageRoot} from '../cli.mjs';
const output=path.resolve(process.argv[2]??path.join(packageRoot,'tests/.artifacts/model-branding'));
const profile=fs.mkdtempSync(path.join(os.tmpdir(),'dafeiyu-branding-'));
const edge=spawn(process.env.EDGE_BINARY||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',[
  '--headless=new','--edge-skip-compat-layer-relaunch','--remote-debugging-address=127.0.0.1','--remote-debugging-port=0',
  '--user-data-dir='+profile,'--disable-background-networking','--disable-extensions','--disable-sync','--no-first-run','about:blank'
],{windowsHide:true,stdio:'ignore'});
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
let c;
try {
  const portFile=path.join(profile,'DevToolsActivePort');
  for(let i=0;i<100&&!fs.existsSync(portFile);i++)await sleep(100);
  const port=Number(fs.readFileSync(portFile,'utf8').split('\n')[0]);
  c=await CdpConnection.connect((await listTargets(port)).find(target=>target.type==='page').webSocketDebuggerUrl);
  await c.send('Page.enable');
  await c.send('Emulation.setDefaultBackgroundColorOverride',{color:{r:0,g:0,b:0,a:0}});
  await c.send('Emulation.setDeviceMetricsOverride',{width:520,height:534,deviceScaleFactor:2,mobile:false});
  const frameId=(await c.send('Page.getFrameTree')).frameTree.frame.id;
  await c.send('Page.setDocumentContent',{frameId,html:'<body style="margin:0"><div class="codex-avatar-root" data-codex-pet-id="portrait" data-codex-pet-state="idle" style="position:absolute;left:60px;top:50px;width:400px;height:434px"></div></body>'});
  await installOnConnection(c,{petId:'portrait',petSize:100,appearance:{expression:'none',autoExpressions:false}});
  await sleep(260);
  const state=await c.evaluate('__codexLive2D.status()');
  if(!state.active||state.face?.eyes?.left<.8||state.face?.eyes?.right<.8)throw new Error('Could not capture the neutral model with open eyes');
  const raw=Buffer.from((await c.send('Page.captureScreenshot',{format:'png',clip:{x:60,y:50,width:400,height:434,scale:1}})).data,'base64');
  fs.mkdirSync(output,{recursive:true});fs.writeFileSync(path.join(output,'neutral.png'),raw);
  const portrait=await c.evaluate(`(async()=>{
    const img=new Image();img.src='data:image/png;base64,${raw.toString('base64')}';await img.decode();
    const canvas=document.createElement('canvas');canvas.width=canvas.height=512;
    const ctx=canvas.getContext('2d'),s=img.naturalWidth/800;
    ctx.imageSmoothingQuality='high';
    ctx.beginPath();ctx.moveTo(0,0);ctx.lineTo(512,0);ctx.lineTo(512,313);ctx.lineTo(494,322);
    ctx.lineTo(483,340);ctx.lineTo(468,365);ctx.lineTo(453,395);ctx.lineTo(437,454);ctx.lineTo(0,454);ctx.closePath();ctx.clip();
    ctx.drawImage(img,174*s,115*s,469*s,394*s,16,52,480,403);
    // Trim the desk and prop at the bottom; the hair, face and ears are model pixels.
    ctx.globalCompositeOperation='destination-in';
    const fade=ctx.createLinearGradient(0,403,0,448);fade.addColorStop(0,'#fff');fade.addColorStop(1,'#fff0');
    ctx.fillStyle=fade;ctx.fillRect(0,0,512,512);
    return canvas.toDataURL('image/png').split(',')[1];
  })()`);
  fs.writeFileSync(path.join(output,'app-avatar.png'),Buffer.from(portrait,'base64'));
  console.log(JSON.stringify({output,modelScale:state.modelScale,source:[400,434],resolution:2}));
}finally{
  if(c){try{await c.send('Browser.close')}catch{}c.close()}
  if(edge.exitCode===null)edge.kill();
}
