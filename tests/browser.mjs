import './isolated-data.mjs';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {spawn} from 'node:child_process';
import http from 'node:http';
import {sessionDir,readSettings,writeSettings} from '../settings.mjs';
import {CdpConnection,listTargets} from '../cdp.mjs';
import {installOnConnection,packageRoot} from '../cli.mjs';
const artifacts=path.join(packageRoot,'tests/.artifacts');fs.mkdirSync(artifacts,{recursive:true});
const profile=fs.mkdtempSync(path.join(os.tmpdir(),'dafeiyu-browser-'));
const fixturePetId='browser-fixture';
const browser=spawn(process.env.EDGE_BINARY||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',['--headless=new','--edge-skip-compat-layer-relaunch','--remote-debugging-address=127.0.0.1','--remote-debugging-port=0',`--user-data-dir=${profile}`,'--no-first-run','--no-default-browser-check','--disable-background-networking','--disable-extensions','--disable-sync','about:blank'],{windowsHide:true,stdio:['ignore','ignore','pipe']});
let diagnostic='';browser.stderr.on('data',bytes=>{diagnostic=(diagnostic+bytes.toString()).slice(-3000);});
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const report={scope:'Native rectangular hit-region contract with alpha-derived body regions, transparent padding, original event bubbling, separate controls and restore.',checks:[]};
const check=(name,pass,evidence)=>{report.checks.push({name,pass:!!pass,evidence});if(!pass)throw new Error(name);};
let c,proxy,controller;
try{
  const portFile=path.join(profile,'DevToolsActivePort');for(let i=0;i<100&&!fs.existsSync(portFile);i++){if(browser.exitCode!==null&&browser.exitCode!==0)throw new Error('Browser exited '+browser.exitCode+': '+diagnostic);await sleep(100);}
  c=await CdpConnection.connect((await listTargets(Number(fs.readFileSync(portFile,'utf8').split('\n')[0]))).find(t=>t.type==='page').webSocketDebuggerUrl);
  await c.send('Page.enable');await c.send('Runtime.enable');
  await c.send('Emulation.setDeviceMetricsOverride',{width:900,height:900,deviceScaleFactor:1,mobile:false});
  const frame=(await c.send('Page.getFrameTree')).frameTree.frame.id;
  await c.send('Page.setDocumentContent',{frameId:frame,html:`<html><head><meta http-equiv="Content-Security-Policy" content="default-src 'none'; script-src 'self' 'wasm-unsafe-eval'; style-src 'unsafe-inline'; img-src blob: data:; connect-src blob:"><style>body{margin:0}.group{position:absolute;left:150px;top:160px;width:400px;height:434px}.codex-avatar-root{position:relative;width:400px;height:434px;background-image:linear-gradient(red,blue)}</style></head><body><div class="group" id="pet-group"><div id="native-pet" data-avatar-overlay-hit-region="mascot" data-avatar-mascot="true" data-testid="avatar-mascot-button" style="width:100%;height:100%"><div class="codex-avatar-root" data-codex-pet-id="${fixturePetId}" data-codex-pet-state="idle"></div></div></div><button id="control" data-avatar-overlay-hit-region="realtime-voice" style="position:absolute;left:320px;top:700px;width:60px;height:30px">Control</button></body></html>`});
  await c.evaluate(`window.__petClicks=0;window.__controlClicks=0;document.querySelector('#native-pet').addEventListener('pointerdown',()=>window.__petClicks++);document.querySelector('#control').addEventListener('pointerdown',()=>window.__controlClicks++);window.__nativeHit=(x,y)=>[...document.querySelectorAll('[data-avatar-overlay-hit-region]')].some(e=>{const s=getComputedStyle(e),r=e.getBoundingClientRect();return s.pointerEvents!=='none'&&s.display!=='none'&&s.visibility!=='hidden'&&x>=r.left&&x<=r.right&&y>=r.top&&y<=r.bottom&&document.elementsFromPoint(x,y).some(t=>t===e||e.contains(t));});`);
  const csp=await c.send('Runtime.evaluate',{expression:`(()=>{try{new Function('return 1')();return true;}catch{return false;}})()`,returnByValue:true,allowUnsafeEvalBlockedByCSP:false});
  check('strict_csp_blocks_dynamic_functions',csp.result?.value===false);
  await installOnConnection(c,{fps:60,petId:fixturePetId});
  const state=await c.evaluate('globalThis.__codexLive2D.status()');
  await sleep(1200);
  const moving=await c.evaluate('globalThis.__codexLive2D.status()');
  check('real_model_continuously_renders',moving.frames-state.frames>20&&!moving.lastError,{frames:moving.frames-state.frames});
  await c.evaluate('globalThis.__codexLive2D.playMotion("Hammer")');await sleep(500);
  const hammer=await c.evaluate('globalThis.__codexLive2D.status()');
  check('hammer_preserves_character_scale',hammer.modelScale===moving.modelScale&&hammer.sizeMultiplier===1.06);
  check('alpha_mask_loaded',state.active&&state.hitArea.regions>5&&!state.hitArea.error,state.hitArea);
  await c.evaluate('globalThis.__codexLive2D.setAppearance({expression:"star-eyes",autoExpressions:false,accessories:{glasses:"glasses-round",sticker:"sticker-cat",tablecloth:"dark-tablecloth"}})');
  await sleep(800);
  const dressed=await c.evaluate('globalThis.__codexLive2D.status()');
  check('face_and_multiple_accessory_groups_compose',dressed.expression==='星星眼'&&dressed.appearanceParameters.ParamCheek70===1&&dressed.appearanceParameters.ParamCheek83===1&&dressed.appearanceParameters.cc2===1,dressed.appearanceParameters);
  await c.evaluate('globalThis.__codexLive2D.setState("waiting")');await sleep(400);
  check('manual_face_survives_native_state_changes',await c.evaluate('globalThis.__codexLive2D.status().expression==="星星眼"'));
  await c.evaluate('globalThis.__codexLive2D.setAppearance({accessories:{glasses:"glasses-square"}})');await sleep(800);
  const switched=await c.evaluate('globalThis.__codexLive2D.status()');
  check('switching_glasses_replaces_the_previous_pair',switched.appearanceParameters.ParamCheek72===1&&!('ParamCheek70' in switched.appearanceParameters)&&!('ParamCheek83' in switched.appearanceParameters),switched.appearanceParameters);
  await sleep(600);
  check('accessory_values_do_not_accumulate_per_frame',await c.evaluate('globalThis.__codexLive2D.status().appearanceParameters.ParamCheek72===1'));
  check('invalid_appearance_does_not_replace_valid_selection',await c.evaluate('(()=>{try{globalThis.__codexLive2D.setAppearance({accessories:{glasses:"invalid"}})}catch{}return globalThis.__codexLive2D.status().appearance.accessories.glasses==="glasses-square"})()'));
  await c.evaluate('globalThis.__codexLive2D.setAppearance({});globalThis.__codexLive2D.setState("idle")');await sleep(800);
  check('reset_clears_accessories_and_restores_automatic_faces',await c.evaluate('(()=>{const s=globalThis.__codexLive2D.status();return s.appearance.expression==="auto"&&s.appearance.autoExpressions&&Object.values(s.appearance.accessories).every(v=>v==="none")&&Object.keys(s.appearanceParameters).length===0})()'));
  const geometry=await c.evaluate(`(()=>{const root=document.querySelector('.codex-avatar-root').getBoundingClientRect();const areas=[...document.querySelectorAll('[data-codex-live2d-hit-region]')].map(e=>e.getBoundingClientRect());const r=areas[Math.floor(areas.length/2)];return {visible:{x:r.x+r.width/2,y:r.y+r.height/2},top:{x:root.x+root.width/2,y:root.y+2},bottom:{x:root.x+root.width/2,y:root.bottom-2},padding:{x:root.x-20,y:root.y-20},root:root.toJSON(),area:areas.reduce((sum,r)=>sum+r.width*r.height,0),nativePE:getComputedStyle(document.querySelector('#native-pet')).pointerEvents,controlPE:getComputedStyle(document.querySelector('#control')).pointerEvents};})()`);
  const hit=point=>c.evaluate(`window.__nativeHit(${point.x},${point.y})`);
  check('visible_body_is_interactive',await hit(geometry.visible),geometry.visible);
  check('transparent_top_is_click_through',!await hit(geometry.top),geometry.top);
  check('transparent_bottom_is_click_through',!await hit(geometry.bottom),geometry.bottom);
  check('expanded_drawing_padding_is_click_through',!await hit(geometry.padding),geometry.padding);
  check('whole_sprite_rectangle_is_disabled',geometry.nativePE==='none');
  check('hit_area_is_smaller_than_sprite',geometry.area/(geometry.root.width*geometry.root.height)<0.7,{ratio:geometry.area/(geometry.root.width*geometry.root.height)});
  async function click(point){await c.send('Input.dispatchMouseEvent',{type:'mouseMoved',...point});await c.send('Input.dispatchMouseEvent',{type:'mousePressed',button:'left',clickCount:1,...point});await c.send('Input.dispatchMouseEvent',{type:'mouseReleased',button:'left',clickCount:1,...point});}
  await click(geometry.visible);await click(geometry.top);await click(geometry.bottom);
  check('visible_body_events_bubble_to_original_handler',await c.evaluate('window.__petClicks')===1);
  await c.send('Input.dispatchMouseEvent',{type:'mouseMoved',...geometry.top});await sleep(100);
  check('transparent_margin_does_not_hover_pet_group',await c.evaluate(`!document.querySelector('#pet-group').matches(':hover')`));
  await click({x:350,y:715});
  check('original_control_remains_interactive',geometry.controlPE==='auto'&&await c.evaluate('window.__controlClicks')===1);
  await c.evaluate(`window.__nearMessages=[];window.addEventListener('message',event=>{if(event.data?.type==='avatar-overlay-action'&&event.data.action?.type==='pet-pointer-proximity-changed')window.__nearMessages.push(event.data.action.isNear);});`);
  await c.send('Input.dispatchMouseEvent',{type:'mouseMoved',...geometry.top});await sleep(260);
  await c.evaluate(`window.dispatchEvent(new MessageEvent('message',{data:{type:'avatar-overlay-action',action:{type:'pet-pointer-proximity-changed',isNear:true}}}))`);
  check('native_proximity_does_not_open_controls_in_blank_margin',await c.evaluate(`!globalThis.__codexLive2D.status().controlsHover.expanded&&!window.__nearMessages.includes(true)`));
  await c.send('Input.dispatchMouseEvent',{type:'mouseMoved',...geometry.visible});
  check('artwork_hover_opens_controls',await c.evaluate(`globalThis.__codexLive2D.status().controlsHover.expanded&&window.__nearMessages.at(-1)===true`));
  await c.send('Input.dispatchMouseEvent',{type:'mouseMoved',x:350,y:715});await sleep(260);
  check('moving_to_real_control_keeps_controls_open',await c.evaluate(`globalThis.__codexLive2D.status().controlsHover.expanded`));
  await c.send('Input.dispatchMouseEvent',{type:'mouseMoved',...geometry.padding});await sleep(260);
  check('leaving_artwork_and_controls_collapses_controls',await c.evaluate(`!globalThis.__codexLive2D.status().controlsHover.expanded&&window.__nearMessages.at(-1)===false`));
  await c.evaluate('globalThis.__codexLive2D.restore()');
  check('restore_removes_hit_overrides',await c.evaluate(`document.querySelectorAll('[data-codex-live2d-pointer-host],[data-codex-live2d-hit-region]').length===0&&getComputedStyle(document.querySelector('#native-pet')).pointerEvents==='auto'`));
  // Expose this isolated browser fixture under the inspected native URL only
  // in a test-local discovery server. Never discover or launch daily Codex.
  const actual=(await listTargets(Number(fs.readFileSync(path.join(profile,'DevToolsActivePort'),'utf8').split('\n')[0]))).find(target=>target.type==='page');
  proxy=http.createServer((request,response)=>{response.setHeader('Content-Type','application/json');response.end(JSON.stringify([{...actual,url:'app://-/index.html?initialRoute=%2Favatar-overlay'}]));});
  await new Promise(resolve=>proxy.listen(0,'127.0.0.1',resolve));
  writeSettings({petId:null,fps:60});
  await c.evaluate('document.querySelector(".codex-avatar-root").setAttribute("data-codex-pet-id","codex")');
  controller=spawn(process.execPath,[path.join(packageRoot,'launch.mjs'),'start','--port',String(proxy.address().port)],{env:process.env,windowsHide:true,stdio:'ignore'});
  let live;
  for(let i=0;i<100;i++){
    try{live=JSON.parse(fs.readFileSync(path.join(sessionDir,'controller.json'),'utf8'));}catch{}
    if(live?.status==='active')break;
    if(controller.exitCode!==null)throw new Error('Test controller exited before attaching');
    await sleep(100);
  }
  check('auto_mode_does_not_persist_the_startup_fallback',live?.status==='active'&&live.bindingMode==='follow-current'&&readSettings().petId===null);
  async function runAppearance(args,script='appearance.mjs'){
    const child=spawn(process.execPath,[path.join(packageRoot,script),...args],{env:process.env,windowsHide:true,stdio:['ignore','pipe','pipe']});
    let output='',errors='';child.stdout.on('data',bytes=>output+=bytes);child.stderr.on('data',bytes=>errors+=bytes);
    await new Promise((resolve,reject)=>{child.once('error',reject);child.once('exit',code=>code===0?resolve():reject(new Error(errors||'Appearance command failed')))});
    return JSON.parse(output);
  }
  const liveChoice={expression:'love-eyes',accessories:{hair:'ponytail'}};
  const applied=await runAppearance(['apply',Buffer.from(JSON.stringify(liveChoice)).toString('base64')]);await sleep(600);
  check('appearance_command_hot_applies_without_restarting_controller',applied.applied&&controller.exitCode===null&&await c.evaluate('globalThis.__codexLive2D.status().appearance.accessories.hair==="ponytail"&&globalThis.__codexLive2D.status().expression==="爱心眼"'));
  const reset=await runAppearance(['reset']);await sleep(600);
  check('appearance_command_reset_restores_saved_defaults',reset.applied&&await c.evaluate('globalThis.__codexLive2D.status().appearance.expression==="auto"&&Object.keys(globalThis.__codexLive2D.status().appearanceParameters).length===0'));
  const effectResult=await runAppearance(['effect','draw-undo']);await sleep(100);
  check('effect_command_reaches_running_pet_without_saving_it',effectResult.applied&&!effectResult.saved&&await c.evaluate('globalThis.__codexLive2D.status().desktopEffect==="draw-undo"&&globalThis.__codexLive2D.status().appearanceParameters.chehui===1'));
  await sleep(800);check('effect_command_releases_temporary_parameters',await c.evaluate('globalThis.__codexLive2D.status().desktopEffect===null&&!("chehui" in globalThis.__codexLive2D.status().appearanceParameters)'));
  const beforeSelection=await c.evaluate('globalThis.__codexLive2D.status().frames');
  await c.evaluate(`{const root=document.querySelector('.codex-avatar-root');const custom=root.cloneNode(false);custom.removeAttribute('data-codex-live2d-rendered');custom.setAttribute('data-codex-pet-id',${JSON.stringify(fixturePetId)});root.replaceWith(custom);}`);
  await sleep(900);
  const afterSelection=await c.evaluate('globalThis.__codexLive2D.status()');
  check('default_pet_to_custom_pet_transition_keeps_rendering',afterSelection.active&&afterSelection.rootConnected&&afterSelection.frames>beforeSelection&&!afterSelection.lastError);
  check('custom_pet_selection_keeps_auto_mode_unbound',readSettings().petId===null);
  const restored=await runAppearance(['restore'],'launch.mjs');
  for(let i=0;i<120&&controller.exitCode===null;i++)await sleep(100);
  check('controller_stops_without_closing_host',controller.exitCode===0&&await c.evaluate('document.querySelector("#native-pet")!==null'));
  check('controller_restores_original_picture',await c.evaluate('document.querySelectorAll("[data-codex-live2d-rendered]").length===0&&getComputedStyle(document.querySelector(".codex-avatar-root")).backgroundImage!=="none"'));
  check('restore_command_waits_for_controller_owned_verification',restored.restored===true&&restored.results[0].visible===true&&restored.results[0].reloaded===false);
  check('controller_persists_the_verified_restore_outcome',JSON.parse(fs.readFileSync(path.join(sessionDir,'controller.json'),'utf8')).restoration.restored===true);
  report.pass=true;
}catch(error){report.pass=false;report.failure=error.stack;process.exitCode=1;}
finally{if(controller?.exitCode===null)controller.kill();if(proxy)await new Promise(resolve=>proxy.close(resolve));if(c){try{await c.send('Browser.close');}catch{}c.close();}if(browser.exitCode==null)browser.kill();fs.writeFileSync(path.join(artifacts,'browser-report.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2));}
