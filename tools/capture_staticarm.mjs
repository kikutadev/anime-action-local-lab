import { withQaRuntime } from "./qa_runtime.mjs";
import fs from "fs";
const OUT="/Users/kiku28/pj/game/anime-action-local-lab/qa-captures/staticarm";
fs.rmSync(OUT,{recursive:true,force:true});fs.mkdirSync(OUT,{recursive:true});

await withQaRuntime(async ({ call, logs, sleep }) => {
await call("Runtime.enable");await call("Log.enable");await call("Page.enable");
await call("Page.reload",{ignoreCache:true});
for(let i=0;i<120;i++){const r=await call("Runtime.evaluate",{expression:"!!window.unityInstance",returnByValue:true});if(r.result?.value===true)break;await sleep(250);}
async function pose(spec){
 for(let i=0;i<60;i++){
  const before=logs.length;
  const expr='window.unityInstance&&window.unityInstance.SendMessage("ModelQualityShowcase","SetQaState",'+JSON.stringify(spec)+')';
  await call("Runtime.evaluate",{expression:expr});
  await sleep(350);
  if(logs.slice(before).some(x=>x.includes("QA_POSE"))){await sleep(450);return;}
 }
 throw new Error("pose not ready "+spec+"\n"+logs.slice(-80).join("\n"));
}
async function shot(name){const s=await call("Page.captureScreenshot",{format:"png",fromSurface:true,captureBeyondViewport:false});fs.writeFileSync(OUT+"/"+name+".png",Buffer.from(s.data,"base64"));}
const specs=[
["idle_front","idle|0|front|1|0|0|0"],["idle_3q","idle|0|threequarter|1|0|0|0"],["idle_side","idle|0|side|1|0|0|0"],
["walk_000","walk|0|threequarter|1|0|0|0"],["walk_025","walk|0.25|threequarter|1|0|0|0"],["walk_050","walk|0.5|threequarter|1|0|0|0"],["walk_075","walk|0.75|threequarter|1|0|0|0"],
["jog_000","jog|0|threequarter|1|0|0|0"],["jog_025","jog|0.25|threequarter|1|0|0|0"],["jog_050","jog|0.5|threequarter|1|0|0|0"],["jog_075","jog|0.75|threequarter|1|0|0|0"]
];
for(const [name,spec] of specs){await pose(spec);await shot(name);console.log("captured",name);}
fs.writeFileSync(OUT+"/qa.log",logs.join("\n"));
});
