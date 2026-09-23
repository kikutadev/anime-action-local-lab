import { withQaRuntime } from "./qa_runtime.mjs";
import fs from "fs";
import path from "path";
const OUT="/Users/kiku28/pj/game/anime-action-local-lab/qa-captures/runtime-transition";
fs.rmSync(OUT,{recursive:true,force:true}); fs.mkdirSync(OUT,{recursive:true});

await withQaRuntime(async ({ call, logs, sleep }) => {
await call("Runtime.enable"); await call("Log.enable"); await call("Page.enable");
await call("Page.reload",{ignoreCache:true});
await sleep(500);
logs.length=0;
const deadline=Date.now()+45000;
while(Date.now()<deadline){
  const u=await call("Runtime.evaluate",{expression:"!!window.unityInstance",returnByValue:true});
  if(u.result?.value && logs.some(x=>x.includes("Loaded AvatarSample_C.vrm")) && logs.some(x=>x.includes("VRoid action motor bound humanoid"))) break;
  await sleep(250);
}
if(!(logs.some(x=>x.includes("Loaded AvatarSample_C.vrm")) && logs.some(x=>x.includes("VRoid action motor bound humanoid")))) throw new Error("runtime not ready");
await sleep(400);
await shot("00_idle");

await call("Input.dispatchKeyEvent",{type:"keyDown",code:"KeyW",key:"w",windowsVirtualKeyCode:87,nativeVirtualKeyCode:87});
for(const [ms,name] of [[70,"01_start70"],[80,"02_start150"],[110,"03_start260"],[240,"04_run500"]]){
  await sleep(ms); await shot(name);
}
await call("Input.dispatchKeyEvent",{type:"keyUp",code:"KeyW",key:"w",windowsVirtualKeyCode:87,nativeVirtualKeyCode:87});
await sleep(100); await shot("05_stop100");
await sleep(180); await shot("06_stop280");

fs.writeFileSync(path.join(OUT,"qa.log"),logs.join("\n"));

console.log("TRANSITION_QA_OK");
});
