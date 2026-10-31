import './isolated-data.mjs';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import http from 'node:http';
import {spawn} from 'node:child_process';
import {CdpConnection,listTargets} from '../cdp.mjs';
import {restoreNativePets} from '../restore.mjs';
import {packageRoot} from '../cli.mjs';

const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const artifacts=path.join(packageRoot,'tests/.artifacts');fs.mkdirSync(artifacts,{recursive:true});
const profile=fs.mkdtempSync(path.join(os.tmpdir(),'dafeiyu-restore-'));
const browser=spawn(process.env.EDGE_BINARY||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',['--headless=new','--edge-skip-compat-layer-relaunch','--remote-debugging-address=127.0.0.1','--remote-debugging-port=0',`--user-data-dir=${profile}`,'--no-first-run','--no-default-browser-check','--disable-background-networking','--disable-extensions','--disable-sync','about:blank'],{windowsHide:true,stdio:'ignore'});
const report={scope:'Isolated native picture decoding, one pet-only refresh, safe failures and no signed URL output.',checks:[]};
const check=(name,pass,evidence)=>{report.checks.push({name,pass:!!pass,evidence});if(!pass)throw new Error(name);};
let connection,mainConnection,server,fixtureTarget,mainTarget,port,mode='healthy',loads=0,discovery=[];
try{
  const portFile=path.join(profile,'DevToolsActivePort');
  for(let i=0;i<100&&!fs.existsSync(portFile);i++){if(browser.exitCode!==null)throw new Error('Test browser exited');await sleep(100);}
  const debugPort=Number(fs.readFileSync(portFile,'utf8').split('\n')[0]);
  fixtureTarget=(await listTargets(debugPort)).find(t=>t.type==='page');
  connection=await CdpConnection.connect(fixtureTarget.webSocketDebuggerUrl);
  await connection.send('Page.enable');
  const mainId=(await connection.send('Target.createTarget',{url:'about:blank'})).targetId;
  mainTarget=(await listTargets(debugPort)).find(t=>t.id===mainId);
  mainConnection=await CdpConnection.connect(mainTarget.webSocketDebuggerUrl);
  await mainConnection.evaluate('globalThis.__mainMarker="untouched";true');
  server=http.createServer((request,response)=>{
    response.setHeader('Cache-Control','no-store');
    if(request.url==='/json/list'){response.setHeader('Content-Type','application/json');response.end(JSON.stringify(discovery));return;}
    if(request.url.startsWith('/expired.png')){response.writeHead(403);response.end();return;}
    if(request.url.startsWith('/good.png')){response.setHeader('Content-Type','image/png');response.end(Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a1ioAAAAASUVORK5CYII=','base64'));return;}
    if(request.url==='/pet'){
      loads++;
      const broken=mode==='broken'||mode==='refresh'&&loads===1;
      const image=mode==='css'?'linear-gradient(red,blue)':`url("/${broken?'expired':'good'}.png?sig=TEST-PRIVATE-SIGNED-URL")`;
      response.setHeader('Content-Type','text/html; charset=utf-8');
      response.end(`<html><style>.codex-avatar-root{width:120px;height:140px;background-image:${image}}</style><body>${mode==='missing'?'':`<div class="codex-avatar-root" data-codex-pet-id="fixture" data-codex-pet-state="idle"></div>`}</body></html>`);return;
    }
    response.writeHead(404);response.end();
  });
  await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));port=server.address().port;
  discovery=[{...mainTarget,url:'app://-/index.html'},{...fixtureTarget,url:'app://-/index.html?initialRoute=%2Favatar-overlay'}];
  async function navigate(next){mode=next;loads=0;await connection.send('Page.navigate',{url:`http://127.0.0.1:${port}/pet`});for(let i=0;i<50;i++){try{if(await connection.evaluate('document.readyState==="complete"&&location.pathname==="/pet"'))return;}catch{}await sleep(50);}throw new Error('Fixture not ready');}
  await navigate('healthy');
  await connection.evaluate('globalThis.__disposed=false;globalThis.__codexLive2D={restore(){globalThis.__disposed=true},status(){return {active:!globalThis.__disposed}}};true');
  let result=await restoreNativePets(port);
  check('healthy_native_picture_is_decoded_without_refresh',result.restored&&result.results[0].visible&&!result.results[0].reloaded&&loads===1,result);
  check('adapter_is_disposed_before_verifying_native_picture',await connection.evaluate('globalThis.__disposed===true'));
  await navigate('refresh');result=await restoreNativePets(port);
  check('expired_picture_refreshes_pet_once_and_recovers',result.restored&&result.results[0].reloaded&&loads===2,result);
  check('chat_page_is_never_reloaded',await mainConnection.evaluate('globalThis.__mainMarker==="untouched"'));
  check('same_browser_remains_running',browser.exitCode===null);
  check('restore_output_does_not_expose_signed_urls',!JSON.stringify(result).includes('TEST-PRIVATE')&&!JSON.stringify(result).includes('sig='));
  await navigate('broken');result=await restoreNativePets(port);
  check('permanent_image_failure_is_not_reported_as_success',!result.restored&&result.reason==='native-picture-unavailable',result);
  check('failed_image_has_only_one_refresh_no_loop',loads===2,{loads});
  await navigate('css');result=await restoreNativePets(port);
  check('built_in_css_picture_does_not_refresh',result.restored&&!result.results[0].reloaded&&loads===1);
  discovery=[{...mainTarget,url:'app://-/index.html'}];result=await restoreNativePets(port);
  check('closed_pet_is_a_safe_noop',result.restored&&result.reason==='pet-window-closed');
  discovery=[{...fixtureTarget,url:'app://-/index.html?initialRoute=%2Favatar-overlay'}];
  // A failed dispose must not provoke a reload/reinstall race.
  await connection.evaluate('globalThis.__codexLive2D={restore(){},status(){return {active:true}}};true');result=await restoreNativePets(port);
  check('still_active_adapter_prevents_page_reload',!result.restored&&result.reason==='live2d-still-active'&&loads===1);
  report.pass=true;
}catch(error){report.pass=false;report.failure=error.stack;process.exitCode=1;}
finally{
  mainConnection?.close();
  if(connection){try{await connection.send('Browser.close');}catch{}connection.close();}
  if(browser.exitCode===null)browser.kill();
  if(server)await new Promise(resolve=>server.close(resolve));
  fs.writeFileSync(path.join(artifacts,'restoration-report.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));
}
