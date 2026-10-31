import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {CdpConnection,listTargets,isNativePetTarget} from './cdp.mjs';
import {readCore} from './core-runtime.mjs';
import {readSettings} from './settings.mjs';
import {readAppearance,compileAppearanceCatalog} from './appearance.mjs';
import {restoreNativePets} from './restore.mjs';

export const packageRoot=path.dirname(fileURLToPath(import.meta.url));
export const defaultPetId=null;
const sleep=ms=>new Promise(resolve=>setTimeout(resolve,ms));
const hash=bytes=>crypto.createHash('sha256').update(bytes).digest('hex');

export function verifyAssets() {
  const manifest=JSON.parse(fs.readFileSync(path.join(packageRoot,'assets-manifest.json'),'utf8'));
  for(const file of manifest.files){
    const absolute=path.resolve(packageRoot,file.path);
    if(!absolute.startsWith(packageRoot+path.sep))throw new Error('Asset path escapes the package');
    const bytes=fs.readFileSync(absolute);
    if(bytes.length!==file.bytes||hash(bytes)!==file.sha256)throw new Error(`Asset verification failed: ${file.path}`);
  }
  return manifest;
}

export function buildModelBundle() {
  const manifest=verifyAssets();
  const modelRoot=path.join(packageRoot,'assets/model');
  const model=JSON.parse(fs.readFileSync(path.join(packageRoot,manifest.model_file),'utf8'));
  const files=manifest.files.filter(x=>x.path.startsWith('assets/model/')&&!x.path.endsWith('.model3.json')).map(file=>{
    const relative=path.relative(modelRoot,path.join(packageRoot,file.path)).split(path.sep).join('/');
    return {path:relative,mime:relative.endsWith('.png')?'image/png':relative.endsWith('.json')?'application/json':'application/octet-stream',base64:fs.readFileSync(path.join(packageRoot,file.path)).toString('base64')};
  });
  const motionCatalog={};
  for(const [group,entries] of Object.entries(model.FileReferences?.Motions??{})){
    const entry=entries[0];
    const file=files.find(file=>file.path===entry?.File);
    if(!file)continue;
    const data=JSON.parse(Buffer.from(file.base64,'base64').toString('utf8'));
    motionCatalog[group]={durationMs:Math.max(400,Math.min(8000,Number(data.Meta?.Duration)*1000||1600)),parameters:data.Curves.filter(curve=>curve.Target==='Parameter').map(curve=>curve.Id)};
  }
  return {model,files,motionCatalog,appearanceCatalog:compileAppearanceCatalog(model,files)};
}

export async function installOnConnection(connection,{petId=defaultPetId,allowAnyPet=petId===null,fps=60}={}) {
  if(![30,60].includes(fps))throw new Error('Choose 30 or 60 FPS');
  const manifest=verifyAssets();
  await connection.send('Runtime.enable');
  const original=await connection.evaluate('({core:!!globalThis.Live2DCubismCore,vendor:!!globalThis.__dshLive2dPetVendor,vendorBuildId:globalThis.__dshLive2dPetVendor?.buildId??null})');
  if(!original.core)await connection.evaluate(readCore()+'\n;!!globalThis.Live2DCubismCore');
  if(!original.vendor||original.vendorBuildId!==manifest.runtime_build.id){
    await connection.evaluate('globalThis.__codexLive2D?.dispose?.()');
    await connection.evaluate(fs.readFileSync(path.join(packageRoot,'assets/runtime/live2d-vendor.js'),'utf8')+'\n;!!globalThis.__dshLive2dPetVendor');
  }
  const {installCodexLive2D}=await import('./renderer.mjs');
  const config={petId,allowAnyPet,fps,appearance:readAppearance(),bundle:buildModelBundle()};
  return connection.evaluate(`(${installCodexLive2D.toString()})(${JSON.stringify(config)})`);
}

async function main() {
  const args=process.argv.slice(2),command=args.shift()??'status';
  const option=(name,fallback)=>{const at=args.indexOf(name);return at<0?fallback:args[at+1]??fallback;};
  if(command==='verify'){console.log(JSON.stringify({verifiedFiles:verifyAssets().files.length}));return;}
  const port=Number(option('--port','9343'));
  const fps=Number(option('--fps','60'));
  const petId=option('--pet-id',readSettings().petId);
  if(command==='restore'){console.log(JSON.stringify({command,port,...await restoreNativePets(port)},null,2));return;}
  const targets=(await listTargets(port)).filter(isNativePetTarget);
  if(!targets.length)throw new Error('No official Codex pet overlay is exposed on this local debug port. Wake the pet in the debug-enabled Codex session first.');
  const results=[];
  for(const target of targets){
    const connection=await CdpConnection.connect(target.webSocketDebuggerUrl);
    try {
      if(command==='attach')results.push(await installOnConnection(connection,{petId,fps,allowAnyPet:petId===null||args.includes('--allow-any-pet')}));
      else if(command==='status')results.push(await connection.evaluate('globalThis.__codexLive2D?.status?.() ?? {active:false,installed:false}'));
      else throw new Error('Commands: verify, status, attach, restore');
    }finally{connection.close();}
  }
  console.log(JSON.stringify({command,port,results},null,2));
}

if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url))main().catch(error=>{console.error(error.message);process.exitCode=1;});
