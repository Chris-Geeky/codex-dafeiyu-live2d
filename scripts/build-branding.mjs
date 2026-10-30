// Package the attributed whale-girl portrait into Windows icon sizes.
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import crypto from 'node:crypto';
import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {CdpConnection,listTargets} from '../cdp.mjs';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const directory=path.join(root,'assets/branding');
const profile=fs.mkdtempSync(path.join(os.tmpdir(),'dafeiyu-icon-'));
const browser=spawn(process.env.EDGE_BINARY||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',[
  '--headless=new','--edge-skip-compat-layer-relaunch','--remote-debugging-address=127.0.0.1','--remote-debugging-port=0',`--user-data-dir=${profile}`,
  '--no-first-run','--no-default-browser-check','--disable-background-networking','--disable-extensions','--disable-sync','about:blank'
],{windowsHide:true,stdio:'ignore'});
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
let connection;
try{
  const portFile=path.join(profile,'DevToolsActivePort');
  for(let i=0;i<150&&!fs.existsSync(portFile);i++)await sleep(100);
  const port=Number(fs.readFileSync(portFile,'utf8').split('\n')[0]);
  const target=(await listTargets(port)).find(target=>target.type==='page');
  connection=await CdpConnection.connect(target.webSocketDebuggerUrl);
  await connection.send('Page.enable');
  await connection.send('Emulation.setDefaultBackgroundColorOverride',{color:{r:0,g:0,b:0,a:0}});
  const frame=(await connection.send('Page.getFrameTree')).frameTree.frame.id;
  const portrait=fs.readFileSync(path.join(directory,'app-avatar.png')).toString('base64');
  const html=`<!doctype html><img id="portrait" src="data:image/png;base64,${portrait}"><canvas></canvas>`;
  await connection.send('Page.setDocumentContent',{frameId:frame,html});
  await connection.evaluate('Promise.all([...document.images].map(image=>image.decode()))');
  const sizes=[16,20,24,28,32,36,40,48,56,64,72,80,96,128,256];const entries=[];
  for(const size of sizes){
    const encoded=await connection.evaluate(`(()=>{const canvas=document.querySelector('canvas');canvas.width=canvas.height=${size};const context=canvas.getContext('2d');context.imageSmoothingEnabled=true;context.imageSmoothingQuality='high';context.drawImage(document.getElementById('portrait'),0,0,${size},${size});return canvas.toDataURL('image/png').split(',')[1];})()`);
    const bytes=Buffer.from(encoded,'base64');entries.push({size,bytes});
    if(size===256)fs.writeFileSync(path.join(directory,'app-256.png'),bytes);
  }
  const header=Buffer.alloc(6+16*entries.length);header.writeUInt16LE(1,2);header.writeUInt16LE(entries.length,4);
  let offset=header.length;
  entries.forEach(({size,bytes},i)=>{const at=6+i*16;header[at]=size===256?0:size;header[at+1]=size===256?0:size;header.writeUInt16LE(1,at+4);header.writeUInt16LE(32,at+6);header.writeUInt32LE(bytes.length,at+8);header.writeUInt32LE(offset,at+12);offset+=bytes.length;});
  fs.writeFileSync(path.join(directory,'app.ico'),Buffer.concat([header,...entries.map(entry=>entry.bytes)]));
  const manifestPath=path.join(root,'assets-manifest.json');
  const manifest=JSON.parse(fs.readFileSync(manifestPath,'utf8'));
  for(const name of ['app-avatar.png','app.ico','app-256.png']){
    const relative='assets/branding/'+name;const bytes=fs.readFileSync(path.join(root,relative));
    const entry={path:relative,bytes:bytes.length,sha256:crypto.createHash('sha256').update(bytes).digest('hex')};
    const index=manifest.files.findIndex(file=>file.path===relative);if(index<0)manifest.files.push(entry);else manifest.files[index]=entry;
  }
  fs.writeFileSync(manifestPath,JSON.stringify(manifest,null,2)+'\n');
  const compareAt=process.argv.indexOf('--compare-with');
  if(compareAt>=0){
    const old=fs.readFileSync(path.resolve(process.argv[compareAt+1]));const previous=new Map();
    for(let i=0;i<old.readUInt16LE(4);i++){const at=6+i*16;const size=old[at]||256;previous.set(size,old.subarray(old.readUInt32LE(at+12),old.readUInt32LE(at+12)+old.readUInt32LE(at+8)));}
    const labels=[16,24,32,48,64];
    const cells=(before,dark)=>labels.map(size=>{const bytes=before?previous.get(size):entries.find(entry=>entry.size===size).bytes;return `<div class="cell"><img width="${size}" height="${size}" src="data:image/png;base64,${bytes.toString('base64')}"><span>${size}px</span></div>`;}).join('');
    const compare=`<!doctype html><style>body{margin:0;background:#f5f7fb;font:14px 'Microsoft YaHei UI',sans-serif;color:#21314a}h2{margin:20px 24px 8px;font-size:18px}p{margin:0 24px 20px;color:#65748a}.row{display:flex;padding:12px 20px;align-items:center}.name{width:90px}.cell{width:108px;height:84px;display:flex;align-items:center;justify-content:center;position:relative}.cell span{position:absolute;bottom:0;font-size:11px;opacity:.7}.dark{background:#24262b;color:#e6ebf4}.light{background:#eef3fa}</style><h2>鲸鱼娘图标 · 实际像素比较</h2><p>保留同一张原图，仅比较缩小采样；图标按标注像素显示。</p><div class="row dark"><div class="name">原版</div>${cells(true,true)}</div><div class="row dark"><div class="name">高质量采样</div>${cells(false,true)}</div><div class="row light"><div class="name">原版</div>${cells(true,false)}</div><div class="row light"><div class="name">高质量采样</div>${cells(false,false)}</div>`;
    await connection.send('Emulation.setDeviceMetricsOverride',{width:680,height:550,deviceScaleFactor:1,mobile:false});
    await connection.send('Page.setDocumentContent',{frameId:frame,html:compare});await connection.evaluate('Promise.all([...document.images].map(image=>image.decode()))');
    const shot=await connection.send('Page.captureScreenshot',{format:'png'});const output=path.join(root,'tests/.artifacts/icon-comparison.png');fs.mkdirSync(path.dirname(output),{recursive:true});fs.writeFileSync(output,Buffer.from(shot.data,'base64'));
  }
  console.log(JSON.stringify({iconSizes:sizes,iconBytes:offset,source:'assets/branding/app-avatar.png'}));
}finally{if(connection){try{await connection.send('Browser.close');}catch{}connection.close();}if(browser.exitCode===null)browser.kill();}
