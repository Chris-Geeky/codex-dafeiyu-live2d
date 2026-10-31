// Must be the first import in integration entry points. Test controllers never
// share stop requests, settings, or status files with the user's daily helper.
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
const source=process.env.DAFEIYU_DATA_DIR||path.join(process.env.LOCALAPPDATA||path.join(os.homedir(),'.local','share'),'CodexDafeiyuLive2D');
const temporary=fs.mkdtempSync(path.join(os.tmpdir(),'dafeiyu-tests-'));
const core=path.join(source,'runtime','live2dcubismcore.min.js');
if(fs.existsSync(core)){fs.mkdirSync(path.join(temporary,'runtime'),{recursive:true});fs.copyFileSync(core,path.join(temporary,'runtime','live2dcubismcore.min.js'));}
process.env.DAFEIYU_DATA_DIR=temporary;
process.once('exit',()=>{const resolved=path.resolve(temporary),boundary=path.resolve(os.tmpdir())+path.sep;if(resolved.startsWith(boundary)&&path.basename(resolved).startsWith('dafeiyu-tests-'))fs.rmSync(resolved,{recursive:true,force:true});});
