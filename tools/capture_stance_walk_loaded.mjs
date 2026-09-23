import { withQaRuntime } from "./qa_runtime.mjs";
import fs from "fs";
const OUT="/Users/kiku28/pj/game/anime-action-local-lab/qa-captures/stance-walk-loaded";
fs.rmSync(OUT,{recursive:true,force:true});fs.mkdirSync(OUT,{recursive:true});

await withQaRuntime(async ({ call, logs, sleep }) => {
await call("Runtime.enable");await call("Page.enable");
const specs=[
["idle0_front","idle|0|front|0.45|0|0|0"],["idle0_3q","idle|0|threequarter|0.45|0|0|0"],["idle0_side","idle|0|side|0.45|0|0|0"],
["idleZn90_front","idle|0|front|0.45|0|0|-90"],["idleZn90_3q","idle|0|threequarter|0.45|0|0|-90"],["idleZn90_side","idle|0|side|0.45|0|0|-90"],
["walk0_000","walk|0|threequarter|0.45|0|0|0"],["walk0_025","walk|0.25|threequarter|0.45|0|0|0"],["walk0_050","walk|0.50|threequarter|0.45|0|0|0"],["walk0_075","walk|0.75|threequarter|0.45|0|0|0"],
["walkZn90_000","walk|0|threequarter|0.45|0|0|-90"],["walkZn90_025","walk|0.25|threequarter|0.45|0|0|-90"],["walkZn90_050","walk|0.50|threequarter|0.45|0|0|-90"],["walkZn90_075","walk|0.75|threequarter|0.45|0|0|-90"],
["jog0_000","jog|0|threequarter|0.45|0|0|0"],["jog0_025","jog|0.25|threequarter|0.45|0|0|0"],["jog0_050","jog|0.50|threequarter|0.45|0|0|0"],["jog0_075","jog|0.75|threequarter|0.45|0|0|0"]
];
for(const [name,spec] of specs){
 const expr='window.unityInstance&&window.unityInstance.SendMessage("ModelQualityShowcase","SetQaState",'+JSON.stringify(spec)+')';
 await call("Runtime.evaluate",{expression:expr});await new Promise(r=>setTimeout(r,650));
 const shot=await call("Page.captureScreenshot",{format:"png",fromSurface:true,captureBeyondViewport:false});
 fs.writeFileSync(OUT+"/"+name+".png",Buffer.from(shot.data,"base64"));
 console.log("captured",name);
}
});
