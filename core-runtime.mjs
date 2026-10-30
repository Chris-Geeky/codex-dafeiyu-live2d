import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {dataDir} from './settings.mjs';

const root=path.dirname(fileURLToPath(import.meta.url));
export const corePath=path.join(dataDir,'runtime','live2dcubismcore.min.js');
export const coreLicenseUrl='https://www.live2d.com/eula/live2d-proprietary-software-license-agreement_en.html';
const descriptor=()=>JSON.parse(fs.readFileSync(path.join(root,'assets-manifest.json'),'utf8')).core_runtime;
export function validateCore(bytes,expected=descriptor()){
  if(bytes.length!==expected.bytes||crypto.createHash('sha256').update(bytes).digest('hex')!==expected.sha256)throw new Error('Live2D Core integrity check failed. The official CDN may have changed; do not bypass verification.');
  return bytes;
}
export function readCore(){
  if(!fs.existsSync(corePath))throw new Error('Live2D Core is not prepared. Open CodexDafeiyu.exe, or read the license and run: node setup.mjs --accept-live2d-license');
  return validateCore(fs.readFileSync(corePath)).toString('utf8');
}
export function hasCore(){try{readCore();return true;}catch{return false;}}
export async function prepareCore({acceptLicense=false}={}){
  if(hasCore())return {ready:true,downloaded:false};
  if(!acceptLicense)throw new Error('Review '+coreLicenseUrl+' before downloading. Pass --accept-live2d-license only if you agree.');
  const expected=descriptor();
  const url=new URL(expected.url);
  if(url.protocol!=='https:'||url.hostname!=='cubism.live2d.com')throw new Error('Expected the official Live2D HTTPS source');
  const response=await fetch(url,{signal:AbortSignal.timeout(30000),redirect:'error'});
  if(!response.ok)throw new Error('Official Live2D download failed: HTTP '+response.status);
  const chunks=[];let length=0;
  for await(const chunk of response.body){length+=chunk.length;if(length>expected.bytes)throw new Error('Unexpected Live2D Core size');chunks.push(chunk);}
  const bytes=validateCore(Buffer.concat(chunks),expected);
  fs.mkdirSync(path.dirname(corePath),{recursive:true});
  const temporary=corePath+'.'+process.pid+'.tmp';
  try{fs.writeFileSync(temporary,bytes);fs.renameSync(temporary,corePath);}finally{try{fs.unlinkSync(temporary);}catch{}}
  fs.writeFileSync(path.join(path.dirname(corePath),'license-acceptance.json'),JSON.stringify({url:coreLicenseUrl,acceptedAt:new Date().toISOString(),sha256:expected.sha256},null,2));
  return {ready:true,downloaded:true};
}
