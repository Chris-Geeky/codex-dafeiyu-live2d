import assert from 'node:assert/strict';
import test,{after} from 'node:test';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const temporary=fs.mkdtempSync(path.join(os.tmpdir(),'codex-dafeiyu-test-'));
process.env.DAFEIYU_DATA_DIR=path.join(temporary,'recipient');
const {dataDir,sessionDir,readSettings,writeSettings}=await import('../settings.mjs');
const {verifyAssets,defaultPetId}=await import('../cli.mjs');
const {corePath,prepareCore,hasCore,validateCore}=await import('../core-runtime.mjs');
const {isNativePetTarget,listTargets,CdpConnection}=await import('../cdp.mjs');
const {readAppearance,writeAppearance,appearanceFile}=await import('../appearance.mjs');
after(()=>fs.rmSync(temporary,{recursive:true,force:true}));

test('a new recipient starts without an author-specific pet binding',()=>{
  assert.equal(defaultPetId,null);assert.deepEqual(readSettings(),{petId:null,fps:60});
});
test('configuration and runtime stay inside the selected per-user data directory',()=>{
  assert.equal(dataDir,path.join(temporary,'recipient'));
  for(const value of [sessionDir,corePath])assert.ok(value.startsWith(dataDir+path.sep));
  assert.equal(fs.existsSync(path.join(path.dirname(fileURLToPath(import.meta.url)),'..','.sessions')),false);
});

test('appearance starts in automatic mode and stays separate from pet binding',()=>{
  const value=readAppearance();assert.equal(value.expression,'auto');assert.equal(value.autoExpressions,true);
  assert.ok(Object.values(value.accessories).every(choice=>choice==='none'));
  assert.ok(appearanceFile.startsWith(dataDir+path.sep));
  writeAppearance({expression:'star-eyes',autoExpressions:false,accessories:{glasses:'glasses-round',sticker:'sticker-cat'}});
  const restored=readAppearance();assert.equal(restored.expression,'star-eyes');assert.equal(restored.accessories.glasses,'glasses-round');assert.equal(restored.accessories.sticker,'sticker-cat');
  assert.deepEqual(readSettings(),{petId:null,fps:60});
});

test('appearance validates choices before replacing a saved outfit',()=>{
  const before=readAppearance();
  for(const value of [{expression:'not-a-face'},{autoExpressions:'true'},{accessories:{glasses:['glasses-round','sunglasses']}},{accessories:{unknown:'none'}},{accessories:{glasses:'../../private'}},JSON.parse('{"accessories":{"__proto__":"none"}}')])assert.throws(()=>writeAppearance(value));
  assert.deepEqual(readAppearance(),before);
  writeAppearance({});assert.equal(readAppearance().expression,'auto');assert.ok(Object.values(readAppearance().accessories).every(choice=>choice==='none'));
});
test('pet binding persists, FPS updates preserve it, and auto clears it',()=>{
  writeSettings({petId:'recipient-owned-pet'});writeSettings({fps:30});
  assert.deepEqual(readSettings(),{petId:'recipient-owned-pet',fps:30});
  writeSettings({petId:null});assert.deepEqual(readSettings(),{petId:null,fps:30});
});
test('invalid persisted settings cannot silently bind another character',()=>{
  for(const input of [{fps:120},{petId:''},{petId:42},{petId:'x'.repeat(257)}])assert.throws(()=>writeSettings(input));
  assert.deepEqual(readSettings(),{petId:null,fps:30});
});
test('all published model/vendor files match the manifest; Core is separate',()=>{
  const manifest=verifyAssets();assert.ok(manifest.files.length>=59);
  assert.ok(manifest.files.every(file=>!file.path.startsWith('.cache/')));
  assert.equal(new URL(manifest.core_runtime.url).hostname,'cubism.live2d.com');
});
test('proprietary Core is not downloaded without explicit license acceptance',async()=>{
  assert.equal(hasCore(),false);
  await assert.rejects(prepareCore(),/accept-live2d-license|Review/);
  assert.equal(fs.existsSync(corePath),false);
});
test('a modified Core is rejected even if its byte count matches',()=>{
  assert.throws(()=>validateCore(Buffer.from('malicious'),{bytes:9,sha256:'0'.repeat(64)}),/integrity/);
});
test('only the official native pet page is eligible for injection',()=>{
  const target=url=>({type:'page',url});
  assert.equal(isNativePetTarget(target('app://-/index.html?initialRoute=%2Favatar-overlay')),true);
  assert.equal(isNativePetTarget(target('app://-/avatar-overlay')),true);
  for(const url of ['app://-/index.html','https://example.org/avatar-overlay','app://attacker/avatar-overlay'])assert.equal(isNativePetTarget(target(url)),false);
  assert.equal(isNativePetTarget({type:'worker',url:'app://-/avatar-overlay'}),false);
});
test('remote endpoints and invalid debugging ports are rejected before connecting',async()=>{
  await assert.rejects(CdpConnection.connect('ws://example.org:9343/devtools/page/a'),/local/);
  await assert.rejects(CdpConnection.connect('wss://127.0.0.1:9343/devtools/page/a'),/local/);
  for(const port of [0,80,65536,'9343',NaN])await assert.rejects(listTargets(port),/port/);
});
