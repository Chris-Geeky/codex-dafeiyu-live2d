import './isolated-data.mjs';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {spawn} from 'node:child_process';
import {startPreviewServer} from '../preview-server.mjs';
import {CdpConnection,listTargets} from '../cdp.mjs';
import {readAppearance,appearanceFile} from '../appearance.mjs';
import {readSettings,writeSettings} from '../settings.mjs';
import {packageRoot} from '../cli.mjs';
const sleep=ms=>new Promise(r=>setTimeout(r,ms)),checks=[];
const check=(name,pass,evidence)=>{checks.push({name,pass:!!pass,evidence});if(!pass)throw Error(name)};
const original=readAppearance(),fileExisted=fs.existsSync(appearanceFile),initialSettings=readSettings();
const profile=fs.mkdtempSync(path.join(os.tmpdir(),'dafeiyu-preview-test-'));
let c,preview,browser,failure;
try{
 preview=await startPreviewServer();
 browser=spawn(process.env.EDGE_BINARY||'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',['--headless=new','--edge-skip-compat-layer-relaunch','--remote-debugging-address=127.0.0.1','--remote-debugging-port=0',`--user-data-dir=${profile}`,'--no-first-run','--no-default-browser-check','--disable-background-networking','--disable-extensions','--disable-sync','about:blank'],{windowsHide:true,stdio:'ignore'});
 const portFile=path.join(profile,'DevToolsActivePort');for(let i=0;i<120&&!fs.existsSync(portFile);i++)await sleep(100);
 c=await CdpConnection.connect((await listTargets(Number(fs.readFileSync(portFile,'utf8').split('\n')[0]))).find(t=>t.type==='page').webSocketDebuggerUrl);
 await c.send('Page.enable');await c.send('Emulation.setDeviceMetricsOverride',{width:900,height:800,deviceScaleFactor:2,mobile:false});
 await c.send('Page.navigate',{url:preview.url});
 let ready=false;for(let i=0;i<150;i++){await sleep(100);try{ready=await c.evaluate('window.__dafeiyuEditor?.state().ready===true')}catch{}if(ready)break}
 check('preview_initializes_the_real_model',ready);
 const before=await c.evaluate('globalThis.__codexLive2D.status().frames');await sleep(1200);const after=await c.evaluate('globalThis.__codexLive2D.status().frames');check('dynamic_preview_draws_continuously',after-before>30,{frames:after-before});
 await c.evaluate('document.querySelector("button[aria-label=眼镜]").click()');
 check('selector_opens_without_css_animation',await c.evaluate('(()=>{const m=document.querySelector(".choice-menu");return !!m&&getComputedStyle(m).animationDuration==="0s"&&getComputedStyle(m).transitionDuration==="0s"})()'));
 await c.send('Input.dispatchKeyEvent',{type:'keyDown',key:'ArrowDown',code:'ArrowDown'});await c.send('Input.dispatchKeyEvent',{type:'keyUp',key:'ArrowDown',code:'ArrowDown'});
 check('selector_keyboard_moves_one_option',await c.evaluate('document.activeElement.textContent==="圆眼镜"'));
 await c.evaluate('document.activeElement.click()');await sleep(600);check('selector_changes_the_preview',await c.evaluate('globalThis.__codexLive2D.status().appearanceParameters.ParamCheek70===1'));
 check('preview_selection_does_not_save_the_pet',fileExisted===fs.existsSync(appearanceFile)&&JSON.stringify(readAppearance())===JSON.stringify(original));
 await c.evaluate('window.__dafeiyuEditor.select({expression:"none",autoExpressions:false,accessories:{tools:"draw-brush"}})');await sleep(650);
 check('brush_pose_is_available',await c.evaluate('globalThis.__codexLive2D.status().appearanceParameters.bi===1'));
 await c.evaluate('window.__dafeiyuEditor.select({expression:"none",autoExpressions:false,accessories:{tools:"draw-eraser"}})');await sleep(650);
 check('eraser_replaces_brush_pose',await c.evaluate('(()=>{const p=globalThis.__codexLive2D.status().appearanceParameters;return p.pi===1&&!("bi" in p)})()'));
 await c.evaluate('document.getElementById("static").click()');await sleep(300);const frozen=await c.evaluate('globalThis.__codexLive2D.status().frames');await sleep(700);check('static_preview_stops_continuous_drawing',await c.evaluate('globalThis.__codexLive2D.status().frames')===frozen);
 await c.evaluate('window.__dafeiyuEditor.select({accessories:{sticker:"sticker-cat"}})');await sleep(1500);const settled=await c.evaluate('globalThis.__codexLive2D.status()');await sleep(400);check('static_preview_updates_then_freezes',settled.appearanceParameters.ParamCheek83===1&&await c.evaluate('globalThis.__codexLive2D.status().frames')===settled.frames);
 writeSettings({fps:30});await sleep(1300);check('preview_follows_saved_30_fps_setting',await c.evaluate('globalThis.__codexLive2D.status().fps===30'));
 await c.evaluate('document.getElementById("dynamic").click()');const start30=await c.evaluate('globalThis.__codexLive2D.status().frames');await sleep(1200);const delta30=await c.evaluate('globalThis.__codexLive2D.status().frames')-start30;check('dynamic_preview_obeys_30_fps_target',delta30>=20&&delta30<=43,{frames:delta30});
 for(const [effect,parameter] of [['draw-undo','chehui'],['menu-press','pointZ']]){
  check(effect+'_starts',await c.evaluate(`window.__dafeiyuEditor.effect(${JSON.stringify(effect)})`));await sleep(160);check(effect+'_visible',await c.evaluate(`globalThis.__codexLive2D.status().appearanceParameters[${JSON.stringify(parameter)}]===1`));await sleep(850);check(effect+'_releases',await c.evaluate(`globalThis.__codexLive2D.status().desktopEffect===null&&!(${JSON.stringify(parameter)} in globalThis.__codexLive2D.status().appearanceParameters)`));
 }
 check('ketchup_effect_starts_full_motion',await c.evaluate('window.__dafeiyuEditor.effect("ketchup-squeeze")&&globalThis.__codexLive2D.status().motion.group==="Ketchup"'));
 await sleep(200);check('ketchup_squeeze_parameter_is_visible',await c.evaluate('globalThis.__codexLive2D.status().appearanceParameters.ji===1'));
 const bad=await fetch(new URL('../missing/',preview.url));check('preview_requires_the_private_path',bad.status===404);
 const foreign=await fetch(preview.url+'initial.json',{headers:{Origin:'https://example.org'}});check('preview_blocks_foreign_origins',foreign.status===403);
 const binary=await fetch(preview.url+'assets/runtime/webview2/Microsoft.Web.WebView2.Core.dll');check('preview_does_not_serve_arbitrary_files',binary.status===404);
 writeSettings({fps:60});await sleep(1300);await c.evaluate('window.__dafeiyuEditor.select({});window.__dafeiyuEditor.mode(true)');await sleep(800);
 check('editor_controls_fit_the_window',await c.evaluate('document.documentElement.scrollHeight<=innerHeight+2'));
 const screenshot=await c.send('Page.captureScreenshot',{format:'png'});fs.writeFileSync(path.join(packageRoot,'tests/.artifacts/preview-editor.png'),Buffer.from(screenshot.data,'base64'));
}catch(error){failure=error.stack;process.exitCode=1}
finally{writeSettings(initialSettings);if(c){await c.send('Browser.close').catch(()=>{});c.close()}if(browser?.exitCode===null)browser.kill();if(preview)await preview.close();const report={checks,pass:!failure,failure};fs.writeFileSync(path.join(packageRoot,'tests/.artifacts/preview-report.json'),JSON.stringify(report,null,2));console.log(JSON.stringify(report,null,2))}
