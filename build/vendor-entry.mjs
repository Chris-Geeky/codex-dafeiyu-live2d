// Same pinned rendering dependencies as A8Chann/dsh-pet-live2d.
// Pixi's official polyfill replaces generated shader/uniform functions with
// ordinary functions, preserving the host Content Security Policy.
// https://github.com/pixijs/pixijs/blob/v8.19.0/src/unsafe-eval/init.ts
import 'pixi.js/unsafe-eval';
import {Application,Assets,Point,extensions} from 'pixi.js';
import {MotionPriority,configureCubismSDK,Live2DModel,Live2DPlugin} from 'untitled-pixi-live2d-engine/cubism';
globalThis.__dshLive2dPetVendor={
  Application,Assets,Point,extensions,MotionPriority,configureCubismSDK,
  Live2DModel,Live2DPlugin,cspSafe:true,buildId:'codex-csp-1',
};
