// Session launcher for the installed official Codex. Never edits its files.
import fs from 'node:fs';
import path from 'node:path';
import net from 'node:net';
import {spawn,execFileSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {packageRoot,defaultPetId,verifyAssets,installOnConnection} from './cli.mjs';
import {CdpConnection,listTargets,isNativePetTarget} from './cdp.mjs';
import {dataDir,sessionDir,readSettings,writeSettings} from './settings.mjs';
import {readCore,hasCore} from './core-runtime.mjs';
import {readAppearance} from './appearance.mjs';
import {readPreferences} from './preferences.mjs';
import {createDragCompatibility,createVisibilityBridge} from './drag-compatibility.mjs';
import readline from 'node:readline';
import {selectCodex,superviseCodex} from './codex-lifecycle.mjs';
import {restoreNativePets} from './restore.mjs';
const stateFile=path.join(sessionDir,'controller.json');
const stopFile=path.join(sessionDir,'stop-request');
const lockFile=path.join(sessionDir,'controller.lock');
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));

export function discoverCodex() {
  const script=`$ErrorActionPreference='Stop'; $pkg=Get-AppxPackage -Name 'OpenAI.Codex' | Select-Object -First 1; $apps=@(Get-CimInstance Win32_Process -Filter "Name = 'ChatGPT.exe' OR Name = 'Codex.exe'" | Where-Object { $_.CommandLine -notmatch '--type=' -and $_.CommandLine -notmatch 'live2d-codex-profiles' -and $_.ExecutablePath -match '(?i)[\\\\/]app[\\\\/](ChatGPT|Codex)\\.exe$' -and ($null -eq $pkg -or $_.ExecutablePath.StartsWith($pkg.InstallLocation,[StringComparison]::OrdinalIgnoreCase)) } | ForEach-Object { $m=[regex]::Match($_.CommandLine,'--remote-debugging-port(?:=|\\s+)(\\d+)'); [pscustomobject]@{pid=$_.ProcessId;executable=$_.ExecutablePath;port=$(if($m.Success){[int]$m.Groups[1].Value}else{$null})} }); [pscustomobject]@{packagePath=$pkg.InstallLocation;packageVersion=[string]$pkg.Version;running=$apps} | ConvertTo-Json -Depth 4 -Compress`;
  const raw=execFileSync('powershell.exe',['-NoProfile','-NonInteractive','-Command',script],{encoding:'utf8',windowsHide:true,timeout:15000});
  const found=JSON.parse(raw.replace(/^\uFEFF/,''));
  if(found.packagePath)found.executable=path.join(found.packagePath,'app','ChatGPT.exe');
  else found.executable=found.running[0]?.executable??null;
  return found;
}

export function preflight() {
  const found=discoverCodex();
  if(selectCodex(found).kind==='needs-relaunch')return {ready:false,reason:'running-without-debug-port',message:'当前 Codex 未开启宠物接入通道。请完整退出一次，保持助手运行，由助手接续打开；无需手动再点 Codex。',found};
  if(!found.executable||!fs.existsSync(found.executable))return {ready:false,reason:'codex-not-found',message:'未找到已安装的官方 Codex。',found};
  return {ready:true,found};
}

function writeState(state){fs.mkdirSync(sessionDir,{recursive:true});const temporary=stateFile+'.'+process.pid+'.tmp';fs.writeFileSync(temporary,JSON.stringify(state,null,2));fs.renameSync(temporary,stateFile);}
function log(message){fs.appendFileSync(path.join(sessionDir,'controller.log'),new Date().toISOString()+' '+message+'\n');}
function ownLock() {
  fs.mkdirSync(sessionDir,{recursive:true});
  if(fs.existsSync(lockFile)){
    const pid=Number(fs.readFileSync(lockFile,'utf8'));let alive=false;
    try{process.kill(pid,0);alive=true;}catch{}
    if(alive)throw new Error('Live2D 控制器已经运行。');
    fs.unlinkSync(lockFile);
  }
  const fd=fs.openSync(lockFile,'wx');fs.writeFileSync(fd,String(process.pid));fs.closeSync(fd);
}
function releaseLock(){try{if(fs.readFileSync(lockFile,'utf8')===String(process.pid))fs.unlinkSync(lockFile);}catch{}}
async function freePort(){return new Promise((resolve,reject)=>{const server=net.createServer();server.once('error',reject);server.listen(0,'127.0.0.1',()=>{const port=server.address().port;server.close(()=>resolve(port));});});}
function nativeEnvironment(){
  const env={...process.env};
  for(const key of Object.keys(env))if((key.startsWith('CODEX_')&&!['CODEX_HOME','CODEX_SQLITE_HOME','CODEX_ELECTRON_USER_DATA_PATH'].includes(key))||['ELECTRON_RUN_AS_NODE','NODE_OPTIONS'].includes(key))delete env[key];
  return env;
}

// Use the app's own open-state query before its toggle action. This only sends
// UI messages defined in the local client; it never reads the chat DOM.
export async function requestPetWindow(port){
  const targets=await listTargets(port);
  const main=targets.find(target=>{
    if(target.type!=='page'||isNativePetTarget(target))return false;
    try{const url=new URL(target.url);return url.protocol==='app:'&&url.hostname==='-'&&url.pathname==='/index.html'&&!url.searchParams.has('initialRoute');}catch{return false;}
  });
  if(!main)return {requested:false,reason:'main-window-not-ready'};
  const connection=await CdpConnection.connect(main.webSocketDebuggerUrl);
  try{return await connection.evaluate(`(async()=>{
    const bridge=globalThis.electronBridge;
    if(typeof bridge?.sendMessageFromView!=='function')return {requested:false,reason:'bridge-not-ready'};
    let handler,timer;
    const openState=new Promise(resolve=>{
      handler=event=>{if(event.data?.type==='avatar-overlay-open-state-changed'&&typeof event.data.isOpen==='boolean')resolve(event.data.isOpen);};
      window.addEventListener('message',handler);
      timer=setTimeout(()=>resolve(null),2000);
    });
    try{
      await bridge.sendMessageFromView({type:'avatar-overlay-open-state-request'});
      const isOpen=await openState;
      if(isOpen===null)return {requested:false,reason:'open-state-unavailable'};
      if(isOpen)return {requested:false,alreadyOpen:true};
      await bridge.sendMessageFromView({type:'avatar-overlay-open'});
      return {requested:true};
    }finally{clearTimeout(timer);window.removeEventListener('message',handler);}
  })()`);}finally{connection.close();}
}

function processStillAlive(pid){try{process.kill(pid,0);return true;}catch(error){return error.code!=='ESRCH';}}


export async function start({port:requestedPort,fps=60,petId=defaultPetId,waitForExit=false,waitTimeoutMs=3600000,openPet=false,watchCodex=false,nativeHelper=false}={}){
  if(![30,60].includes(fps))throw new Error('Choose 30 or 60 FPS');
  const manifest=verifyAssets();readCore();ownLock();
  let stopping=false,reconnectRequested=false,wakeWait=null,currentCompatibility=null,latestState=null;
  let restoration={restored:true,reason:'not-attached',results:[]};
  const base={controllerPid:process.pid,petId,bindingMode:petId===null?'follow-current':'fixed',fps,watchCodex};
  const shouldStop=()=>stopping||fs.existsSync(stopFile);
  const wake=()=>{wakeWait?.();};
  const wait=ms=>new Promise(resolve=>{let timer;const finish=()=>{clearTimeout(timer);if(wakeWait===finish)wakeWait=null;resolve();};timer=setTimeout(finish,ms);wakeWait=finish;});
  const input=readline.createInterface({input:process.stdin});
  input.on('line',line=>{
    if(line.length>1024)return;
    try{
      const message=JSON.parse(line);
      if(message.type==='reconnect'&&Object.keys(message).length===1){reconnectRequested=true;wake();return;}
      if(message.type==='window-drag'&&typeof message.moving==='boolean'&&Object.keys(message).every(k=>['type','moving','nativeFastHideCount'].includes(k))){
        if(latestState&&Number.isInteger(message.nativeFastHideCount)&&message.nativeFastHideCount>=0)latestState.nativeFastHideCount=message.nativeFastHideCount;
        void currentCompatibility?.setMoving(message.moving);
      }
    }catch{}
  });
  input.on('close',()=>{void currentCompatibility?.setMoving(false);if(nativeHelper){stopping=true;wake();}});
  try{
    if(fs.existsSync(stopFile))fs.unlinkSync(stopFile);
    if(!requestedPort&&!waitForExit){const check=preflight();if(!check.ready)throw new Error(check.message);}
    let lastLifecycle='';
    const onState=value=>{
      latestState={...base,...value,appPid:null,port:null,updatedAt:new Date().toISOString()};writeState(latestState);
      const identity=JSON.stringify(value);if(identity!==lastLifecycle){log('Connection lifecycle: '+identity);lastLifecycle=identity;}
    };
    const launchOfficial=async found=>{
      const port=await freePort();
      const child=spawn(found.executable,['--remote-debugging-address=127.0.0.1','--remote-debugging-port='+port],{env:nativeEnvironment(),windowsHide:true,detached:true,stdio:'ignore'});
      await new Promise((resolve,reject)=>{child.once('spawn',resolve);child.once('error',reject);});child.unref();
      return {appPid:child.pid,port};
    };
    const runSession=async connectionInfo=>{
      const {appPid,port}=connectionInfo;
      const state={...base,...connectionInfo,status:'waiting-for-pet',startedAt:new Date().toISOString()};latestState=state;
      const visibilityBridge=createVisibilityBridge(port);
      const compatibility=createDragCompatibility(visibilityBridge,{enabled:readPreferences().dragCompatibility,onChange:value=>{state.dragCompatibility=value;writeState(state);}});
      currentCompatibility=compatibility;state.dragCompatibility=compatibility.status();
      writeState(state);log('Connected process='+appPid+'; port='+port+'; petId='+petId+'; fps='+fps);
      const targetsState=new Map();let unreachable=0,openFinished=!openPet,nextOpenAttempt=0;
      const begin=Date.now();
      try{
        while(!shouldStop()){
          if(appPid&&!processStillAlive(appPid)){log('Codex exited; watching for its next launch.');break;}
          if(reconnectRequested){targetsState.clear();reconnectRequested=false;openFinished=!openPet;nextOpenAttempt=0;}
          const preferences=readPreferences();state.earlyAvoidance=preferences.earlyAvoidance;state.avoidanceTrigger=preferences.avoidanceTrigger;
          if(compatibility.status().enabled!==preferences.dragCompatibility)void compatibility.setEnabled(preferences.dragCompatibility);
          if(compatibility.status().temporarilyHidden||compatibility.status().enabled&&compatibility.status().moving){state.dragCompatibility=compatibility.status();state.updatedAt=new Date().toISOString();writeState(state);await sleep(100);continue;}
          let targets;
          try{targets=(await listTargets(port)).filter(isNativePetTarget);unreachable=0;}
          catch(error){
            unreachable++;
            if(unreachable>=3){state.status='reconnecting';state.updatedAt=new Date().toISOString();writeState(state);}
            if(Date.now()-begin>20000&&unreachable>8){log('Codex connection closed; rediscovering the current process.');break;}
            await sleep(700);continue;
          }
          if(!openFinished&&Date.now()-begin>=10000&&Date.now()>=nextOpenAttempt){
            try{
              const result=await requestPetWindow(port);
              if(result.requested||result.alreadyOpen){openFinished=true;log(`Native pet visibility request: ${JSON.stringify(result)}`);}
            }catch(error){log(`Native pet visibility request failed: ${error.message.slice(0,200)}`);}
            nextOpenAttempt=Date.now()+10000;
            if(Date.now()-begin>60000)openFinished=true;
          }
          const ids=new Set(targets.map(t=>t.id));
          for(const id of targetsState.keys())if(!ids.has(id))targetsState.delete(id);
          for(const target of targets){
            const previous=targetsState.get(target.id);
            if(previous?.nextCheck>Date.now()||previous?.attempts>=3)continue;
            const connection=await CdpConnection.connect(target.webSocketDebuggerUrl).catch(()=>null);if(!connection)continue;
            try{
              // Keep auto mode unbound: Codex may briefly render its built-in pet
              // before restoring the signed-in account's selected custom pet.
              const snapshot=await connection.evaluate('({status:globalThis.__codexLive2D?.status?.()??null,vendorBuildId:globalThis.__dshLive2dPetVendor?.buildId??null,selectedPetId:document.querySelector(".codex-avatar-root[data-codex-pet-id][data-codex-pet-state]")?.getAttribute("data-codex-pet-id")??null})');
              let status=snapshot.status;
              if(!status||status.disposed||snapshot.vendorBuildId!==manifest.runtime_build.id||status.rendererBuildId!==manifest.renderer_build){status=await installOnConnection(connection,{petId,fps});log(`Attached to official pet window ${target.id}; active=${status.active}`);}
              const appearance=readAppearance();
              if(JSON.stringify(status.appearance)!==JSON.stringify(appearance))status=await connection.evaluate(`globalThis.__codexLive2D.setAppearance(${JSON.stringify(appearance)})`);
              const waitingForRoot=status.lastError?.includes('pet root not found')||status.lastError?.includes('pet root is not available');
              state.selectedPetId=snapshot.selectedPetId;
              state.status=status.active?'active':waitingForRoot?(petId===null?'waiting-for-pet':'waiting-for-selected-pet'):status.lastError?'waiting-or-failed':'waiting-for-pet';
              state.lastStatus=status;state.updatedAt=new Date().toISOString();writeState(state);
              if(status.active)void visibilityBridge.prepare().catch(()=>{});
              targetsState.set(target.id,{nextCheck:Date.now()+3000,attempts:status.active?0:(previous?.attempts??0)+(status.lastError&&!status.lastError.includes('root not found')?1:0)});
            }catch(error){log(`Pet renderer attach failed: ${error.message.slice(0,400)}`);targetsState.set(target.id,{nextCheck:Date.now()+10000,attempts:(previous?.attempts??0)+1});}
            finally{connection.close();}
          }
          await sleep(700);
        }

      }finally{await compatibility.close();currentCompatibility=null;}
      restoration=appPid&&!processStillAlive(appPid)?{restored:true,reason:'app-exited',results:[]}:await restoreNativePets(port);
      log('Native pet restoration: '+JSON.stringify(restoration));
    };
    if(requestedPort)await runSession({appPid:null,port:requestedPort,version:null,startedOfficialClient:false});
    else await superviseCodex({discover:discoverCodex,launch:launchOfficial,runSession,onState,shouldStop,
      takeLaunchRequest:()=>{const requested=reconnectRequested;reconnectRequested=false;return requested;},wait,
      watch:watchCodex,timeoutMs:watchCodex?Infinity:waitTimeoutMs});
    if(latestState?.port===null)restoration={restored:true,reason:'not-attached',results:[]};
    latestState={...(latestState??base),restoration,status:'stopped',stoppedAt:new Date().toISOString()};writeState(latestState);
  }finally{stopping=true;input.close();releaseLock();}
}

async function main(){
  const args=process.argv.slice(2),command=args.shift()??'preflight';
  const value=(flag,fallback)=>{const i=args.indexOf(flag);return i>=0?args[i+1]:fallback;};
  if(command==='preflight'){const result=preflight();console.log(JSON.stringify(result,null,2));process.exitCode=result.ready?0:2;return;}
  if(command==='start'){const settings=readSettings();await start({port:value('--port',null)==null?null:Number(value('--port')),fps:Number(value('--fps',String(settings.fps))),petId:value('--pet-id',settings.petId),waitForExit:args.includes('--wait-for-exit'),openPet:args.includes('--open-pet'),watchCodex:args.includes('--watch-codex'),nativeHelper:args.includes('--native-helper')});return;}
  if(command==='configure'){
    if(fs.existsSync(lockFile)){const pid=Number(fs.readFileSync(lockFile,'utf8'));if(processStillAlive(pid))throw new Error('Restore the original pet before changing the binding or FPS.');}
    const settings=readSettings();
    const id=value('--pet-id',settings.petId);
    console.log(JSON.stringify(writeSettings({petId:id==='auto'?null:id,fps:Number(value('--fps',String(settings.fps)))})));return;
  }
  if(command==='environment'){console.log(JSON.stringify({dataDir,coreReady:hasCore(),settings:readSettings()}));return;}
  if(command==='restore'){
    fs.mkdirSync(sessionDir,{recursive:true});fs.writeFileSync(stopFile,'stop');
    let state=fs.existsSync(stateFile)?JSON.parse(fs.readFileSync(stateFile,'utf8')):null;
    const owner=fs.existsSync(lockFile)?Number(fs.readFileSync(lockFile,'utf8')):null;
    if(owner&&processStillAlive(owner)){
      const deadline=Date.now()+25000;
      // The controller owns restoration while stopping. A second caller must
      // not reload a page that the attach loop is still using.
      while(fs.existsSync(lockFile)&&processStillAlive(owner)&&Date.now()<deadline)await sleep(100);
      if(fs.existsSync(lockFile)&&processStillAlive(owner))throw new Error('控制器仍在停止，请稍后重试恢复。');
      state=fs.existsSync(stateFile)?JSON.parse(fs.readFileSync(stateFile,'utf8')):null;
      if(state?.controllerPid===owner&&state?.status==='stopped'&&state.restoration){console.log(JSON.stringify(state.restoration,null,2));return;}
    }
    const result=state?.port?await restoreNativePets(state.port):{restored:true,reason:'not-started',results:[]};
    if(state){state.restoration=result;writeState(state);}
    console.log(JSON.stringify(result,null,2));return;
  }
  if(command==='status'){console.log(fs.existsSync(stateFile)?fs.readFileSync(stateFile,'utf8'):'{"status":"not-started"}');return;}
  throw new Error('Commands: preflight, start, status, restore, configure, environment');
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url))main().catch(error=>{fs.mkdirSync(sessionDir,{recursive:true});log(error.message);console.error(error.message);process.exitCode=1;});
