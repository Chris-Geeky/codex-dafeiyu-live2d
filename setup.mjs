import {prepareCore} from './core-runtime.mjs';
prepareCore({acceptLicense:process.argv.includes('--accept-live2d-license')})
  .then(result=>console.log(JSON.stringify(result)))
  .catch(error=>{console.error(error.message);process.exitCode=1;});
