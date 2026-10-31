import './isolated-data.mjs';
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import net from 'node:net';
import {spawn} from 'node:child_process';
import {selectCodex,acquireCodex,superviseCodex} from '../codex-lifecycle.mjs';
const installed=running=>({executable:'official-codex.exe',packageVersion:'fixture',running});
const app=(pid,port=null)=>({pid,port});
const pause=ms=>new Promise(resolve=>setTimeout(resolve,ms));

test('an existing compatible Codex is selected even alongside an ordinary process',()=>{
  assert.equal(selectCodex(installed([app(10),app(20,61108)])).app.pid,20);
  for(const port of [null,0,80,65536,'61108'])assert.equal(selectCodex(installed([app(10,port)])).kind,'needs-relaunch');
});

test('closing and immediately reopening normally is rediscovered without launching a duplicate',async()=>{
  const sequence=[installed([app(10)]),installed([app(20)]),installed([])],states=[];let launched=0;
  const connected=await acquireCodex({discover:()=>sequence.shift(),wait:async()=>{},onState:value=>states.push(value),launch:async()=>{launched++;return {appPid:30,port:61108}}});
  assert.equal(launched,1);assert.equal(connected.appPid,30);
  assert.deepEqual(states.map(x=>x.waitingPids),[[10],[20]]);
  assert.equal(states[1].waitingReason,'reopened-without-connection');
});

test('a manually reopened compatible process attaches immediately instead of waiting for old PIDs',async()=>{
  const sequence=[installed([app(10)]),installed([app(20,61109)])];let launched=false;
  const next=await acquireCodex({discover:()=>sequence.shift(),wait:async()=>{},launch:async()=>{launched=true}});
  assert.equal(next.appPid,20);assert.equal(next.startedOfficialClient,false);assert.equal(launched,false);
});

test('after app exit the supervisor waits without reopening Codex, then attaches its next compatible launch',async()=>{
  let stopped=false,current=installed([app(10,61108)]),sessions=0,launched=0;
  await superviseCodex({watch:true,discover:()=>current,launch:async()=>{launched++},shouldStop:()=>stopped,wait:async()=>{},
    runSession:async info=>{sessions++;assert.equal(info.appPid,sessions===1?10:20);if(sessions===1)current=installed([]);else stopped=true},
    onState:value=>{assert.equal(value.status,'watching-for-codex');assert.equal(launched,0);current=installed([app(20,61109)])}});
  assert.equal(sessions,2);assert.equal(launched,0);
});

test('an explicit helper click opens Codex from the idle watcher',async()=>{
  let stopped=false,request=false,current=installed([app(10,61108)]),launched=0,sessions=0;
  await superviseCodex({watch:true,discover:()=>current,shouldStop:()=>stopped,wait:async()=>{},takeLaunchRequest:()=>{const result=request;request=false;return result},
    launch:async()=>{launched++;return {appPid:20,port:61109}},
    runSession:async()=>{sessions++;if(sessions===1)current=installed([]);else stopped=true},onState:()=>{assert.equal(launched,0);request=true}});
  assert.equal(launched,1);assert.equal(sessions,2);
});

test('ordinary manual restarts remain open until the user arranges a handoff',async()=>{
  let stopped=false,request=false,normal=false,launched=0;const states=[];
  await superviseCodex({watch:true,discover:()=>normal?installed([app(20)]):installed([app(10,61108)]),shouldStop:()=>stopped,wait:async()=>{},
    takeLaunchRequest:()=>{const result=request;request=false;return result},launch:async()=>{launched++},runSession:async()=>{normal=true},
    onState:value=>{states.push(value);if(states.length===1)request=true;else stopped=true}});
  assert.deepEqual(states.map(x=>x.autoRelaunch),[false,true]);assert.equal(launched,0);
});

test('stopping while discovery is pending cannot open an application',async()=>{
  let stopped=false,launched=0;
  const result=await acquireCodex({discover:async()=>{stopped=true;return installed([])},shouldStop:()=>stopped,launch:async()=>{launched++}});
  assert.equal(result,null);assert.equal(launched,0);
});

test('cancelling an idle watcher does not wake Codex',async()=>{
  let stopped=false,launched=0,sessions=0;
  await superviseCodex({watch:true,discover:()=>sessions?installed([]):installed([app(10,61108)]),shouldStop:()=>stopped,
    launch:async()=>{launched++},runSession:async()=>{sessions++},wait:async()=>{},onState:()=>{stopped=true}});
  assert.equal(sessions,1);assert.equal(launched,0);
});

test('bounded non-watching waits still expire without closing Codex',async()=>{
  let now=0;const states=[];
  const result=await acquireCodex({discover:()=>installed([app(10)]),launch:()=>assert.fail('must not launch'),onState:value=>states.push(value),
    now:()=>now,timeoutMs:10,wait:async()=>{now=11}});
  assert.equal(result,null);assert.equal(states.at(-1).status,'expired');
});

test('the native-helper controller exits when its owning input pipe closes',async t=>{
  const data=process.env.DAFEIYU_DATA_DIR;
  if(!fs.existsSync(path.join(data,'runtime/live2dcubismcore.min.js'))){t.skip('Locally licensed Core required for the process integration check');return;}
  const server=net.createServer();await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));const port=server.address().port;await new Promise(resolve=>server.close(resolve));
  const child=spawn(process.execPath,[path.resolve('launch.mjs'),'start','--port',String(port),'--native-helper'],{env:{...process.env},windowsHide:true,stdio:['pipe','ignore','pipe']});
  let errors='';child.stderr.on('data',data=>{errors+=data});
  const completed=new Promise(resolve=>child.once('exit',code=>resolve(code)));
  let deadline;
  try{
    const stateFile=path.join(data,'sessions/controller.json');
    for(let i=0;i<100&&!fs.existsSync(stateFile);i++)await pause(25);
    assert.equal(fs.existsSync(stateFile),true,errors);child.stdin.end();
    const code=await Promise.race([completed,new Promise((_,reject)=>{deadline=setTimeout(()=>reject(Error('Orphan controller did not stop')),6000)})]);
    assert.equal(code,0,errors);assert.equal(JSON.parse(fs.readFileSync(stateFile,'utf8')).status,'stopped');
    assert.equal(fs.existsSync(path.join(data,'sessions/controller.lock')),false);
  }finally{clearTimeout(deadline);if(child.exitCode===null){child.kill();await completed}}
});
