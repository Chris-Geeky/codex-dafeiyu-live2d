import fs from 'node:fs';
import path from 'node:path';
import http from 'node:http';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {appearanceCatalog,readAppearance} from './appearance.mjs';
import {buildModelBundle,verifyAssets} from './cli.mjs';
import {hasCore,readCore} from './core-runtime.mjs';
import {readSettings,sessionDir} from './settings.mjs';

const root=path.dirname(fileURLToPath(import.meta.url));
export async function startPreviewServer(){
  verifyAssets();const token=crypto.randomBytes(24).toString('hex'),prefix='/'+token+'/';
  let bundle=null,origin;
  const routes=new Map([['','assets/preview/index.html'],['ui.css','assets/preview/ui.css'],['ui.mjs','assets/preview/ui.mjs'],['renderer.mjs','renderer.mjs'],['vendor.js','assets/runtime/live2d-vendor.js']]);
  const types={'.html':'text/html; charset=utf-8','.css':'text/css; charset=utf-8','.mjs':'text/javascript; charset=utf-8','.js':'text/javascript; charset=utf-8'};
  const server=http.createServer((request,response)=>{
    const send=(status,body,type='application/json; charset=utf-8')=>{response.writeHead(status,{'Content-Type':type,'Cache-Control':'no-store','X-Content-Type-Options':'nosniff','Content-Security-Policy':"default-src 'none'; script-src 'self' 'wasm-unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' blob: data:; connect-src 'self' blob:; object-src 'none'; base-uri 'none'; frame-ancestors 'none'"});response.end(body)};
    try{
      if(request.method!=='GET'||request.headers.host!==origin.slice(7)||(request.headers.origin&&request.headers.origin!==origin))return send(403,'{}');
      const pathname=new URL(request.url,origin).pathname;if(!pathname.startsWith(prefix))return send(404,'{}');
      const route=pathname.slice(prefix.length);
      if(route==='initial.json')return send(200,JSON.stringify({catalog:appearanceCatalog,selection:readAppearance(),fps:readSettings().fps,coreReady:hasCore()}));
      if(route==='state.json'){
        let state='idle';try{state=JSON.parse(fs.readFileSync(path.join(sessionDir,'controller.json'),'utf8')).lastStatus?.state??state}catch{}
        return send(200,JSON.stringify({fps:readSettings().fps,state}));
      }
      if(route==='bundle.json'){bundle??=buildModelBundle();return send(200,JSON.stringify(bundle))}
      if(route==='core.js')return send(200,readCore(),'text/javascript; charset=utf-8');
      const file=routes.get(route);if(!file)return send(404,'{}');
      return send(200,fs.readFileSync(path.join(root,file)),types[path.extname(file)]);
    }catch{send(500,JSON.stringify({error:'Preview resources are not ready'}))}
  });
  await new Promise((resolve,reject)=>{server.once('error',reject);server.listen(0,'127.0.0.1',resolve)});
  origin='http://127.0.0.1:'+server.address().port;
  return {url:origin+prefix,close:()=>new Promise(resolve=>server.close(resolve)),server};
}
if(process.argv[1]&&path.resolve(process.argv[1])===fileURLToPath(import.meta.url)){
  const instance=await startPreviewServer();console.log(JSON.stringify({url:instance.url}));
  const parent=Number(process.argv[2]);
  const timer=setInterval(()=>{if(Number.isInteger(parent)&&parent>0)try{process.kill(parent,0)}catch{clearInterval(timer);instance.server.close();process.exit(0)}},2000);
  process.stdin.resume();process.stdin.once('end',()=>{clearInterval(timer);instance.server.close();process.exit(0)});
}
