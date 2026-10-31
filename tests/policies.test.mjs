import assert from 'node:assert/strict';
import test from 'node:test';
import {createExpressionCycle} from '../expression-cycle.mjs';
import {createDragCompatibility} from '../drag-compatibility.mjs';
import {normalizePreferences} from '../preferences.mjs';
import {appearanceCatalog,normalizeAppearance} from '../appearance.mjs';
const names=appearanceCatalog.expressions.map(x=>x.name).filter(Boolean);
test('all twenty faces rotate without repetition in normal running state',()=>{
 const cycle=createExpressionCycle({all:names,daily:appearanceCatalog.automaticExpressions,random:()=>.37});
 for(let i=0;i<20;i++)cycle.tick(12000,{state:'running'});
 assert.equal(names.length,20);assert.equal(new Set(cycle.status().history).size,20);
 cycle.tick(12000,{state:'idle'});const h=cycle.status().history;assert.notEqual(h.at(-1),h.at(-2));
});
test('daily rotation and urgent states have their own complete pools',()=>{
 assert.equal(appearanceCatalog.automaticExpressions.length,12);
 const cycle=createExpressionCycle({all:names,daily:appearanceCatalog.automaticExpressions,states:{failed:['悲伤','大哭']},random:()=>.4});
 for(let i=0;i<12;i++)cycle.tick(12000,{state:'running',range:'daily'});
 assert.deepEqual([...new Set(cycle.status().history)].sort(),[...appearanceCatalog.automaticExpressions].sort());
 cycle.tick(12000,{state:'failed'});cycle.tick(12000,{state:'failed'});assert.deepEqual(new Set(cycle.status().history.slice(-2)),new Set(['悲伤','大哭']));
 assert.equal(cycle.tick(1,{enabled:false}),null);
});
test('old appearance data gains full rotation and rejects unknown ranges',()=>{assert.equal(normalizeAppearance({}).expressionScope,'all');assert.throws(()=>normalizeAppearance({expressionScope:'broken'}));});
test('a persistent review state cannot trap rotation in three faces',()=>{const c=createExpressionCycle({all:names,daily:appearanceCatalog.automaticExpressions,states:{review:['星星眼','爱心眼','情绪花花']},random:()=>.4});for(let i=0;i<28;i++)c.tick(12000,{state:'review'});assert.equal(new Set(c.status().history).size,20);});
test('theme and compatibility preferences validate a closed schema',()=>{
 assert.deepEqual(normalizePreferences(),{theme:'system',dragCompatibility:true,previewBackground:'theme'});
 for(const value of [{theme:'dark'},{theme:'light',previewBackground:'checker'},{dragCompatibility:false}])assert.doesNotThrow(()=>normalizePreferences(value));
 for(const value of [{theme:'unknown'},{dragCompatibility:'true'},{previewBackground:'red'},{path:'../file'},null,[]])assert.throws(()=>normalizePreferences(value));
});
function fixture(open=true){const calls=[];const bridge={async isOpen(){return open},async hide(){calls.push('hide');open=false},async show(){calls.push('show');open=true}};return {bridge,calls,isOpen:()=>open};}
test('dragging hides and restores only an originally visible pet',async()=>{
 const f=fixture(),c=createDragCompatibility(f.bridge);await c.setMoving(true);assert.equal(f.isOpen(),false);await c.setMoving(false);assert.equal(f.isOpen(),true);assert.deepEqual(f.calls,['hide','show']);await c.close();
});
test('dragging never wakes an already hidden pet',async()=>{const f=fixture(false),c=createDragCompatibility(f.bridge);await c.setMoving(true);await c.setMoving(false);await c.close();assert.deepEqual(f.calls,[]);});
test('disabled compatibility leaves the pet alone',async()=>{const f=fixture(),c=createDragCompatibility(f.bridge,{enabled:false});await c.setMoving(true);await c.setMoving(false);assert.deepEqual(f.calls,[]);await c.close();});
test('release during visibility query cancels a stale hide request',async()=>{const f=fixture();let release;f.bridge.isOpen=()=>new Promise(resolve=>release=resolve);const c=createDragCompatibility(f.bridge);const start=c.setMoving(true);const end=c.setMoving(false);release(true);await Promise.all([start,end]);assert.deepEqual(f.calls,[]);await c.close();});
test('rapid release while hide is pending restores in sequence',async()=>{
 const f=fixture();let release;const nativeHide=f.bridge.hide;f.bridge.hide=async()=>{await new Promise(r=>release=r);await nativeHide()};const c=createDragCompatibility(f.bridge);
 const start=c.setMoving(true);for(let i=0;i<5&&!release;i++)await Promise.resolve();const end=c.setMoving(false);release();await Promise.all([start,end]);assert.deepEqual(f.calls,['hide','show']);assert.equal(c.status().temporarilyHidden,false);await c.close();
});
test('disabling or closing while dragging restores the owned pet',async()=>{for(const close of [false,true]){const f=fixture(),c=createDragCompatibility(f.bridge);await c.setMoving(true);if(close)await c.close();else{await c.setEnabled(false);await c.close()}assert.equal(f.isOpen(),true);assert.equal(c.status().restoredCount,1);}});
test('failed restoration remains owned and is retried on close',async()=>{
 const f=fixture();const show=f.bridge.show;let failed=false;f.bridge.show=async()=>{if(!failed){failed=true;throw Error('Unavailable')}await show()};const c=createDragCompatibility(f.bridge);await c.setMoving(true);await c.setMoving(false);assert.equal(c.status().temporarilyHidden,true);await c.close();assert.equal(f.isOpen(),true);assert.equal(c.status().lastError,null);
});
