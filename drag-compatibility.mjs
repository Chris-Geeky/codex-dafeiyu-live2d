import {CdpConnection,listTargets,isNativePetTarget} from './cdp.mjs';
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
export function createDragCompatibility(bridge,{enabled=true,onChange=()=>{}}={}){
  let moving=false,owned=false,closing=false,operation=null,lastError=null,hiddenCount=0,restoredCount=0;
  const status=()=>({enabled,moving,temporarilyHidden:owned,hiddenCount,restoredCount,lastError});
  function pump(){
    if(operation)return operation;
    operation=(async()=>{
      for(;;){
        const hide=enabled&&moving&&!closing;
        if(hide&&!owned){
          if(!await bridge.isOpen())break;
          if(!enabled||!moving||closing)break;
          owned=true;onChange(status());await bridge.hide();hiddenCount++;lastError=null;
        }else if(!hide&&owned){await bridge.show();owned=false;restoredCount++;lastError=null;}
        else break;
      }
    })().catch(()=>{lastError='pet-visibility-operation-failed';}).finally(()=>{operation=null;onChange(status());});
    return operation;
  }
  return {status,setMoving(value){moving=value===true;return pump();},setEnabled(value){enabled=value===true;return pump();},settled(){return operation??Promise.resolve();},async close(){closing=true;moving=false;await pump();if(owned)await pump();await bridge.close?.();}};
}

export function createVisibilityBridge(port){
  let main=null,pet=null,pendingMain=null,pendingPet=null;
  async function mainConnection(){
    if(main&&main.socket.readyState===1)return main;
    if(pendingMain)return pendingMain;
    pendingMain=(async()=>{const target=(await listTargets(port)).find(target=>{try{const u=new URL(target.url);return target.type==='page'&&!isNativePetTarget(target)&&u.protocol==='app:'&&u.hostname==='-'&&u.pathname==='/index.html'&&!u.searchParams.has('initialRoute')}catch{return false}});if(!target)throw new Error('Main window unavailable');main=await CdpConnection.connect(target.webSocketDebuggerUrl);return main;})();
    try{return await pendingMain}finally{pendingMain=null;}
  }
  async function isOpen(){
    const connection=await mainConnection();
    const value=await connection.evaluate(`(async()=>{let handler,timer;const state=new Promise(resolve=>{handler=e=>{if(e.data?.type==='avatar-overlay-open-state-changed'&&typeof e.data.isOpen==='boolean')resolve(e.data.isOpen)};window.addEventListener('message',handler);timer=setTimeout(()=>resolve(null),900)});try{await globalThis.electronBridge.sendMessageFromView({type:'avatar-overlay-open-state-request'});return await state}finally{clearTimeout(timer);window.removeEventListener('message',handler)}})()`);
    if(typeof value!=='boolean')throw new Error('Pet visibility unavailable');return value;
  }
  async function petConnection(){if(pet&&pet.socket.readyState===1)return pet;if(pendingPet)return pendingPet;pendingPet=(async()=>{const target=(await listTargets(port)).find(isNativePetTarget);if(!target)throw new Error('Pet window unavailable');pet=await CdpConnection.connect(target.webSocketDebuggerUrl);return pet;})();try{return await pendingPet}finally{pendingPet=null;}}
  async function waitFor(value){for(let i=0;i<8;i++){if(await isOpen()===value)return;await sleep(35);}throw new Error('Visibility change not confirmed');}
  return {
    isOpen,
    async prepare(){await Promise.all([mainConnection(),petConnection()]);},
    async hide(){
      const connection=await petConnection();
      try{await connection.evaluate(`globalThis.electronBridge.sendMessageFromView({type:'avatar-overlay-close'});true`);}catch(error){if(!error.message.includes('closed'))throw error;}
      await waitFor(false);
    },
    async show(){if(!await isOpen()){const connection=await mainConnection();await connection.evaluate(`globalThis.electronBridge.sendMessageFromView({type:'avatar-overlay-open'});true`);}await waitFor(true);},
    async close(){await Promise.allSettled([pendingMain,pendingPet].filter(Boolean));main?.close();pet?.close();main=pet=null;}
  };
}
