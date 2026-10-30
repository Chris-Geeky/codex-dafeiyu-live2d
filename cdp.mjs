// Minimal loopback-only CDP transport, using Node.js built-ins.
export class CdpConnection {
  constructor(socket, timeoutMs=30000) {
    this.socket=socket; this.timeoutMs=timeoutMs; this.nextId=1; this.pending=new Map(); this.events=[];
    socket.addEventListener('message', event=>{
      const message=JSON.parse(event.data);
      const waiter=this.pending.get(message.id);
      if(waiter){this.pending.delete(message.id);clearTimeout(waiter.timer);message.error?waiter.reject(new Error(message.error.message)):waiter.resolve(message.result);}
      else if(message.method==='Runtime.exceptionThrown') this.events.push(message.params.exceptionDetails);
    });
    socket.addEventListener('close',()=>{
      for(const waiter of this.pending.values()){clearTimeout(waiter.timer);waiter.reject(new Error('CDP target closed'));}
      this.pending.clear();
    });
  }
  static async connect(url) {
    const parsed=new URL(url);
    if(parsed.protocol!=='ws:'||parsed.hostname!=='127.0.0.1') throw new Error('Only a local 127.0.0.1 CDP endpoint is supported');
    const socket=new WebSocket(url);
    await new Promise((resolve,reject)=>{
      const timer=setTimeout(()=>{socket.close();reject(new Error('CDP connect timed out'));},10000);
      socket.addEventListener('open',()=>{clearTimeout(timer);resolve();},{once:true});
      socket.addEventListener('error',()=>{clearTimeout(timer);reject(new Error('CDP connection failed'));},{once:true});
    });
    return new CdpConnection(socket);
  }
  send(method,params={}) {
    const id=this.nextId++;
    const result=new Promise((resolve,reject)=>{
      const timer=setTimeout(()=>{this.pending.delete(id);reject(new Error(`CDP request timed out: ${method}`));},this.timeoutMs);
      this.pending.set(id,{resolve,reject,timer});
    });
    this.socket.send(JSON.stringify({id,method,params}));return result;
  }
  async evaluate(expression) {
    const response=await this.send('Runtime.evaluate',{expression,awaitPromise:true,returnByValue:true});
    if(response.exceptionDetails) throw new Error(response.exceptionDetails.exception?.description??response.exceptionDetails.text??'Renderer evaluation failed');
    return response.result?.value;
  }
  close(){this.socket.close();}
}

export async function listTargets(port) {
  if(!Number.isInteger(port)||port<1024||port>65535)throw new Error('Expected a local debug port between 1024 and 65535');
  const response=await fetch(`http://127.0.0.1:${port}/json/list`,{signal:AbortSignal.timeout(5000)});
  if(!response.ok)throw new Error(`Local CDP returned HTTP ${response.status}`);
  return response.json();
}

export function isNativePetTarget(target) {
  if(target.type!=='page')return false;
  try {
    const url=new URL(target.url);
    return url.protocol==='app:'&&url.hostname==='-'&&(
      url.searchParams.get('initialRoute')==='/avatar-overlay'||
      url.pathname==='/avatar-overlay'||
      url.hash.split('?')[0]==='#/avatar-overlay'
    );
  }catch{return false;}
}
