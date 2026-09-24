export const QA_VIEWS = Object.freeze([
  { id: "front", label: "Front" },
  { id: "front_threequarter_l", label: "Front 3/4 L" },
  { id: "side_l", label: "Side L" },
  { id: "back_threequarter_l", label: "Back 3/4 L" },
  { id: "back", label: "Back" },
  { id: "back_threequarter_r", label: "Back 3/4 R" },
  { id: "side_r", label: "Side R" },
  { id: "front_threequarter_r", label: "Front 3/4 R" },
]);

export const QA_SUPPLEMENTARY_VIEWS = Object.freeze([
  { id: "top_slight", label: "Top slight" },
  { id: "low_slight", label: "Low slight" },
]);

export const QA_ACTIONS = Object.freeze({
  slash: {
    phases: [
      { id: "anticipation", phase: 0.12 },
      { id: "early_swing", phase: 0.28 },
      { id: "contact", phase: 0.42 },
      { id: "follow_through", phase: 0.68 },
      { id: "recovery", phase: 0.90 },
    ],
    keyPhase: "contact",
    upperBodyWeight: 0,
    weaponEuler: [0, 0, -90],
    runtimeWeaponPose: true,
  },
  dodge: {
    phases: [
      { id: "ready", phase: 0.05 },
      { id: "push_off", phase: 0.22 },
      { id: "travel", phase: 0.50 },
      { id: "settle", phase: 0.86 },
    ],
    keyPhase: "travel",
    upperBodyWeight: 0,
    weaponEuler: [0, 90, 0],
  },
  run: {
    phases: [
      { id: "contact_a", phase: 0.00 },
      { id: "down", phase: 0.125 },
      { id: "passing", phase: 0.25 },
      { id: "up", phase: 0.375 },
      { id: "contact_b", phase: 0.50 },
    ],
    keyPhase: "passing",
    upperBodyWeight: 0,
    weaponEuler: [0, 0, -35],
  },
});

function phaseToken(value) {
  return String(Math.round(value * 1000)).padStart(3, "0");
}

export function makeQaSpec(actionId, phase, viewId) {
  const action = QA_ACTIONS[actionId];
  if (!action) throw new Error(`Unknown QA action: ${actionId}`);
  const [x, y, z] = action.weaponEuler;
  const parts = [
    actionId,
    Number(phase).toFixed(3),
    viewId,
    action.upperBodyWeight,
    x,
    y,
    z,
  ];
  if (action.runtimeWeaponPose) parts.push("auto");
  return parts.join("|");
}

export function buildQaMatrixCaptures() {
  const captures = [];
  for (const [actionId, action] of Object.entries(QA_ACTIONS)) {
    for (const phase of action.phases) {
      for (const view of QA_VIEWS) {
        captures.push({
          kind: "matrix",
          action: actionId,
          phaseId: phase.id,
          phase: phase.phase,
          view: view.id,
          name: `${actionId}__${phase.id}__${view.id}`,
          spec: makeQaSpec(actionId, phase.phase, view.id),
        });
      }
    }

    const keyPhase = action.phases.find(phase => phase.id === action.keyPhase);
    if (!keyPhase) throw new Error(`Missing key phase for ${actionId}: ${action.keyPhase}`);
    for (const view of QA_SUPPLEMENTARY_VIEWS) {
      captures.push({
        kind: "supplementary",
        action: actionId,
        phaseId: keyPhase.id,
        phase: keyPhase.phase,
        view: view.id,
        name: `${actionId}__${keyPhase.id}__${view.id}`,
        spec: makeQaSpec(actionId, keyPhase.phase, view.id),
      });
    }
  }
  return captures;
}

export function buildIdleReferenceCaptures() {
  return QA_VIEWS.map(view => ({
    kind: "reference",
    action: "idle",
    phaseId: "reference",
    phase: 0,
    view: view.id,
    name: `idle__${view.id}`,
    spec: `idle|0.000|${view.id}|0.20|0|0|-35`,
  }));
}

export function createManifestSkeleton() {
  return {
    schemaVersion: 2,
    views: QA_VIEWS,
    supplementaryViews: QA_SUPPLEMENTARY_VIEWS,
    actions: Object.fromEntries(
      Object.entries(QA_ACTIONS).map(([id, action]) => [
        id,
        {
          phases: action.phases,
          keyPhase: action.keyPhase,
        },
      ]),
    ),
    staticCaptures: [],
    runtimeSequences: {},
  };
}
