// Pure timing and shuffle policy, serialized as a factory for the CDP install.
export function createExpressionCycle({all=[],daily=[],states={},random=Math.random}={}){
  const bags=new Map(),history=[];let clock=0,nextAt=6000,until=0,current=null,last=null,key=null,scope='all',lastState=null,stateSince=0;
  function pick(names,poolKey){
    let bag=bags.get(poolKey);
    if(!bag?.length){bag=[...new Set(names)];for(let i=bag.length-1;i>0;i--){const j=Math.floor(random()*(i+1));[bag[i],bag[j]]=[bag[j],bag[i]];}if(bag.length>1&&bag.at(-1)===last)[bag[0],bag[bag.length-1]]=[bag.at(-1),bag[0]];bags.set(poolKey,bag);}
    return bag.pop()??null;
  }
  return {
    tick(delta,{state='idle',enabled=true,range='all'}={}){
      clock+=Math.max(0,Number(delta)||0);scope=range;
      if(state!==lastState){lastState=state;stateSince=clock;}
      const priority=states[state]?.length&&clock-stateSince<14000;
      const names=priority?states[state]:range==='daily'?daily:all;
      const nextKey=(priority?state:'ambient')+':'+range;
      if(!enabled){current=null;key=null;lastState=null;nextAt=clock+2000;return null;}
      if(nextKey!==key){if(key!==null){current=null;nextAt=Math.min(nextAt,clock+800);}key=nextKey;}
      if(current&&clock>=until)current=null;
      if(clock>=nextAt&&names.length){current=pick(names,key);last=current;until=clock+5000+random()*1500;nextAt=clock+8500+random()*3500;history.push(current);if(history.length>40)history.shift();}
      return current;
    },
    status(){return {current,scope,nextInMs:Math.max(0,Math.round(nextAt-clock)),remaining:bags.get(key)?.length??0,history:history.slice()};}
  };
}
