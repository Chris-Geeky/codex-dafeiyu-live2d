// Process discovery and reconnection policy. Never closes an application.
export function selectCodex(found,{allowLaunch=true}={}) {
  const running=Array.isArray(found.running)?found.running:[];
  const app=running.find(app=>Number.isInteger(app.port)&&app.port>=1024&&app.port<=65535);
  if(app)return {kind:'attach',app,version:found.packageVersion??null};
  if(running.length)return {kind:'needs-relaunch',pids:running.map(app=>app.pid)};
  if(!found.executable)return {kind:'not-installed'};
  return {kind:allowLaunch?'launch':'wait'};
}

export async function acquireCodex({discover,launch,onState=()=>{},shouldStop=()=>false,takeLaunchRequest=()=>false,
  wait=ms=>new Promise(resolve=>setTimeout(resolve,ms)),allowLaunch=true,timeoutMs=Infinity,now=Date.now,pollMs=1500}={}) {
  const expires=now()+timeoutMs;let previousPids='',reopened=false;
  while(!shouldStop()) {
    if(takeLaunchRequest())allowLaunch=true;
    const found=await discover();if(shouldStop())return null;
    const choice=selectCodex(found,{allowLaunch});
    if(choice.kind==='attach')return {appPid:choice.app.pid,port:choice.app.port,version:choice.version,startedOfficialClient:false};
    if(choice.kind==='launch') {
      // Recheck cancellation after discovery, before any new process starts.
      if(shouldStop())return null;
      return {...await launch(found),version:found.packageVersion??null,startedOfficialClient:true};
    }
    if(choice.kind==='not-installed')throw new Error('未找到已安装的官方 Codex。');
    if(choice.kind==='needs-relaunch') {
      const pids=choice.pids.join(',');if(previousPids&&previousPids!==pids)reopened=true;previousPids=pids;
      onState({status:'waiting-for-codex-exit',waitingPids:choice.pids,autoRelaunch:allowLaunch,
        waitingReason:reopened?'reopened-without-connection':'connection-not-enabled'});
    }else onState({status:'watching-for-codex',waitingPids:[],autoRelaunch:false,waitingReason:'app-not-running'});
    if(now()>=expires){onState({status:'expired',waitingPids:[]});return null;}
    await wait(pollMs);
  }
  return null;
}

export async function superviseCodex({runSession,watch=false,wait=ms=>new Promise(resolve=>setTimeout(resolve,ms)),...options}={}) {
  let allowLaunch=true;
  do {
    const connection=await acquireCodex({...options,wait,allowLaunch});
    if(!connection)return;
    await runSession(connection);
    if(!watch||options.shouldStop?.())return;
    // A deliberate app exit leaves Codex closed. Only an explicit launch request
    // from the helper may reopen it; a compatible manual restart is reattached.
    allowLaunch=false;
    await wait(500);
  }while(!options.shouldStop?.());
}
