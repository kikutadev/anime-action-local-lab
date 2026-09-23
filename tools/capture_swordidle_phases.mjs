import { withQaRuntime } from "./qa_runtime.mjs";
import fs from "fs";
const OUT="/Users/kiku28/pj/game/anime-action-local-lab/qa-captures/swordidle-phases";
fs.rmSync(OUT,{recursive:true,force:true}); fs.mkdirSync(OUT,{recursive:true});

await withQaRuntime(async ({ call, logs, sleep }) => {
await call("Runtime.enable"); await call("Page.enable");
const start=Date.now();
while(Date.now()-start<30000){
  const r=await call("Runtime.evaluate",{expression:"!!window.unityInstance",returnByValue:true});
  if(r.result?.value) break;
  await sleep(250);
}
for(let i=0;i<120 && !logs.some(x=>x.includes("VRoid action motor bound humanoid"));i++) await sleep(200);
async function pose(spec){
  const before=logs.length;
  const expr="window.unityInstance&&window.unityInstance.SendMessage("+JSON.stringify("ModelQualityShowcase")+","+JSON.stringify("SetQaState")+","+JSON.stringify(spec)+")";
  for(let i=0;i<30;i++){
    await call("Runtime.evaluate",{expression:expr}); await sleep(120);
    if(logs.slice(before).some(x=>x.includes("QA_POSE"))) break;
  }
  await sleep(180);
}
async function shot(name){
  const s=await call("Page.captureScreenshot",{format:"png",fromSurface:true,captureBeyondViewport:false});
  fs.writeFileSync(OUT+"/"+name+".png",Buffer.from(s.data,"base64"));
}
for (const p of [0,0.25,0.5,0.75]){
  const tag=String(p).replace(".","p");
  await pose("swordidle|"+p+"|threequarter|0|0|0|-90");
  await shot("swordidle_"+tag);
  console.log("captured",p);
}
});
