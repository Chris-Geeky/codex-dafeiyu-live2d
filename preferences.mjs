import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {dataDir} from './settings.mjs';
export const preferencesFile=path.join(dataDir,'preferences.json');
export const defaultPreferences=Object.freeze({theme:'system',dragCompatibility:true,earlyAvoidance:false,previewBackground:'theme'});
export function normalizePreferences(value={}){
  if(!value||typeof value!=='object'||Array.isArray(value)||Object.keys(value).some(k=>!Object.hasOwn(defaultPreferences,k)))throw new Error('Invalid preferences');
  const next={...defaultPreferences,...value};
  if(!['system','light','dark'].includes(next.theme)||!['theme','light','dark','checker'].includes(next.previewBackground)||typeof next.dragCompatibility!=='boolean'||typeof next.earlyAvoidance!=='boolean')throw new Error('Invalid preferences');
  return next;
}
export function readPreferences(){return normalizePreferences(fs.existsSync(preferencesFile)?JSON.parse(fs.readFileSync(preferencesFile,'utf8')):{});}
export function writePreferences(update){
  const next=normalizePreferences({...readPreferences(),...update});fs.mkdirSync(dataDir,{recursive:true});
  const temporary=preferencesFile+'.'+process.pid+'.tmp';fs.writeFileSync(temporary,JSON.stringify(next,null,2)+'\n');fs.renameSync(temporary,preferencesFile);return next;
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
  try{const action=process.argv[2]??'get';if(!['get','set'].includes(action))throw new Error('Commands: get, set');console.log(JSON.stringify(action==='get'?readPreferences():writePreferences(JSON.parse(Buffer.from(process.argv[3]??'','base64').toString('utf8')))));}catch(error){console.error(error.message);process.exitCode=1;}
}
