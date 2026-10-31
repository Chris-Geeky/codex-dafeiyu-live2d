// Restore only the native pet renderer. Never reload the Codex chat window or
// return signed image URLs in command output, status files, or logs.
import {CdpConnection,listTargets,isNativePetTarget} from './cdp.mjs';

const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));

export async function inspectNativePicture(timeoutMs=1800) {
  const root=document.querySelector('.codex-avatar-root[data-codex-pet-id][data-codex-pet-state]');
  if(!root)return {visible:false,reason:'pet-root-not-ready'};
  if(globalThis.__codexLive2D?.status?.().active)return {visible:false,reason:'live2d-still-active'};
  const style=getComputedStyle(root),rect=root.getBoundingClientRect();
  if(style.display==='none'||style.visibility==='hidden'||Number(style.opacity)===0||rect.width<=0||rect.height<=0)return {visible:false,reason:'pet-root-hidden'};
  const background=style.backgroundImage;
  if(!background||background==='none')return {visible:false,reason:'native-picture-missing'};
  const urls=[...background.matchAll(/url\(\s*(?:"((?:\\.|[^"\\])*)"|'((?:\\.|[^'\\])*)'|([^)]*))\s*\)/g)].map(match=>(match[1]??match[2]??match[3]).replace(/\\(["'\\])/g,'$1').trim());
  if(!urls.length)return {visible:true,reason:'native-css-picture'};
  const loaded=await Promise.all(urls.map(src=>new Promise(resolve=>{
    const image=new Image();let timer;
    const finish=value=>{clearTimeout(timer);image.onload=image.onerror=null;resolve(value);};
    timer=setTimeout(()=>finish(false),timeoutMs);
    image.onload=()=>finish(image.naturalWidth>0&&image.naturalHeight>0);
    image.onerror=()=>finish(false);image.src=src;
  })));
  return {visible:loaded.every(Boolean),reason:loaded.every(Boolean)?'native-picture-ready':'native-picture-unavailable'};
}

export async function restoreNativeConnection(connection,{reloadTimeoutMs=8000}={}) {
  await connection.evaluate('globalThis.__codexLive2D?.restore?.();true');
  let picture=await connection.evaluate(`(${inspectNativePicture.toString()})()`);
  if(picture.visible)return {restored:true,reloaded:false,...picture};
  // A still-running adapter must be stopped before a page reload can help.
  if(picture.reason==='live2d-still-active')return {restored:false,reloaded:false,...picture};
  await connection.send('Page.reload',{ignoreCache:true});
  const deadline=Date.now()+reloadTimeoutMs;
  do {
    await sleep(250);
    try{picture=await connection.evaluate(`(${inspectNativePicture.toString()})(1200)`);}catch{picture={visible:false,reason:'pet-page-loading'};}
    if(picture.visible)return {restored:true,reloaded:true,...picture};
    // A completed decode failure is conclusive; do not keep re-requesting the
    // same expired URL or repeatedly refreshing a broken cloud resource.
    if(picture.reason==='native-picture-unavailable'||picture.reason==='live2d-still-active')break;
  }while(Date.now()<deadline);
  return {restored:false,reloaded:true,...picture};
}

export async function restoreNativePets(port) {
  let targets;
  try{targets=(await listTargets(port)).filter(isNativePetTarget);}catch{
    return {restored:false,reason:'codex-endpoint-unavailable',results:[]};
  }
  const results=[];
  for(const target of targets){
    let connection;
    try{connection=await CdpConnection.connect(target.webSocketDebuggerUrl);results.push(await restoreNativeConnection(connection));}
    catch{results.push({restored:false,reloaded:false,visible:false,reason:'pet-connection-unavailable'});}
    finally{connection?.close();}
  }
  return {restored:results.every(result=>result.restored),reason:results.find(result=>!result.restored)?.reason??(targets.length?'checked':'pet-window-closed'),results};
}
