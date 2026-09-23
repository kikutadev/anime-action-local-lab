import { withQaRuntime } from "./qa_runtime.mjs";
import fs from "fs";
const OUT="/Users/kiku28/pj/game/anime-action-local-lab/qa-captures/runtime-check";
fs.rmSync(OUT,{recursive:true,force:true});fs.mkdirSync(OUT,{recursive:true});

await withQaRuntime(async ({ call, logs, sleep }) => {
await call("Runtime.enable");
await call("Log.enable");
await call("Page.enable");
await call("Page.reload",{ignoreCache:true});
await sleep(500);
logs.length=0;

const deadline=Date.now()+45000;
let ready=false;
while(Date.now()<deadline){
  const unity=await call("Runtime.evaluate",{expression:"!!window.unityInstance",returnByValue:true});
  const loaded=logs.some(x=>x.includes("Loaded AvatarSample_C.vrm"));
  const bound=logs.some(x=>x.includes("VRoid action motor bound humanoid"));
  if(unity.result?.value===true && loaded && bound){
    ready=true;
    break;
  }
  await sleep(250);
}
if(!ready){
  fs.writeFileSync(OUT+"/qa.log",logs.join("\n"));
  throw new Error("runtime QA not ready after 45s\n"+logs.slice(-80).join("\n"));
}

await sleep(500);
await shot("idle");

await call("Input.dispatchKeyEvent",{type:"keyDown",code:"KeyW",key:"w",windowsVirtualKeyCode:87,nativeVirtualKeyCode:87});
await sleep(900);
await shot("move");
await call("Input.dispatchKeyEvent",{type:"keyUp",code:"KeyW",key:"w",windowsVirtualKeyCode:87,nativeVirtualKeyCode:87});

await sleep(250);
fs.writeFileSync(OUT+"/qa.log",logs.join("\n"));

console.log("RUNTIME_QA_OK");
});
