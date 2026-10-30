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
  const html=`<!doctype html><style>html,body{margin:0;background:transparent;width:100%;height:100%;overflow:hidden}img{display:block;width:100%;height:100%;object-fit:contain}</style><img src="data:image/png;base64,${portrait}">`;
  await connection.send('Page.setDocumentContent',{frameId:frame,html});
  await connection.evaluate('document.querySelector("img").decode()');
  const sizes=[16,24,32,48,64,128,256];const entries=[];
  for(const size of sizes){
    await connection.send('Emulation.setDeviceMetricsOverride',{width:size,height:size,deviceScaleFactor:1,mobile:false});
    await sleep(50);
    const shot=await connection.send('Page.captureScreenshot',{format:'png',clip:{x:0,y:0,width:size,height:size,scale:1}});
    const bytes=Buffer.from(shot.data,'base64');entries.push({size,bytes});
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
  console.log(JSON.stringify({iconSizes:sizes,iconBytes:offset,source:'assets/branding/app-avatar.png'}));
}finally{if(connection){try{await connection.send('Browser.close');}catch{}connection.close();}if(browser.exitCode===null)browser.kill();}
