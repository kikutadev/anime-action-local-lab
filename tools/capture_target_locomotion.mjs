import { withQaRuntime } from "./qa_runtime.mjs";
import fs from "fs";
const OUT="/Users/kiku28/pj/game/anime-action-local-lab/qa-captures/target-locomotion";
fs.rmSync(OUT,{recursive:true,force:true}); fs.mkdirSync(OUT,{recursive:true});

await withQaRuntime(async ({ call, logs, sleep }) => {
await call("Runtime.enable"); await call("Page.enable");
await call("Page.reload",{ignoreCache:true});
await sleep(500);
logs.length=0;
const start=Date.now();
while(Date.now()-start<35000){
  const r=await call("Runtime.evaluate",{expression:"!!window.unityInstance",returnByValue:true});
  if(r.result?.value===true) break;
  await sleep(250);
}
for(let i=0;i<140 && !logs.some(x=>x.includes("VRoid action motor bound humanoid"));i++) await sleep(200);
async function setPose(spec){
  const before=logs.length;
  const expr="window.unityInstance&&window.unityInstance.SendMessage("+JSON.stringify("ModelQualityShowcase")+","+JSON.stringify("SetQaState")+","+JSON.stringify(spec)+")";
  for(let i=0;i<30;i++){
    await call("Runtime.evaluate",{expression:expr});
    await sleep(120);
    if(logs.slice(before).some(x=>x.includes("QA_POSE"))) break;
  }
  await sleep(180);
}
async function shot(name){
  const s=await call("Page.captureScreenshot",{format:"png",fromSurface:true,captureBeyondViewport:false});
  fs.writeFileSync(OUT+"/"+name+".png",Buffer.from(s.data,"base64"));
}
for(const [name,spec] of [
["idle_front","idle|0|front|0.45|0|0|-90"],
["idle_3q","idle|0|threequarter|0.45|0|0|-90"],
["idle_side","idle|0|side|0.45|0|0|-90"],
["jog_000","jog|0|threequarter|0.45|0|0|0"],
["jog_025","jog|0.25|threequarter|0.45|0|0|0"],
["jog_050","jog|0.50|threequarter|0.45|0|0|0"],
["jog_075","jog|0.75|threequarter|0.45|0|0|0"]
]){
  await setPose(spec); await shot(name); console.log("captured",name);
}
fs.writeFileSync(OUT+"/qa.log",logs.join("\n"));
});
