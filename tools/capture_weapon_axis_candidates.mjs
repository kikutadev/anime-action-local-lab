import { withQaRuntime } from "./qa_runtime.mjs";
import fs from "fs";
import path from "path";

const ROOT="/Users/kiku28/pj/game/anime-action-local-lab";
const OUT=path.join(ROOT,"qa-captures","weapon-axis");
fs.rmSync(OUT,{recursive:true,force:true});
fs.mkdirSync(OUT,{recursive:true});

await withQaRuntime(async ({ call, logs, sleep }) => {
await call("Runtime.enable");
await call("Log.enable");
await call("Page.enable");
await call("Page.reload",{ignoreCache:true});
await sleep(300);
await call("Emulation.setDeviceMetricsOverride",{width:700,height:900,deviceScaleFactor:1,mobile:false,screenWidth:700,screenHeight:900});

const readyStart=Date.now();
let unityReady=false;
while(Date.now()-readyStart<30000){
  const r=await call("Runtime.evaluate",{expression:"!!window.unityInstance",returnByValue:true});
  if(r.result?.value===true){ unityReady=true; break; }
  await sleep(250);
}
if(!unityReady) throw new Error("Unity instance did not become ready");

const avatarStart=Date.now();
while(Date.now()-avatarStart<30000){
  const loaded=logs.some(x=>x.includes("Loaded AvatarSample_C.vrm"));
  const bound=logs.some(x=>x.includes("VRoid action motor bound humanoid"));
  if(loaded && bound) break;
  await sleep(200);
}
if(!logs.some(x=>x.includes("VRoid action motor bound humanoid"))){
  throw new Error("VRoid humanoid did not become ready\n"+logs.slice(-60).join("\n"));
}

async function setPose(spec){
  const expr="window.unityInstance && window.unityInstance.SendMessage("+
    JSON.stringify("ModelQualityShowcase")+","+
    JSON.stringify("SetQaState")+","+
    JSON.stringify(spec)+")";
  await call("Runtime.evaluate",{expression:expr});
  await sleep(400);
}
async function shot(name){
  const s=await call("Page.captureScreenshot",{format:"png",fromSurface:true,captureBeyondViewport:false});
  fs.writeFileSync(path.join(OUT,name+".png"),Buffer.from(s.data,"base64"));
}

const specs=[
 ["e_000_000_000","swordidle|0|threequarter|0.45|0|0|0"],
 ["e_090_000_000","swordidle|0|threequarter|0.45|90|0|0"],
 ["e_n90_000_000","swordidle|0|threequarter|0.45|-90|0|0"],
 ["e_000_090_000","swordidle|0|threequarter|0.45|0|90|0"],
 ["e_000_n90_000","swordidle|0|threequarter|0.45|0|-90|0"],
 ["e_000_180_000","swordidle|0|threequarter|0.45|0|180|0"],
 ["e_000_000_090","swordidle|0|threequarter|0.45|0|0|90"],
 ["e_000_000_n90","swordidle|0|threequarter|0.45|0|0|-90"],
]
for(const [name,spec] of specs){
  await setPose(spec);
  await shot(name);
  console.log("captured",name);
}
fs.writeFileSync(path.join(OUT,"qa.log"),logs.join("\n"));

console.log("DONE",OUT);
});
