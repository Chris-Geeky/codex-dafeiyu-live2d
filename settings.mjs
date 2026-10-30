import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

// Runtime data belongs to each recipient, never to the distributed source tree.
export const dataDir=path.resolve(process.env.DAFEIYU_DATA_DIR||path.join(process.env.LOCALAPPDATA||path.join(os.homedir(),'.local','share'),'CodexDafeiyuLive2D'));
export const sessionDir=path.join(dataDir,'sessions');
export const settingsFile=path.join(dataDir,'settings.json');
export function readSettings(){
  if(!fs.existsSync(settingsFile))return {petId:null,fps:60};
  const data=JSON.parse(fs.readFileSync(settingsFile,'utf8'));
  if(data.petId!==null&&(typeof data.petId!=='string'||!data.petId.trim()||data.petId.length>256))throw new Error('Invalid local pet binding');
  if(![30,60].includes(data.fps))throw new Error('Invalid local FPS setting');
  return {petId:data.petId,fps:data.fps};
}
export function writeSettings(update){
  const next={...readSettings(),...update};
  if(next.petId!==null&&(typeof next.petId!=='string'||!next.petId.trim()||next.petId.length>256))throw new Error('Invalid pet ID');
  if(![30,60].includes(next.fps))throw new Error('Choose 30 or 60 FPS');
  fs.mkdirSync(dataDir,{recursive:true});
  const temporary=settingsFile+'.'+process.pid+'.tmp';
  fs.writeFileSync(temporary,JSON.stringify(next,null,2)+'\n');
  fs.renameSync(temporary,settingsFile);
  return next;
}
