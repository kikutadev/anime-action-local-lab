import assert from "node:assert/strict";
import {
  QA_ACTIONS,
  QA_SUPPLEMENTARY_VIEWS,
  QA_VIEWS,
  buildIdleReferenceCaptures,
  buildQaMatrixCaptures,
  createManifestSkeleton,
} from "./qa_pose_matrix.mjs";

assert.equal(QA_VIEWS.length, 8, "action QA must cover eight horizontal views");
assert.equal(
  new Set(QA_VIEWS.map(view => view.id)).size,
  QA_VIEWS.length,
  "QA view ids must be unique",
);
assert.equal(
  QA_SUPPLEMENTARY_VIEWS.length,
  2,
  "QA should retain top/low supplementary views",
);

assert.deepEqual(
  QA_ACTIONS.slash.phases.map(phase => phase.id),
  ["anticipation", "early_swing", "contact", "follow_through", "recovery"],
);
assert.deepEqual(
  QA_ACTIONS.dodge.phases.map(phase => phase.id),
  ["ready", "push_off", "travel", "settle"],
);
assert.deepEqual(
  QA_ACTIONS.run.phases.map(phase => phase.id),
  ["contact_a", "down", "passing", "up", "contact_b"],
);

const captures = [
  ...buildIdleReferenceCaptures(),
  ...buildQaMatrixCaptures(),
];
assert.equal(captures.length, 126, "static QA capture count changed unexpectedly");
assert.equal(
  new Set(captures.map(capture => capture.name)).size,
  captures.length,
  "QA capture names must be unique",
);
assert.ok(
  captures.every(capture => {
    const fieldCount = capture.spec.split("|").length;
    return fieldCount === 7 || fieldCount === 8;
  }),
  "all QA captures must use the complete pose spec",
);
assert.ok(
  captures
    .filter(capture => capture.action === "slash")
    .every(capture => capture.spec.endsWith("|auto")),
  "slash QA must use the runtime weapon-pose curve",
);

const manifest = createManifestSkeleton();
assert.equal(manifest.schemaVersion, 2);
assert.deepEqual(Object.keys(manifest.actions), ["slash", "dodge", "run"]);

console.log(
  "PASS qa pose matrix",
  `views=${QA_VIEWS.length}`,
  `captures=${captures.length}`,
);
