import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {dataDir,sessionDir} from './settings.mjs';
import {CdpConnection,listTargets,isNativePetTarget} from './cdp.mjs';

const root=path.dirname(fileURLToPath(import.meta.url));
export const appearanceCatalog=JSON.parse(fs.readFileSync(path.join(root,'appearance-catalog.json'),'utf8'));
export const appearanceFile=path.join(dataDir,'appearance.json');
export function normalizeAppearance(value={}) {
  if(!value||typeof value!=='object'||Array.isArray(value))throw new Error('Invalid appearance settings');
  if(Object.keys(value).some(key=>!['expression','autoExpressions','expressionScope','accessories'].includes(key)))throw new Error('Unknown appearance setting');
  const expression=value.expression??'auto',autoExpressions=value.autoExpressions??true,accessories=value.accessories??{};
  if(!appearanceCatalog.expressions.some(item=>item.id===expression))throw new Error('Unknown facial expression');
  if(typeof autoExpressions!=='boolean'||!accessories||typeof accessories!=='object'||Array.isArray(accessories))throw new Error('Invalid appearance choices');
  if(Object.keys(accessories).some(key=>!appearanceCatalog.groups.some(group=>group.id===key)))throw new Error('Unknown accessory group');
  const expressionScope=value.expressionScope??'all';if(!['all','daily'].includes(expressionScope))throw new Error('Unknown expression scope');
  return {expression,autoExpressions,expressionScope,accessories:Object.fromEntries(appearanceCatalog.groups.map(group=>{
    const selected=accessories[group.id]??'none';
    if(!group.options.some(option=>option.id===selected))throw new Error('Unknown accessory: '+group.id);
    return [group.id,selected];
  }))};
}
export function readAppearance(){return normalizeAppearance(fs.existsSync(appearanceFile)?JSON.parse(fs.readFileSync(appearanceFile,'utf8')):{});}
export function writeAppearance(value){
  const next=normalizeAppearance(value);fs.mkdirSync(dataDir,{recursive:true});
  const temporary=appearanceFile+'.'+process.pid+'.tmp';
  fs.writeFileSync(temporary,JSON.stringify(next,null,2)+'\n');fs.renameSync(temporary,appearanceFile);return next;
}
export function compileAppearanceCatalog(model,files){
  const expressions=new Map((model.FileReferences.Expressions??[]).map(entry=>{
    const file=files.find(file=>file.path===entry.File);if(!file)throw new Error('Missing expression resource');
    return [entry.Name,JSON.parse(Buffer.from(file.base64,'base64').toString('utf8')).Parameters];
  }));
  const compile=option=>({...option,parameters:option.expressions.flatMap(name=>{
    if(!expressions.has(name))throw new Error('Unknown model expression: '+name);return expressions.get(name);
  })});
  return {...appearanceCatalog,groups:appearanceCatalog.groups.map(group=>({...group,options:group.options.map(compile)})),effects:(appearanceCatalog.effects??[]).map(compile)};
}
async function main(){
  const command=process.argv[2]??'get';
  if(command==='get'){console.log(JSON.stringify({selection:readAppearance(),catalog:appearanceCatalog}));return;}
  if(!['apply','reset','effect'].includes(command))throw new Error('Commands: get, apply, reset, effect');
  const encoded=process.argv[3];
  if(command==='apply'&&(!encoded||encoded.length>16384||!/^[A-Za-z0-9+/]*={0,2}$/.test(encoded)))throw new Error('Expected bounded base64-encoded appearance JSON');
  if(command==='effect'&&!appearanceCatalog.effects.some(effect=>effect.id===encoded))throw new Error('Unknown desktop effect');
  const selection=command==='effect'?readAppearance():writeAppearance(command==='reset'?{}:JSON.parse(Buffer.from(encoded,'base64').toString('utf8')));
  let applied=0,waiting=false;
  try{
    const state=JSON.parse(fs.readFileSync(path.join(sessionDir,'controller.json'),'utf8'));
    if(state.status==='active')for(const target of (await listTargets(state.port)).filter(isNativePetTarget)){
      const connection=await CdpConnection.connect(target.webSocketDebuggerUrl);
      try{if(await connection.evaluate(command==='effect'?`globalThis.__codexLive2D?.playEffect?.(${JSON.stringify(encoded)})===true`:`globalThis.__codexLive2D?.setAppearance?.(${JSON.stringify(selection)})?.active===true`))applied++;}finally{connection.close();}
    }
  }catch{waiting=true;}
  console.log(JSON.stringify({saved:command!=='effect',applied:applied>0,waiting,selection}));
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url))main().catch(error=>{console.error(error.message);process.exitCode=1;});
