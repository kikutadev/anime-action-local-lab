import { withQaRuntime } from "./qa_runtime.mjs";
import fs from "fs";
const OUT="/Users/kiku28/pj/game/anime-action-local-lab/qa-captures/weight-loaded";
fs.rmSync(OUT,{recursive:true,force:true});fs.mkdirSync(OUT,{recursive:true});

await withQaRuntime(async ({ call, logs, sleep }) => {
await call("Runtime.enable");await call("Page.enable");
const specs=[];
for(const weight of [0.45,0.72,1.0]){
 for(const phase of [0,0.25,0.5,0.75]){
  const tag=String(weight).replace(".","p");
  specs.push(["jog_"+tag+"_"+String(phase).replace(".",""),"jog|"+phase+"|threequarter|"+weight+"|0|0|0"]);
  specs.push(["walk_"+tag+"_"+String(phase).replace(".",""),"walk|"+phase+"|threequarter|"+weight+"|0|0|0"]);
 }
 specs.push(["idle_"+String(weight).replace(".","p")+"_3q","idle|0|threequarter|"+weight+"|0|0|0"]);
}
for(const [name,spec] of specs){
 const expr='window.unityInstance&&window.unityInstance.SendMessage("ModelQualityShowcase","SetQaState",'+JSON.stringify(spec)+')';
 await call("Runtime.evaluate",{expression:expr});await new Promise(r=>setTimeout(r,600));
 const shot=await call("Page.captureScreenshot",{format:"png",fromSurface:true,captureBeyondViewport:false});
 fs.writeFileSync(OUT+"/"+name+".png",Buffer.from(shot.data,"base64"));
}
});
