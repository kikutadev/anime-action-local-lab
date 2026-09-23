import { withQaRuntime } from "./qa_runtime.mjs";
import fs from "fs";
import path from "path";

const ROOT="/Users/kiku28/pj/game/anime-action-local-lab";
const OUT=path.join(ROOT,"qa-captures","current");
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
  const motion=spec.split("|")[0];
  for(let i=0;i<50;i++){
    const before=logs.length;
    const expr="window.unityInstance && window.unityInstance.SendMessage("+
      JSON.stringify("ModelQualityShowcase")+","+
      JSON.stringify("SetQaState")+","+
      JSON.stringify(spec)+")";
    await call("Runtime.evaluate",{expression:expr});
    await sleep(120);
    if(logs.slice(before).some(x=>x.includes("QA_POSE")&&x.includes("motion="+motion))){
      await sleep(180);
      return;
    }
  }
  throw new Error("QA pose not acknowledged: "+spec+"\n"+logs.slice(-40).join("\n"));
}
async function shot(name){
  const s=await call("Page.captureScreenshot",{format:"png",fromSurface:true,captureBeyondViewport:false});
  fs.writeFileSync(path.join(OUT,name+".png"),Buffer.from(s.data,"base64"));
}

const specs=[
 ["idle_front","idle|0|front"],
 ["idle_threequarter","idle|0|threequarter"],
 ["idle_side","idle|0|side"],
 ["walk_000","walk|0.00|threequarter"],
 ["walk_025","walk|0.25|threequarter"],
 ["walk_050","walk|0.50|threequarter"],
 ["walk_075","walk|0.75|threequarter"],
 ["jog_000","jog|0.00|threequarter"],
 ["jog_025","jog|0.25|threequarter"],
 ["jog_050","jog|0.50|threequarter"],
 ["jog_075","jog|0.75|threequarter"],
];
for(const [name,spec] of specs){
  await setPose(spec);
  await shot(name);
  console.log("captured",name);
}
fs.writeFileSync(path.join(OUT,"qa.log"),logs.join("\n"));

console.log("DONE",OUT);
});
